# SDK evaluation checks only: no restore, build, containers, or user-secrets mutation.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("concertable-system-tests-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
function Write-Fixture([string]$Path, [string]$Value) {
    New-Item -ItemType Directory -Path (Split-Path $Path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($Path, $Value)
}
try {
    $bootstrap = Join-Path $PSScriptRoot 'system/setup-local-dev.ps1'
    $projectXml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsAspireHost>true</IsAspireHost><UserSecretsId>offline-system-fixture</UserSecretsId></PropertyGroup></Project>'
    $referenceXml = '<Project><ItemGroup><ProjectReference Include="../Runtime.csproj" IsAspireProjectResource="false" /></ItemGroup></Project>'
    foreach ($file in @('Directory.Build.props', 'Directory.Build.targets', 'foreign.targets')) {
        $directory = Join-Path $temporaryRoot $file.Replace('.', '-')
        $project = Join-Path $directory 'Host.csproj'
        $content = if ($file -eq 'foreign.targets') { $projectXml.Replace('</Project>', '<Import Project="foreign.targets" /></Project>') } else { $projectXml }
        Write-Fixture $project $content
        Write-Fixture (Join-Path $directory $file) $referenceXml
        $rejected = $false
        try { & $bootstrap -AppHostProject $project -WhatIf } catch {
            if ($_.Exception.Message -notmatch 'without source ProjectReferences') { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw "System accepted a runtime reference imported through $file." }
    }
    $validProject = Join-Path $temporaryRoot 'valid/Host.csproj'
    Write-Fixture $validProject $projectXml.Replace('</Project>', '<ItemGroup><ProjectReference Include="../Runtime.csproj" Condition="false" /></ItemGroup></Project>')
    & $bootstrap -AppHostProject $validProject -WhatIf
    Write-Host 'PASS: real MSBuild rejects Directory.Build.props, Directory.Build.targets, and explicit imported runtime references; ignores an inactive reference.'
} finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $comparison = if ([IO.Path]::DirectorySeparatorChar -eq '\') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $resolvedTemporaryRoot.StartsWith($tempPrefix, $comparison) -or
        (Split-Path $resolvedTemporaryRoot -Leaf) -notlike 'concertable-system-tests-*') { throw 'Unsafe temporary cleanup path.' }
    Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
}
