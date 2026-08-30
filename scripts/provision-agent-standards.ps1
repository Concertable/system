[CmdletBinding()]
param(
    [ValidateSet('All', 'Claude', 'Codex')][string]$Harness = 'All',
    [ValidateSet('Generic', 'Concertable')][string]$StandardsScope = 'Generic',
    [string]$AgentStandardsSource,
    [string]$DotnetStandardsSource,
    [string]$ReactStandardsSource,
    [string]$Repository,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

# This script is vendored as a single executable bootstrap. It must not depend on files beside it:
# a consumer needs it to install or refresh the plugin that owns every automatic hook.
$marketplaces = @(
    [pscustomobject]@{ Name = 'dotagents'; Source = $DotnetStandardsSource; Plugins = @('dotnet-standards') }
    [pscustomobject]@{ Name = 'react-agents'; Source = $ReactStandardsSource; Plugins = @('react-standards') }
)
if ($StandardsScope -eq 'Concertable') {
    $marketplaces = @(
        [pscustomobject]@{ Name = 'agent-standards'; Source = $AgentStandardsSource; Plugins = @('concertable', 'dotnet', 'react') }
    ) + $marketplaces
}

function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}

function Get-CodexExecutable {
    $command = @(Get-Command codex -All -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandType -notin @('Function', 'Alias') } |
        Select-Object -First 1)
    if ($command.Count -gt 0) { return $command[0].Source }
    $bundled = Join-Path $env:USERPROFILE '.codex\plugins\.plugin-appserver\codex.exe'
    if (Test-Path -LiteralPath $bundled) { return $bundled }
    throw 'Codex CLI was not found on PATH as an actual executable, or in the Codex desktop plugin runtime.'
}

function Get-ClaudeExecutable {
    $command = @(Get-Command claude -All -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandType -notin @('Function', 'Alias') } |
        Select-Object -First 1)
    if ($command.Count -gt 0) { return $command[0].Source }
    throw 'Claude Code CLI was not found on PATH as an actual executable.'
}

function Install-ClaudeStandards {
    $claude = Get-ClaudeExecutable
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
}

if ($Harness -in @('All', 'Claude')) { Install-ClaudeStandards }
if ($Harness -in @('All', 'Codex')) { Install-CodexStandards }

if ($Repository) {
    $repositoryRoot = (Resolve-Path -LiteralPath $Repository).Path
    $router = Join-Path $repositoryRoot '.agents\hooks\skill_router.py'
    if (-not (Test-Path -LiteralPath $router)) { throw "$repositoryRoot has no local skill router to verify." }
    $python = (Get-Command python -ErrorAction Stop | Select-Object -First 1).Source
    Push-Location $repositoryRoot
    try {
        if ($Harness -in @('All', 'Claude')) { Invoke-Checked $python @('-B', $router, '--verify-install', 'claude') }
        if ($Harness -in @('All', 'Codex')) { Invoke-Checked $python @('-B', $router, '--verify-install', 'codex') }
    } finally {
        Pop-Location
    }
}

Write-Host "Standards provisioning verified for $Harness ($StandardsScope scope). Start a new session to load the refreshed catalogue."
