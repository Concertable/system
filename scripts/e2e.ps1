param(
    [Parameter(Position = 0)]
    [string]$domain,
    [Parameter(Position = 1)]
    [string]$cmd,
    [switch]$Headed
)

$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot
[Environment]::CurrentDirectory = $repoRoot
$localPlatform = Join-Path $PSScriptRoot 'local-platform.ps1'

$b2bUi      = Join-Path $repoRoot "api/Concertable.B2B/tests/E2ETests/Concertable.B2B.E2ETests.Ui"
$customerUi = Join-Path $repoRoot "api/Concertable.Customer/tests/E2ETests/Concertable.Customer.E2ETests.Ui"
$b2bApi      = Join-Path $repoRoot "api/Concertable.B2B/tests/E2ETests/Concertable.B2B.E2ETests"
$customerApi = Join-Path $repoRoot "api/Concertable.Customer/tests/E2ETests/Concertable.Customer.E2ETests"
$runsettings = Join-Path $repoRoot "api/Concertable.runsettings"

$quiet = @('--nologo', '--verbosity', 'quiet')

if (-not $Headed) { $env:HEADLESS = "true" }

function Invoke-PrettyTest([string]$suite, [string]$csproj, [string[]]$extra, [string]$logName = 'ui-tests.last.log') {
    $dir = Split-Path $csproj -Parent
    $log = Join-Path $dir $logName
    $resultsDir = Join-Path $dir 'TestResults'
    $trx = Join-Path $resultsDir 'run.trx'
    if (Test-Path $trx) { Remove-Item $trx -Force }

    Write-Host ""
    Write-Host "=== $suite (running...) ===" -ForegroundColor Cyan

    $testArgs = @($csproj) + $quiet + @(
        '--results-directory', $resultsDir,
        '--logger', 'trx;LogFileName=run.trx',
        '--logger', 'console;verbosity=normal'
    ) + $extra
    & $localPlatform test @testArgs *> $log
    $processExitCode = $LASTEXITCODE

    if (-not (Test-Path $trx)) {
        Write-Host "  No results -- build or run failed. Full log: $log" -ForegroundColor Red
        return [pscustomobject]@{ Suite = $suite; Passed = 0; Failed = 0; Total = 0; ExitCode = $processExitCode }
    }

    [xml]$xml = Get-Content $trx
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $results = $xml.SelectNodes("//t:UnitTestResult", $ns) | Sort-Object { $_.GetAttribute('testName') }

    $passed = 0; $failed = 0
    foreach ($r in $results) {
        $name = $r.GetAttribute('testName')
        if ($r.GetAttribute('outcome') -eq 'Passed') {
            Write-Host "  [PASS] $name" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  [FAIL] $name" -ForegroundColor Red
            $failed++
        }
    }
    return [pscustomobject]@{ Suite = $suite; Passed = $passed; Failed = $failed; Total = ($passed + $failed); ExitCode = $processExitCode }
}

function Show-Summary([object[]]$summaries) {
    Write-Host ""
    Write-Host "  Summary" -ForegroundColor Cyan
    Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f 'Suite', 'Passed', 'Failed', 'Total') -ForegroundColor Gray
    Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f '------', '------', '------', '-----') -ForegroundColor Gray
    foreach ($s in $summaries) {
        $color = if ($s.Failed -gt 0) { 'Red' } else { 'Green' }
        Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f $s.Suite, $s.Passed, $s.Failed, $s.Total) -ForegroundColor $color
    }
    $tp = ($summaries | Measure-Object Passed -Sum).Sum
    $tf = ($summaries | Measure-Object Failed -Sum).Sum
    $tt = ($summaries | Measure-Object Total -Sum).Sum
    $totalColor = if ($tf -gt 0) { 'Red' } else { 'Green' }
    Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f 'TOTAL', $tp, $tf, $tt) -ForegroundColor $totalColor
    Write-Host ""
}

function Complete-TestRun([object[]]$summaries) {
    Show-Summary $summaries
    $failed = ($summaries | Measure-Object Failed -Sum).Sum
    $emptySuites = @($summaries | Where-Object Total -eq 0).Count
    $abortedSuites = @($summaries | Where-Object ExitCode -ne 0).Count
    if ($failed -gt 0 -or $emptySuites -gt 0 -or $abortedSuites -gt 0) { exit 1 }
    exit 0
}

function Assert-DockerHealthy {
    # Structural gate: `docker ps` answering is NOT proof Docker is healthy (a
    # half-started engine forwards old containers' ports while new ones are dead).
    # docker-health.ps1 does a fresh-container host->container round-trip and exits
    # non-zero on the half-started signature. Never boot the stack without it.
    & (Join-Path $PSScriptRoot 'docker-health.ps1')
    if ($LASTEXITCODE -ne 0) {
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit 1
    }
}

function Assert-PinnedImagesPullable {
    # The stack runs Auth and Payment from digest-pinned ghcr.io images, so a boot needs a
    # registry credential CI gets from its `Log in to GHCR` step. Without one the pull fails
    # `unauthorized`, Aspire marks `auth` FailedToStart, every dependent cascades, and the
    # fixture then reports a health-check timeout on an unrelated port - three layers from
    # the cause. Fail here instead, naming the remedy.
    $appHost = Join-Path $PSScriptRoot '../api/Concertable.B2B/src/Concertable.B2B.AppHost/AppHost.cs'
    $image = (Select-String -Path $appHost -Pattern 'AuthImage = "([^"]+)"').Matches.Groups[1].Value
    $digest = (Select-String -Path $appHost -Pattern 'AuthDigest = "([^"]+)"').Matches.Groups[1].Value
    docker manifest inspect "$image@$digest" *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Cannot resolve the pinned Auth image $image@$digest." -ForegroundColor Red
        Write-Host "Log in to the registry, then retry:" -ForegroundColor Red
        Write-Host "  gh auth token | docker login ghcr.io -u <your-github-user> --password-stdin" -ForegroundColor Red
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit 1
    }
}

function Assert-HostCapacity {
    # An E2E boot needs the host mostly to itself; a competing full-solution build or another
    # E2E run starves it and it dies at fixture startup. Wait up to 5 min for transient
    # contention (a build finishing) before refusing. WHY in host-capacity.ps1.
    & (Join-Path $PSScriptRoot 'host-capacity.ps1') -WaitSeconds 300
    if ($LASTEXITCODE -ne 0) {
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit 1
    }
}

function Assert-SpaDependencies {
    # The UI stack boots the SPAs through `npm run dev`, so a checkout without an installed
    # workspace fails as `'vite' is not recognized` and every scenario dies waiting for readiness.
    # NEVER run `npm ci` over an existing app/node_modules: npm links workspace packages as NTFS
    # junctions, and ci's recursive wipe follows them and deletes the real sources under app/shared,
    # app/web/shared and their siblings. Only ci into an absent node_modules; repair an incomplete
    # one with `npm install`, which does not wipe the tree.
    $app = Join-Path $repoRoot 'app'
    $modules = Join-Path $app 'node_modules'
    # Probe the entrypoint the shim runs, not the shim: .bin/vite.cmd is a stub that outlives its own
    # package, so it reports a complete workspace while `npm run dev` dies on a missing module.
    if (Test-Path (Join-Path $modules 'vite/bin/vite.js')) { return }

    $command = if (Test-Path $modules) { 'install' } else { 'ci' }
    Write-Host "Installing SPA workspace dependencies (npm $command)..." -ForegroundColor Gray
    Push-Location $app
    try { & npm $command } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "npm $command failed in app/ -- the SPAs cannot start." -ForegroundColor Red
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit $LASTEXITCODE
    }
}

function Assert-SpaPackagesBuilt {
    # The shared workspace packages resolve through their exports map to dist/, which npm ci does not
    # produce -- their build is wired to prepack, not prepare. Without it the SPAs serve but every
    # page dies on "Failed to resolve import @concertable/web/...", so readiness passes and the
    # scenarios fail instead. dist/ on the widest shared package is the marker for the whole set.
    $app = Join-Path $repoRoot 'app'
    if (Test-Path (Join-Path $app 'web/shared/dist')) { return }

    Write-Host "Building shared SPA workspace packages (app/web/shared/dist is missing)..." -ForegroundColor Gray
    Push-Location $app
    try { & npm run build:web-packages } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "npm run build:web-packages failed -- the SPAs cannot resolve their shared imports." -ForegroundColor Red
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit $LASTEXITCODE
    }
}

function Assert-PlaywrightBrowsers {
    # Playwright pins its browser build to the Microsoft.Playwright package version, so a package
    # bump leaves the machine without the matching binary and every scenario dies in BeforeRun with
    # "Executable doesn't exist". playwright.ps1 is generated into the build output, so the project
    # has to be built before it can be reached. Both UI suites pin the same version, so one install
    # covers both, and the install is a no-op once the build is present.
    $csproj = "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
    & $localPlatform build $csproj @quiet
    if ($LASTEXITCODE -ne 0) {
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit $LASTEXITCODE
    }

    $script = Get-ChildItem -Path (Join-Path $b2bUi 'bin') -Filter 'playwright.ps1' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $script) {
        Write-Host "Could not find playwright.ps1 under $b2bUi/bin after building -- cannot verify browsers." -ForegroundColor Red
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit 1
    }

    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }
    & $shell -NoProfile -File $script.FullName install chromium
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Playwright browser install failed." -ForegroundColor Red
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        exit $LASTEXITCODE
    }
}

function Show-Usage {
    Write-Host ""
    Write-Host "  Usage: ./scripts/e2e.ps1 <ui|api> <command> [-Headed]" -ForegroundColor White
    Write-Host ""
    Write-Host "  UI E2E (Reqnroll + Playwright, real browser):" -ForegroundColor DarkGray
    Write-Host "    ui run       Run all UI scenarios (B2B + Customer)"
    Write-Host "    ui b2b       Run B2B UI scenarios only"
    Write-Host "    ui customer  Run Customer UI scenarios only"
    Write-Host "    ui 3ds       Run 3DS scenarios (B2B only)"
    Write-Host "    ui trace     Open latest Playwright trace"
    Write-Host ""
    Write-Host "  API E2E (xUnit, full Aspire stack, no browser):" -ForegroundColor DarkGray
    Write-Host "    api run       Run all API E2E (B2B + Customer); non-zero exit on any failure"
    Write-Host "    api b2b       Run B2B API E2E only"
    Write-Host "    api customer  Run Customer API E2E only"
    Write-Host ""
    Write-Host "  A bare command with no domain (e.g. './scripts/e2e.ps1 run') is treated as 'ui'." -ForegroundColor DarkGray
    Write-Host ""
}

function Invoke-UiCommand([string]$cmd) {
    if ($cmd -in @('run', 'b2b', 'customer', '3ds')) {
        Assert-DockerHealthy
        Assert-PinnedImagesPullable
        Assert-HostCapacity
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Assert-SpaDependencies
        Assert-SpaPackagesBuilt
        Assert-PlaywrightBrowsers
    }
    switch ($cmd) {
        "run" {
            $b2b  = Invoke-PrettyTest 'B2B'      "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
            $cust = Invoke-PrettyTest 'Customer' "$customerUi/Concertable.Customer.E2ETests.Ui.csproj"
            Complete-TestRun @($b2b, $cust)
        }
        "b2b" {
            $b2b = Invoke-PrettyTest 'B2B' "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
            Complete-TestRun @($b2b)
        }
        "customer" {
            $cust = Invoke-PrettyTest 'Customer' "$customerUi/Concertable.Customer.E2ETests.Ui.csproj"
            Complete-TestRun @($cust)
        }
        "3ds" {
            $r = Invoke-PrettyTest '3DS' "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj" @('--filter', 'DisplayName~3DS')
            Complete-TestRun @($r)
        }
        "trace" { & (Join-Path $repoRoot "api/Concertable.Shared/tests/Concertable.Testing.E2E/ui-trace.ps1") }
        default { Show-Usage }
    }
}

function Invoke-ApiCommand([string]$cmd) {
    $settings = @('--settings', $runsettings)
    if ($cmd -in @('run', 'b2b', 'customer')) {
        Assert-DockerHealthy
        Assert-PinnedImagesPullable
        Assert-HostCapacity
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    switch ($cmd) {
        "run" {
            $b2b  = Invoke-PrettyTest 'B2B API'      "$b2bApi/Concertable.B2B.E2ETests.csproj"           $settings 'api-tests.last.log'
            $cust = Invoke-PrettyTest 'Customer API' "$customerApi/Concertable.Customer.E2ETests.csproj" $settings 'api-tests.last.log'
            Complete-TestRun @($b2b, $cust)
        }
        "b2b" {
            $b2b = Invoke-PrettyTest 'B2B API' "$b2bApi/Concertable.B2B.E2ETests.csproj" $settings 'api-tests.last.log'
            Complete-TestRun @($b2b)
        }
        "customer" {
            $cust = Invoke-PrettyTest 'Customer API' "$customerApi/Concertable.Customer.E2ETests.csproj" $settings 'api-tests.last.log'
            Complete-TestRun @($cust)
        }
        default { Show-Usage }
    }
}

if ($domain) { $domain = $domain.ToLowerInvariant() } else { $domain = '' }
if ($cmd)    { $cmd    = $cmd.ToLowerInvariant() }    else { $cmd    = '' }

if (-not $domain -or $domain -in @('list', 'help', '-h', '--help', '/?')) {
    Show-Usage
    Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
    return
}

# Back-compat: a bare command with no ui/api domain is treated as a UI command.
if ($domain -notin @('ui', 'api')) {
    $cmd = $domain
    $domain = 'ui'
}

switch ($domain) {
    'ui'  { Invoke-UiCommand $cmd }
    'api' { Invoke-ApiCommand $cmd }
}

Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
