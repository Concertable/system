[CmdletBinding()]
param(
    [ValidateSet('All', 'Claude', 'Codex')][string]$Harness = 'All',
    [string]$AgentStandardsSource = 'Concertable/agent-standards',
    [string]$DotnetStandardsSource = 'tomjseery/dotagents',
    [string]$ReactStandardsSource = 'tomjseery/react-agents',
    [string]$Repository,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

$marketplaces = @(
    [pscustomobject]@{
        Name = 'agent-standards'
        Source = $AgentStandardsSource
        Plugins = @('agent-process', 'dotnet', 'react')
    }
    [pscustomobject]@{
        Name = 'dotagents'
        Source = $DotnetStandardsSource
        Plugins = @('dotnet-standards')
    }
    [pscustomobject]@{
        Name = 'react-agents'
        Source = $ReactStandardsSource
        Plugins = @('react-standards')
    }
)

function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-CodexExecutable {
    $command = Get-Command codex -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }

    $bundled = Join-Path $env:USERPROFILE '.codex\plugins\.plugin-appserver\codex.exe'
    if (Test-Path -LiteralPath $bundled) { return $bundled }

    throw 'Codex CLI was not found on PATH or in the Codex desktop plugin runtime.'
}

function Install-ClaudeStandards {
    $claude = (Get-Command claude -ErrorAction Stop | Select-Object -First 1).Source
    $configured = (& $claude plugin marketplace list | Out-String)
    if ($LASTEXITCODE -ne 0) { throw 'Could not list Claude marketplaces.' }

    if (-not $VerifyOnly) {
        foreach ($marketplace in $marketplaces) {
            $pattern = "(?m)^\s+\S+\s+$([regex]::Escape($marketplace.Name))\s*$"
            if ($configured -match $pattern) {
                Invoke-Checked $claude @('plugin', 'marketplace', 'update', $marketplace.Name)
            } else {
                Invoke-Checked $claude @('plugin', 'marketplace', 'add', $marketplace.Source, '--scope', 'user')
            }
        }

        $installed = @(& $claude plugin list --json | ConvertFrom-Json)
        foreach ($marketplace in $marketplaces) {
            foreach ($plugin in $marketplace.Plugins) {
                $id = "$plugin@$($marketplace.Name)"
                if ($installed | Where-Object { $_.id -eq $id -and $_.scope -eq 'user' }) {
                    Invoke-Checked $claude @('plugin', 'update', $id, '--scope', 'user', '--yes')
                } else {
                    Invoke-Checked $claude @('plugin', 'install', $id, '--scope', 'user', '--yes')
                }
            }
        }
    }

    $actual = @(& $claude plugin list --json | ConvertFrom-Json)
    foreach ($marketplace in $marketplaces) {
        foreach ($plugin in $marketplace.Plugins) {
            $id = "$plugin@$($marketplace.Name)"
            if (-not ($actual | Where-Object { $_.id -eq $id -and $_.scope -eq 'user' -and $_.enabled })) {
                throw "Claude plugin '$id' is not installed and enabled at user scope."
            }
        }
    }
    Write-Host 'Claude standards verified: 5/5 plugins installed and enabled at user scope.'
}

function Install-CodexStandards {
    $codex = Get-CodexExecutable
    $configured = (& $codex plugin marketplace list --json | ConvertFrom-Json).marketplaces
    if ($LASTEXITCODE -ne 0) { throw 'Could not list Codex marketplaces.' }

    if (-not $VerifyOnly) {
        foreach ($marketplace in $marketplaces) {
            $existing = $configured | Where-Object { $_.name -eq $marketplace.Name }
            if ($existing) {
                if ($existing.marketplaceSource.sourceType -eq 'git') {
                    Invoke-Checked $codex @('plugin', 'marketplace', 'upgrade', $marketplace.Name)
                }
            } else {
                Invoke-Checked $codex @('plugin', 'marketplace', 'add', $marketplace.Source)
            }
        }

        foreach ($marketplace in $marketplaces) {
            foreach ($plugin in $marketplace.Plugins) {
                Invoke-Checked $codex @('plugin', 'add', "$plugin@$($marketplace.Name)")
            }
        }
    }

    $actual = @((& $codex plugin list --json | ConvertFrom-Json).installed)
    foreach ($marketplace in $marketplaces) {
        foreach ($plugin in $marketplace.Plugins) {
            $id = "$plugin@$($marketplace.Name)"
            if (-not ($actual | Where-Object { $_.pluginId -eq $id -and $_.installed -and $_.enabled })) {
                throw "Codex plugin '$id' is not installed and enabled."
            }
        }
    }
    Write-Host 'Codex standards verified: 5/5 plugins installed and enabled.'
}

if ($Harness -in @('All', 'Claude')) { Install-ClaudeStandards }
if ($Harness -in @('All', 'Codex')) { Install-CodexStandards }

if ($Repository) {
    $repositoryRoot = (Resolve-Path -LiteralPath $Repository).Path
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.agents\skill-routes.json'))) {
        throw "$repositoryRoot has no .agents/skill-routes.json to verify."
    }
    $router = Join-Path (Split-Path -Parent $PSScriptRoot) '.agents\hooks\skill_router.py'
    $python = (Get-Command python -ErrorAction Stop | Select-Object -First 1).Source
    Push-Location $repositoryRoot
    try {
        if ($Harness -in @('All', 'Claude')) {
            Invoke-Checked $python @('-B', $router, '--verify-install', 'claude')
        }
        if ($Harness -in @('All', 'Codex')) {
            Invoke-Checked $python @('-B', $router, '--verify-install', 'codex')
        }
    } finally {
        Pop-Location
    }
}

Write-Host 'Start a new Claude or Codex session to load the refreshed skill catalogue.'
