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

$repoRoot = Split-Path -Parent $PSScriptRoot
$roster = ConvertFrom-Json ([System.IO.File]::ReadAllText((Join-Path $repoRoot '.agents\plugins\install-roster.json')))

# Only the marketplace/plugin roster is genuinely shared - both harnesses install the same three
# marketplaces and five plugins, just through different CLI mechanics, so it is authored once in
# .agents/plugins/install-roster.json rather than duplicated per harness. Everything that differs by
# harness (marketplace add/update syntax, plugin install semantics, autoUpdate) lives in that harness's
# own install.ps1 instead. These three parameters override the roster's default source per marketplace,
# for testing against a fork or an unpublished branch.
$sourceOverrides = @{
    'agent-standards' = $AgentStandardsSource
    'dotagents'        = $DotnetStandardsSource
    'react-agents'     = $ReactStandardsSource
}
$marketplaces = @($roster.marketplaces | Where-Object {
    $_.scope -eq 'generic' -or $StandardsScope -eq 'Concertable'
} | ForEach-Object {
    $override = $sourceOverrides[$_.name]
    [pscustomobject]@{
        Name    = $_.name
        Source  = if ($override) { $override } else { $_.source }
        Plugins = @($_.plugins)
    }
})

if ($Harness -in @('All', 'Claude')) {
    & (Join-Path $repoRoot '.claude\install.ps1') -Marketplaces $marketplaces -VerifyOnly:$VerifyOnly
}
if ($Harness -in @('All', 'Codex')) {
    & (Join-Path $repoRoot '.codex\install.ps1') -Marketplaces $marketplaces -VerifyOnly:$VerifyOnly
}

if ($Repository) {
    $repositoryRoot = (Resolve-Path -LiteralPath $Repository).Path
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.agents\skill-routes.json'))) {
        throw "$repositoryRoot has no .agents/skill-routes.json to verify."
    }
    $router = Join-Path $repoRoot '.agents\hooks\skill_router.py'
    $python = (Get-Command python -ErrorAction Stop | Select-Object -First 1).Source
    . (Join-Path $repoRoot '.agents\plugins\install-helpers.ps1')
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

Write-Host "Start a new Claude or Codex session to load the refreshed $StandardsScope skill catalogue."
