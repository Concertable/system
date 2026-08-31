param(
    [Parameter(Mandatory = $true)]
    [string] $Destination,

    [Parameter(Mandatory = $true)]
    [string] $PackageFeed
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$destinationPath = [IO.Path]::GetFullPath($Destination)
$packageFeedPath = (Resolve-Path -LiteralPath $PackageFeed).Path

if (Test-Path -LiteralPath $destinationPath) {
    throw "E2E carve destination already exists: $destinationPath"
}

$paths = @(
    "api\Directory.Build.targets",
    "api\TestConventions.targets",
    "api\BannedSymbols.txt",
    "api\BannedSymbols.UnitTests.txt",
    "api\tests\Concertable.System.E2E.slnx",
    "api\tests\Concertable.System.E2E",
    "api\tests\nuget.config",
    "api\Concertable.Shared\Directory.Build.props",
    "api\Concertable.Shared\Directory.Packages.props",
    "api\Concertable.Shared\tests\Directory.Build.targets",
    "api\Concertable.Shared\tests\Concertable.Testing.E2E",
    "api\Concertable.Shared\tests\FilterNdjsonTagsTask.cs",
    "api\Concertable.Shared\tests\StripFeatureTraitsTask.cs",
    "api\Concertable.Shared\tests\StripReqnrollHooksTask.cs",
    "api\Concertable.B2B\Directory.Build.props",
    "api\Concertable.B2B\Directory.Build.targets",
    "api\Concertable.B2B\Directory.Packages.props",
    "api\Concertable.B2B\tests\E2ETests\Directory.Build.targets",
    "api\Concertable.B2B\tests\E2ETests\Concertable.B2B.E2ETests",
    "api\Concertable.B2B\tests\E2ETests\Concertable.B2B.E2ETests.Ui",
    "api\Concertable.Customer\Directory.Build.props",
    "api\Concertable.Customer\Directory.Build.targets",
    "api\Concertable.Customer\Directory.Packages.props",
    "api\Concertable.Customer\tests\E2ETests\Directory.Build.targets",
    "api\Concertable.Customer\tests\E2ETests\Concertable.Customer.E2ETests",
    "api\Concertable.Customer\tests\E2ETests\Concertable.Customer.E2ETests.Mobile",
    "api\Concertable.Customer\tests\E2ETests\Concertable.Customer.E2ETests.Ui",
    "api\Concertable.Payment\Directory.Build.props",
    "api\Concertable.Payment\Directory.Build.targets",
    "api\Concertable.Payment\Directory.Packages.props",
    "api\Concertable.Payment\tests\E2ETests\Concertable.Payment.E2ETests.Helpers",
    "api\Concertable.Payment\tests\E2ETests\Concertable.Payment.E2ETests.Helpers.UnitTests",
    "api\Concertable.Search\Directory.Build.props",
    "api\Concertable.Search\Directory.Build.targets",
    "api\Concertable.Search\Directory.Packages.props",
    "api\Concertable.Search\tests\E2ETests\Concertable.Search.E2ETests.Helpers"
    "api\Concertable.Search\tests\E2ETests\Concertable.Search.E2ETests.Helpers.UnitTests"
)

foreach ($relativePath in $paths) {
    $source = Join-Path $repoRoot $relativePath
    $target = Join-Path $destinationPath $relativePath
    if (Test-Path -LiteralPath $source -PathType Container) {
        Get-ChildItem -LiteralPath $source -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object {
                $relativeFile = [IO.Path]::GetRelativePath($source, $_.FullName)
                $targetFile = Join-Path $target $relativeFile
                New-Item -ItemType Directory -Path (Split-Path $targetFile -Parent) -Force | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $targetFile
            }
    } else {
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
}

$nugetConfigPath = Join-Path $destinationPath "api\tests\nuget.config"
$nugetConfig = Get-Content -LiteralPath $nugetConfigPath -Raw
$escapedFeed = [Security.SecurityElement]::Escape($packageFeedPath)
$nugetConfig = $nugetConfig.Replace(
    '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />',
    "<add key=`"stage4-local`" value=`"$escapedFeed`" />`r`n    <add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`" />")
$nugetConfig = $nugetConfig.Replace(
    '<packageSource key="nuget.org">',
    "<packageSource key=`"stage4-local`">`r`n      <package pattern=`"Concertable.*.TestKit`" />`r`n    </packageSource>`r`n    <packageSource key=`"nuget.org`">")
[IO.File]::WriteAllText($nugetConfigPath, $nugetConfig)

Write-Output $destinationPath
