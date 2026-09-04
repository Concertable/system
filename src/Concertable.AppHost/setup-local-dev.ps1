<# Existing source-based full-stack compatibility bootstrap. No sibling source files are changed here. #>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'tools/OwnerOperations.psm1') -Force
Initialize-OwnerDevelopment -Root $PSScriptRoot -AppHostProject 'Concertable.AppHost.csproj' `
    -SecretKeys @('ServiceAuth:B2BClientSecret', 'ServiceAuth:CustomerClientSecret', 'ServiceAuth:AuthClientSecret') `
    -WhatIf:$WhatIfPreference
