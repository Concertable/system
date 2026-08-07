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

$b2bUi      = Join-Path $repoRoot "api/Concertable.B2B/tests/E2ETests/Concertable.B2B.E2ETests.Ui"
$customerUi = Join-Path $repoRoot "api/Concertable.Customer/tests/E2ETests/Concertable.Customer.E2ETests.Ui"
$b2bApi      = Join-Path $repoRoot "api/Concertable.B2B/tests/E2ETests/Concertable.B2B.E2ETests"
$customerApi = Join-Path $repoRoot "api/Concertable.Customer/tests/E2ETests/Concertable.Customer.E2ETests"
$runsettings = Join-Path $repoRoot "api/Concertable.runsettings"
$baselineMd = Join-Path $repoRoot "api/Concertable.Shared/tests/Concertable.E2ETests/E2E_BASELINE.md"

$quiet = @('--nologo', '--verbosity', 'quiet')

if (-not $Headed) { $env:HEADLESS = "true" }

function Get-BaselinePassing([string]$suite) {
    $md = Get-Content -Raw $baselineMd
    $marker = '<!-- BASELINE-DATA-START -->'
    $markerIdx = $md.IndexOf($marker)
    if ($markerIdx -lt 0) {
        throw "BASELINE FORMAT ERROR: missing '$marker' sentinel in $baselineMd. The regress parser only reads content below this marker."
    }
    $data = $md.Substring($markerIdx + $marker.Length)

    $pattern = "(?ms)### $suite passing \((\d+)\)\s*``````text\s*(.+?)\s*``````"
    $match = [regex]::Match($data, $pattern)
    if (-not $match.Success) {
        throw "BASELINE FORMAT ERROR: couldn't find '### $suite passing (N)' section followed by a ``````text block in $baselineMd. See editing rules at the top of that file."
    }

    $declared = [int]$match.Groups[1].Value
    $scenarios = $match.Groups[2].Value.Split([string[]]@("`r`n","`n"), [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() } | Where-Object { $_ }

    if ($scenarios.Count -ne $declared) {
        throw "BASELINE FORMAT ERROR: '### $suite passing ($declared)' but the fenced block has $($scenarios.Count) scenarios. Update either the (N) in the heading or the line count."
    }

    foreach ($s in $scenarios) {
        if ($s -match '^[-*•]\s' -or $s -match '^["''`]') {
            throw "BASELINE FORMAT ERROR: '$suite passing' contains a bulleted/quoted scenario: '$s'. Plain text only -- see editing rules."
        }
    }
    return $scenarios
}

function Invoke-Regress([string]$suite, [string]$csproj) {
    Write-Host ""
    Write-Host "=== Regress: $suite ===" -ForegroundColor Cyan

    $expected = Get-BaselinePassing $suite
    Write-Host "Baseline says $($expected.Count) $suite scenarios must pass." -ForegroundColor Gray
    $filter = ($expected | ForEach-Object { "DisplayName=$_" }) -join '|'

    # Preflight: confirm each baseline scenario resolves to a real test
    $list = dotnet test $csproj --list-tests --filter $filter @quiet 2>&1
    $discovered = $list | Where-Object { $_ -match '^\s{4}\S' } | ForEach-Object { $_.Trim() }
    if ($discovered.Count -ne $expected.Count) {
        $missing = $expected | Where-Object { $discovered -notcontains $_ }
        Write-Host "BASELINE DRIFT: dotnet test --list-tests discovered $($discovered.Count) of $($expected.Count) expected $suite scenarios." -ForegroundColor Red
        Write-Host "Missing (renamed in .feature, or typo'd in baseline?):" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        throw "Baseline drift -- fix $baselineMd or the .feature file."
    }
    Write-Host "Preflight OK: all $($expected.Count) baseline scenarios resolve to real tests." -ForegroundColor Gray

    # Run them
    $logPath = (Join-Path (Split-Path $csproj -Parent) 'regress.last.log')
    dotnet test $csproj --filter $filter @quiet --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath $logPath | Out-Host

    # Parse pass/fail totals (Tee writes UTF-16 on Windows)
    $logBytes = [System.IO.File]::ReadAllBytes($logPath)
    $logText = if ($logBytes.Length -ge 2 -and $logBytes[0] -eq 0xFF -and $logBytes[1] -eq 0xFE) {
        [System.Text.Encoding]::Unicode.GetString($logBytes)
    } else {
        [System.Text.Encoding]::UTF8.GetString($logBytes)
    }
    # dotnet test prints "Failed: N" only when N > 0
    $passedMatch = [regex]::Match($logText, '(?m)^\s*Passed:\s*(\d+)')
    $failedMatch = [regex]::Match($logText, '(?m)^\s*Failed:\s*(\d+)')
    if (-not $passedMatch.Success) {
        throw "Couldn't parse 'Passed: N' from $logPath -- did the run complete?"
    }
    $passed = [int]$passedMatch.Groups[1].Value
    $failed = if ($failedMatch.Success) { [int]$failedMatch.Groups[1].Value } else { 0 }

    if ($passed -eq $expected.Count -and $failed -eq 0) {
        Write-Host "  $suite OK: $passed/$($expected.Count) passed." -ForegroundColor Green
        return $true
    }

    $failedNames = [regex]::Matches($logText, '(?m)^\s+Failed\s+(.+?)\s+\[') | ForEach-Object { $_.Groups[1].Value }
    Write-Host "  ${suite}: $failed scenario(s) failed on the first pass:" -ForegroundColor Yellow
    $failedNames | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }

    # Retry ONLY the failures once, together on a single freshly-booted stack (separate from the full run's).
    # The UI suite is flaky on a loaded
    # Docker host -- the ASB emulator drops connections under the sustained load of the full run, tripping
    # different bus-heavy scenarios each time -- so a scenario that fails in the full run but passes alone
    # is an environment blip, not a regression. Re-running just the failures is far cheaper than the whole
    # baseline and is what makes a green verdict reliable on a flaky host. A scenario that fails BOTH times
    # is a real regression.
    Write-Host "  Retrying $($failedNames.Count) failed scenario(s) in isolation (flaky-stack guard)..." -ForegroundColor Cyan
    $retryFilter = ($failedNames | ForEach-Object { "DisplayName=$_" }) -join '|'
    $retryLog = (Join-Path (Split-Path $csproj -Parent) 'regress.retry.last.log')
    dotnet test $csproj --filter $retryFilter @quiet --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath $retryLog | Out-Host

    $retryBytes = [System.IO.File]::ReadAllBytes($retryLog)
    $retryText = if ($retryBytes.Length -ge 2 -and $retryBytes[0] -eq 0xFF -and $retryBytes[1] -eq 0xFE) {
        [System.Text.Encoding]::Unicode.GetString($retryBytes)
    } else {
        [System.Text.Encoding]::UTF8.GetString($retryBytes)
    }
    $retryPassedMatch = [regex]::Match($retryText, '(?m)^\s*Passed:\s*(\d+)')
    $retryFailedMatch = [regex]::Match($retryText, '(?m)^\s*Failed:\s*(\d+)')
    $retryPassed = if ($retryPassedMatch.Success) { [int]$retryPassedMatch.Groups[1].Value } else { 0 }
    $retryFailed = if ($retryFailedMatch.Success) { [int]$retryFailedMatch.Groups[1].Value } else { 0 }

    if ($retryFailed -eq 0 -and $retryPassed -eq $failedNames.Count) {
        Write-Host "  $suite OK: $passed/$($expected.Count) on first pass; the $($failedNames.Count) flaked scenario(s) passed on isolated retry (environment blip, not a regression)." -ForegroundColor Green
        return $true
    }

    $stillFailing = [regex]::Matches($retryText, '(?m)^\s+Failed\s+(.+?)\s+\[') | ForEach-Object { $_.Groups[1].Value }
    Write-Host "  $suite REGRESSED: $retryFailed scenario(s) failed again on isolated retry (real regression):" -ForegroundColor Red
    $stillFailing | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    return $false
}

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
    dotnet test @testArgs *> $log

    if (-not (Test-Path $trx)) {
        Write-Host "  No results -- build or run failed. Full log: $log" -ForegroundColor Red
        return [pscustomobject]@{ Suite = $suite; Passed = 0; Failed = 0; Total = 0 }
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
    return [pscustomobject]@{ Suite = $suite; Passed = $passed; Failed = $failed; Total = ($passed + $failed) }
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

function Show-Usage {
    Write-Host ""
    Write-Host "  Usage: ./scripts/e2e.ps1 <ui|api> <command> [-Headed]" -ForegroundColor White
    Write-Host ""
    Write-Host "  UI E2E (Reqnroll + Playwright, real browser):" -ForegroundColor DarkGray
    Write-Host "    ui run       Run all UI scenarios (B2B + Customer)"
    Write-Host "    ui regress   Run only baseline-passing scenarios; fail on any regression (~3 min)"
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
    if ($cmd -in @('run', 'regress', 'b2b', 'customer', '3ds')) { Assert-DockerHealthy; Assert-HostCapacity }
    switch ($cmd) {
        "run" {
            $b2b  = Invoke-PrettyTest 'B2B'      "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
            $cust = Invoke-PrettyTest 'Customer' "$customerUi/Concertable.Customer.E2ETests.Ui.csproj"
            Show-Summary @($b2b, $cust)
        }
        "b2b" {
            $b2b = Invoke-PrettyTest 'B2B' "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
            Show-Summary @($b2b)
        }
        "customer" {
            $cust = Invoke-PrettyTest 'Customer' "$customerUi/Concertable.Customer.E2ETests.Ui.csproj"
            Show-Summary @($cust)
        }
        "regress" {
            $b2bOk  = Invoke-Regress 'B2B'      "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj"
            $custOk = Invoke-Regress 'Customer' "$customerUi/Concertable.Customer.E2ETests.Ui.csproj"
            Write-Host ""
            if ($b2bOk -and $custOk) {
                Write-Host "REGRESS PASSED -- every baseline-passing scenario still passes." -ForegroundColor Green
                exit 0
            } else {
                Write-Host "REGRESS FAILED -- at least one baseline-passing scenario regressed." -ForegroundColor Red
                exit 1
            }
        }
        "3ds" {
            $r = Invoke-PrettyTest '3DS' "$b2bUi/Concertable.B2B.E2ETests.Ui.csproj" @('--filter', 'DisplayName~3DS')
            Show-Summary @($r)
        }
        "trace" { & (Join-Path $repoRoot "api/Concertable.Shared/tests/Concertable.E2ETests/ui-trace.ps1") }
        default { Show-Usage }
    }
}

function Invoke-ApiCommand([string]$cmd) {
    $settings = @('--settings', $runsettings)
    if ($cmd -in @('run', 'b2b', 'customer')) { Assert-DockerHealthy; Assert-HostCapacity }
    switch ($cmd) {
        "run" {
            $b2b  = Invoke-PrettyTest 'B2B API'      "$b2bApi/Concertable.B2B.E2ETests.csproj"           $settings 'api-tests.last.log'
            $cust = Invoke-PrettyTest 'Customer API' "$customerApi/Concertable.Customer.E2ETests.csproj" $settings 'api-tests.last.log'
            $summaries = @($b2b, $cust)
            Show-Summary $summaries
            if ((($summaries | Measure-Object Failed -Sum).Sum) -gt 0) { exit 1 } else { exit 0 }
        }
        "b2b" {
            $b2b = Invoke-PrettyTest 'B2B API' "$b2bApi/Concertable.B2B.E2ETests.csproj" $settings 'api-tests.last.log'
            Show-Summary @($b2b)
            if ($b2b.Failed -gt 0) { exit 1 } else { exit 0 }
        }
        "customer" {
            $cust = Invoke-PrettyTest 'Customer API' "$customerApi/Concertable.Customer.E2ETests.csproj" $settings 'api-tests.last.log'
            Show-Summary @($cust)
            if ($cust.Failed -gt 0) { exit 1 } else { exit 0 }
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
