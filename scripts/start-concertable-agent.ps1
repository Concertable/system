[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Codex', 'Claude')][string]$Harness,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$AgentArguments
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot

# The installed plugin is the automatic-hook owner. Upgrade and verify it before starting a session;
# a SessionStart hook cannot make the already-running session use the version it has just downloaded.
& (Join-Path $repositoryRoot 'scripts\provision-agent-standards.ps1') `
    -Harness $Harness `
    -StandardsScope Concertable

$command = $Harness.ToLowerInvariant()
$executable = @(Get-Command $command -All -ErrorAction Stop |
    Where-Object { $_.CommandType -notin @('Function', 'Alias') } |
    Select-Object -First 1).Source
if (-not $executable) {
    throw "$Harness CLI was not found after standards provisioning."
}

& $executable @AgentArguments
