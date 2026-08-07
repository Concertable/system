<#
host-capacity.ps1 — host-contention pre-flight for the E2E suites.

WHY THIS EXISTS (do not weaken it):
An E2E boot is one of the heaviest things this repo runs: ~15 .NET services
(B2B/Customer Web + Payment + Auth + Search + workers) + ASB emulator + SQL/Azurite
containers + stripe-cli + two Vite SPAs, all coming up at once, after which the
fixture waits a fixed window for every /health to go green.

If the host is ALREADY saturated when that starts — a full-solution `Concertable.slnx`
build (dozens of MSBuild nodes) or a second E2E run from another worktree/agent — the
services start too slowly to make the readiness window, or get OOM-killed
(`FailedToStart`). The fixture then dies at fixture startup after 6-13 min with ZERO
scenarios run, and a DIFFERENT service times out each time (whoever loses the CPU/RAM
race). That looks like a random flake but is pure contention. docker-health.ps1 passes
throughout (it only probes a fresh throwaway container), so nothing surfaces the cause.

This gate catches it BEFORE the doomed boot. It does NOT pad any timeout — it refuses
(or waits) when a competing full-solution build or another E2E run is present, so an
E2E stack is never launched into a host that cannot start it.

Exit 0 = host has capacity (safe to boot E2E). Exit 1 = contended (do not boot).

  -WaitSeconds N : instead of failing immediately, poll up to N seconds for the host to
                   clear, then exit 0; exit 1 only if still contended at the cap.
#>
param([int]$WaitSeconds = 0)

function Get-Contenders {
    $procs = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue)
    [pscustomobject]@{
        # Full-solution build/test — the exact thing that starves an E2E boot.
        Builds = @($procs | Where-Object { $_.CommandLine -match 'Concertable\.slnx' })
        # Another E2E run in flight (its `dotnet test` parent or testhost child).
        E2E    = @($procs | Where-Object { $_.CommandLine -match 'E2ETests[\w\.]*\.(dll|csproj)' })
    }
}

function Format-Free { [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1) }

function Show-Contenders($c) {
    foreach ($b in $c.Builds) { Write-Host ("  competing full-solution build   PID {0}" -f $b.ProcessId) -ForegroundColor Yellow }
    foreach ($e in $c.E2E)    { Write-Host ("  another E2E run in flight        PID {0}" -f $e.ProcessId) -ForegroundColor Yellow }
}

$c = Get-Contenders
if (($c.Builds.Count + $c.E2E.Count) -eq 0) {
    Write-Host "Host capacity OK (no competing slnx build or E2E run; $(Format-Free)GB RAM free)." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "HOST CONTENDED -- an E2E boot will likely time out at fixture startup. Free RAM: $(Format-Free)GB." -ForegroundColor Red
Show-Contenders $c

if ($WaitSeconds -le 0) {
    Write-Host "  Let the build(s)/other E2E run finish (or stop them), then re-run." -ForegroundColor Yellow
    Write-Host "  This is host contention, not a Docker or app bug -- do NOT raise E2E timeouts to mask it." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "  Waiting up to ${WaitSeconds}s for the host to clear..." -ForegroundColor Cyan
$elapsed = 0
while ($elapsed -lt $WaitSeconds) {
    Start-Sleep -Seconds 15
    $elapsed += 15
    $c = Get-Contenders
    if (($c.Builds.Count + $c.E2E.Count) -eq 0) {
        Write-Host "Host cleared after ~${elapsed}s -- proceeding ($(Format-Free)GB RAM free)." -ForegroundColor Green
        exit 0
    }
    Write-Host ("  still contended at ~${elapsed}s: {0} build(s), {1} E2E run(s)" -f $c.Builds.Count, $c.E2E.Count) -ForegroundColor Gray
}
Write-Host "Still contended after ${WaitSeconds}s -- not booting E2E. Stop the competing work and retry." -ForegroundColor Red
exit 1
