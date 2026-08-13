param(
    [Parameter(Position = 0)]
    [string]$cmd,
    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$rest
)

$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot
[Environment]::CurrentDirectory = $repoRoot
$localPlatform = Join-Path $PSScriptRoot 'local-platform.ps1'
$powerShell = (Get-Process -Id $PID).Path

$trxNs = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

function Get-TestProjects([string]$pattern, [string]$filter) {
    $apiRoot = Join-Path $repoRoot 'api'
    Get-ChildItem -Path $apiRoot -Recurse -Filter '*.csproj' -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Where-Object { $_.BaseName -match $pattern } |
        Where-Object { -not $filter -or $_.Name -match [regex]::Escape($filter) } |
        Where-Object { Select-String -Path $_.FullName -Pattern 'Microsoft\.NET\.Test\.Sdk' -Quiet } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
}

function Short-Name([string]$testName) {
    $parts = $testName.Split('.')
    if ($parts.Count -ge 2) { return ($parts[-2..-1] -join '.') }
    return $testName
}

function Clip([string]$text, [int]$max = 160) {
    if ($null -eq $text) { return '' }
    $t = $text.Trim()
    if ($t.Length -gt $max) { return $t.Substring(0, $max - 3) + '...' }
    return $t
}

function Invoke-Project([string]$csproj) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
    $dir = Split-Path $csproj -Parent
    $log = Join-Path $dir 'test.last.log'
    $errLog = "$log.err"
    $resultsDir = Join-Path $dir 'TestResults'
    $trx = Join-Path $resultsDir 'run.trx'
    if (Test-Path $trx) { Remove-Item $trx -Force }

    Write-Host ("  {0,-54}" -f $name) -NoNewline

    $testArgs = @(
        $localPlatform, 'test', $csproj, '--nologo', '--verbosity', 'quiet',
        '--results-directory', $resultsDir,
        '--logger', 'trx;LogFileName=run.trx'
    )
    $proc = Start-Process -FilePath $powerShell -ArgumentList $testArgs -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $log -RedirectStandardError $errLog
    if ((Test-Path $errLog) -and (Get-Item $errLog).Length -gt 0) { Get-Content $errLog | Add-Content $log }
    Remove-Item $errLog -ErrorAction SilentlyContinue

    if (-not (Test-Path $trx)) {
        Write-Host "  FAILED (no results)" -ForegroundColor Red
        $diag = Select-String -Path $log -Pattern '(^(fail|crit):|: error |\w+Exception:)' |
            ForEach-Object { Clip $_.Line } | Select-Object -Unique -First 8
        if ($diag) { $diag | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkRed } }
        Write-Host ("      full log: {0}" -f $log) -ForegroundColor DarkGray
        return [pscustomobject]@{ Name = $name; Passed = 0; Failed = 0; Built = $false }
    }

    [xml]$xml = Get-Content $trx
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('t', $trxNs)
    $counters = $xml.SelectSingleNode('//t:ResultSummary/t:Counters', $ns)
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed

    if ($failed -eq 0) {
        Write-Host ("  PASS   {0,4} passed" -f $passed) -ForegroundColor Green
    } else {
        Write-Host ("  FAIL   {0} of {1} failed" -f $failed, ($passed + $failed)) -ForegroundColor Red
        $failedNodes = @($xml.SelectNodes("//t:UnitTestResult[@outcome='Failed']", $ns))
        foreach ($n in ($failedNodes | Select-Object -First 10)) {
            Write-Host ("      - {0}" -f (Short-Name $n.testName)) -ForegroundColor Red
            $msg = $n.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)
            if ($msg -and $msg.InnerText) {
                $line = ($msg.InnerText -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
                Write-Host ("        {0}" -f (Clip $line)) -ForegroundColor DarkGray
            }
        }
        if ($failedNodes.Count -gt 10) {
            Write-Host ("      ... and {0} more (full log: {1})" -f ($failedNodes.Count - 10), $log) -ForegroundColor DarkGray
        }
    }
    return [pscustomobject]@{ Name = $name; Passed = $passed; Failed = $failed; Built = $true }
}

function Run-Suite([string]$title, [string]$pattern, [string]$filter) {
    $projects = Get-TestProjects $pattern $filter
    Write-Host ""
    Write-Host ("== {0} ({1} project{2}) ==" -f $title, $projects.Count, $(if ($projects.Count -ne 1) { 's' })) -ForegroundColor Cyan
    if ($projects.Count -eq 0) {
        Write-Host "  No matching projects." -ForegroundColor DarkGray
        return [pscustomobject]@{ Suite = $title; Passed = $null; Failed = $null; Ok = $true }
    }
    $passed = 0; $failed = 0; $broken = 0
    foreach ($p in $projects) {
        $r = Invoke-Project $p
        $passed += $r.Passed
        $failed += $r.Failed
        if (-not $r.Built) { $broken++ }
    }
    return [pscustomobject]@{ Suite = $title; Passed = $passed; Failed = $failed; Ok = ($failed -eq 0 -and $broken -eq 0) }
}

function Show-Summary([object[]]$rows) {
    Write-Host ""
    Write-Host "  Summary" -ForegroundColor Cyan
    Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f 'Suite', 'Passed', 'Failed', 'Result') -ForegroundColor Gray
    Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f '------', '------', '------', '------') -ForegroundColor Gray
    foreach ($r in $rows) {
        $color = if ($r.Ok) { 'Green' } else { 'Red' }
        $result = if ($r.Ok) { 'PASS' } else { 'FAIL' }
        $p = if ($null -eq $r.Passed) { '-' } else { $r.Passed }
        $f = if ($null -eq $r.Failed) { '-' } else { $r.Failed }
        Write-Host ("  {0,-14}{1,8}{2,8}{3,8}" -f $r.Suite, $p, $f, $result) -ForegroundColor $color
    }
    Write-Host ""
}

function Show-Usage {
    Write-Host ""
    Write-Host "  Usage: ./scripts/test.ps1 <command> [filter]" -ForegroundColor White
    Write-Host ""
    Write-Host "  Commands:" -ForegroundColor DarkGray
    Write-Host "    all           Run unit + integration + e2e, then a combined PASS/FAIL summary"
    Write-Host "    unit [name]   Run unit tests        (optionally filter projects by name, e.g. b2b, concert)"
    Write-Host "    integration   Run integration tests (optionally filter projects by name)"
    Write-Host "    e2e [name]    Run E2E tests (API+UI) (headless; full logs to test.last.log)"
    Write-Host "    list          Show this help"
    Write-Host ""
    Write-Host "  Output is quiet: each project prints one line; full logs go to test.last.log" -ForegroundColor DarkGray
    Write-Host "  next to each project, and failures expand inline. For headed/trace E2E use ./scripts/e2e.ps1." -ForegroundColor DarkGray
    Write-Host ""
}

switch ($cmd) {
    "all" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $unit = Run-Suite 'Unit'        '\.UnitTests$'        $null
        $intg = Run-Suite 'Integration' '\.IntegrationTests$' $null
        $env:HEADLESS = 'true'
        $e2e = Run-Suite 'E2E' '\.E2ETests(\.Ui)?$' $null
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        Show-Summary @($unit, $intg, $e2e)
        if (-not ($unit.Ok -and $intg.Ok -and $e2e.Ok)) {
            Write-Host "  TESTS FAILED -- at least one suite did not pass." -ForegroundColor Red
            exit 1
        }
        Write-Host "  ALL TESTS PASSED." -ForegroundColor Green
        exit 0
    }
    "unit" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $r = Run-Suite 'Unit' '\.UnitTests$' ($rest | Select-Object -First 1)
        Show-Summary @($r)
        exit $(if ($r.Ok) { 0 } else { 1 })
    }
    "integration" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $r = Run-Suite 'Integration' '\.IntegrationTests$' ($rest | Select-Object -First 1)
        Show-Summary @($r)
        exit $(if ($r.Ok) { 0 } else { 1 })
    }
    "e2e" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $env:HEADLESS = 'true'
        $r = Run-Suite 'E2E' '\.E2ETests(\.Ui)?$' ($rest | Select-Object -First 1)
        Remove-Item Env:\HEADLESS -ErrorAction SilentlyContinue
        Show-Summary @($r)
        exit $(if ($r.Ok) { 0 } else { 1 })
    }
    { $_ -in "list", "help" } { Show-Usage }
    default { Show-Usage }
}
