$invocationArguments = @($args)
$Command = if ($invocationArguments.Count -gt 0) { [string]$invocationArguments[0] } else { $null }
$Target = if ($invocationArguments.Count -gt 1) { [string]$invocationArguments[1] } else { $null }
$Rest = if ($invocationArguments.Count -gt 2) { [string[]]$invocationArguments[2..($invocationArguments.Count - 1)] } else { @() }

if ($Command -notin @('prepare', 'restore', 'build', 'test')) {
    throw "Command must be one of: prepare, restore, build, test."
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$platformRoot = Join-Path $repoRoot 'artifacts/local-platform'
$packagesRoot = Join-Path $platformRoot 'packages'
$configPath = Join-Path $platformRoot 'nuget.config'
$versionPath = Join-Path $platformRoot 'version.txt'

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-LocalPlatformVersion {
    if (-not (Test-Path -LiteralPath $versionPath)) {
        throw "Local platform is not prepared. Run './scripts/local-platform.ps1 prepare' first."
    }

    return (Get-Content -Raw -LiteralPath $versionPath).Trim()
}

function Write-NuGetConfig {
    $content = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local-platform" value="packages" />
    <add key="github" value="https://nuget.pkg.github.com/Concertable/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="local-platform">
      <package pattern="Concertable.*" />
    </packageSource>
    <packageSource key="github">
      <package pattern="Concertable.*" />
    </packageSource>
  </packageSourceMapping>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="Concertable" />
      <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
    </github>
  </packageSourceCredentials>
</configuration>
'@
    [System.IO.File]::WriteAllText($configPath, $content, [System.Text.UTF8Encoding]::new($false))
}

function Initialize-LocalPlatform {
    $version = $env:LOCAL_PLATFORM_VERSION
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "0.1.0-local.$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    }

    if (Test-Path -LiteralPath $platformRoot) {
        Remove-Item -LiteralPath $platformRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null
    Write-NuGetConfig
    [System.IO.File]::WriteAllText($versionPath, $version, [System.Text.UTF8Encoding]::new($false))

    $solution = Join-Path $repoRoot 'api/Concertable.slnx'
    Invoke-DotNet @(
        'restore', $solution,
        '--disable-parallel'
    )
    Invoke-DotNet @(
        'pack', $solution,
        '--configuration', 'Release',
        '--output', $packagesRoot,
        '--no-restore',
        "-p:MinVerVersionOverride=$version"
    )

    $packableProjects = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'api') -Recurse -Filter '*.csproj' |
        Where-Object { Select-String -LiteralPath $_.FullName -SimpleMatch '<IsPackable>true</IsPackable>' -Quiet }
    $packages = Get-ChildItem -LiteralPath $packagesRoot -Filter '*.nupkg'
    $missing = $packableProjects | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $packagesRoot "$($_.BaseName).$version.nupkg"))
    }

    if ($missing.Count -gt 0 -or $packages.Count -ne $packableProjects.Count) {
        $missingNames = ($missing.BaseName | Sort-Object) -join ', '
        throw "Local platform package set is incomplete. Expected $($packableProjects.Count), found $($packages.Count). Missing: $missingNames"
    }

    Write-Host "Local platform $version prepared with $($packages.Count) packages at $packagesRoot."
}

function Invoke-LocalPlatformRestore([string]$Project) {
    $version = Get-LocalPlatformVersion
    Invoke-DotNet @(
        'restore', $Project,
        '--configfile', $configPath,
        "-p:ConcertablePlatformVersion=$version",
        "-p:MinVerVersionOverride=$version"
    )
}

function Get-Configuration([string[]]$Arguments) {
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        if ($Arguments[$index] -in @('--configuration', '-c') -and $index + 1 -lt $Arguments.Count) {
            return $Arguments[$index + 1]
        }
    }

    return 'Debug'
}

function Assert-DataAccessAssembly([string]$Project, [string]$Version, [string[]]$Arguments) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($Project)
    if ($projectName -notmatch '(\.IntegrationTests|\.E2ETests(?:\.Ui)?)$') {
        return
    }

    $projectPath = if ([System.IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $repoRoot $Project }
    $projectDirectory = Split-Path ([System.IO.Path]::GetFullPath($projectPath)) -Parent
    $configuration = Get-Configuration $Arguments
    $assemblies = @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory "bin/$configuration") -Recurse -Filter 'Concertable.DataAccess.Infrastructure.dll')

    if ($assemblies.Count -ne 1) {
        throw "Expected exactly one Concertable.DataAccess.Infrastructure.dll under $projectDirectory/bin/$configuration, found $($assemblies.Count)."
    }

    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblies[0].FullName).ProductVersion
    if ($productVersion -ne $Version -and -not $productVersion.StartsWith("$Version+")) {
        throw "Expected Concertable.DataAccess.Infrastructure.dll version $Version, found $productVersion at $($assemblies[0].FullName)."
    }

    Write-Host "Verified one Concertable.DataAccess.Infrastructure.dll at local platform version $Version."
}

Set-Location $repoRoot
[Environment]::CurrentDirectory = $repoRoot

switch ($Command) {
    'prepare' {
        Initialize-LocalPlatform
    }
    'restore' {
        if ([string]::IsNullOrWhiteSpace($Target)) { throw 'restore requires a project or solution target.' }
        Invoke-LocalPlatformRestore $Target | Out-Null
    }
    { $_ -in @('build', 'test') } {
        if ([string]::IsNullOrWhiteSpace($Target)) { throw "$Command requires a project or solution target." }
        Invoke-LocalPlatformRestore $Target
        $version = Get-LocalPlatformVersion
        Invoke-DotNet (@(
            $Command, $Target,
            '--no-restore',
            "-p:ConcertablePlatformVersion=$version",
            "-p:MinVerVersionOverride=$version"
        ) + $Rest)
        Assert-DataAccessAssembly $Target $version $Rest
    }
}
