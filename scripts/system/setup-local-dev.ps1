<# Configure a container-only System AppHost. A true System host must be supplied explicitly. #>
[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$AppHostProject)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $AppHostProject).Path
if ([IO.Path]::GetExtension($project) -ne '.csproj') { throw 'AppHostProject must be a .csproj file.' }
# Evaluate imports and Directory.Build files, even for WhatIf. This does not build or restore.
$evaluationOutput = @(& dotnet msbuild $project -nologo '-getProperty:IsAspireHost' '-getItem:ProjectReference' 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate System AppHost (exit $LASTEXITCODE)." }
$evaluation = ($evaluationOutput -join "`n") | ConvertFrom-Json -ErrorAction Stop
if ($evaluation.Properties.IsAspireHost -ne 'true') { throw 'Project must evaluate IsAspireHost=true.' }
# Package-only hosting is the extraction contract. A local source reference can conceal a runtime
# transitively even when IsAspireProjectResource=false, so System admits no ProjectReferences.
if (@($evaluation.Items.ProjectReference).Count -ne 0) {
    throw 'System bootstrap requires a package/container-only AppHost without source ProjectReferences. Use FullStack for the legacy umbrella.'
}
Import-Module (Join-Path $PSScriptRoot 'OwnerOperations.psm1') -Force
Initialize-OwnerDevelopment -Root (Split-Path $project -Parent) -AppHostProject (Split-Path $project -Leaf) `
    -SecretKeys @('ServiceAuth:B2BClientSecret', 'ServiceAuth:CustomerClientSecret', 'ServiceAuth:AuthClientSecret') `
    -WhatIf:$WhatIfPreference
