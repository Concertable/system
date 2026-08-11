$all = Get-ChildItem -Recurse -Filter "trace-*.zip" $PSScriptRoot

if (-not $all) { Write-Error "No trace files found under api/Concertable.Shared/tests/Concertable.Testing.E2E."; exit 1 }

$latest = $all | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "Found $($all.Count) trace(s). Opening newest:" -ForegroundColor DarkGray
Write-Host "  $($latest.FullName)" -ForegroundColor Cyan
Write-Host "  Last modified: $($latest.LastWriteTime)" -ForegroundColor DarkGray
npx playwright show-trace $latest.FullName
