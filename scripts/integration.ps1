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

$authProjects = @(
    "api/Concertable.Auth/tests/Concertable.Auth.IntegrationTests/Concertable.Auth.IntegrationTests.csproj"
)
$b2bProjects = @(
    "api/Concertable.B2B/src/Modules/Application/Tests/Concertable.B2B.Application.IntegrationTests/Concertable.B2B.Application.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Artist/Tests/Concertable.B2B.Artist.IntegrationTests/Concertable.B2B.Artist.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Booking/Tests/Concertable.B2B.Booking.IntegrationTests/Concertable.B2B.Booking.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Concert/Tests/Concertable.B2B.Concert.IntegrationTests/Concertable.B2B.Concert.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Conversations/Tests/Concertable.B2B.Conversations.IntegrationTests/Concertable.B2B.Conversations.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Dashboard/Tests/Concertable.B2B.Dashboard.IntegrationTests/Concertable.B2B.Dashboard.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Deal/Tests/Concertable.B2B.Deal.IntegrationTests/Concertable.B2B.Deal.IntegrationTests.csproj",
    "api/Concertable.B2B/tests/Concertable.B2B.Lifecycle.IntegrationTests/Concertable.B2B.Lifecycle.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Opportunity/Tests/Concertable.B2B.Opportunity.IntegrationTests/Concertable.B2B.Opportunity.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Tenant/Tests/Concertable.B2B.Tenant.IntegrationTests/Concertable.B2B.Tenant.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/User/Tests/Concertable.B2B.User.IntegrationTests/Concertable.B2B.User.IntegrationTests.csproj",
    "api/Concertable.B2B/src/Modules/Venue/Tests/Concertable.B2B.Venue.IntegrationTests/Concertable.B2B.Venue.IntegrationTests.csproj"
)
$customerProjects = @(
    "api/Concertable.Customer/src/Modules/Artist/Tests/Concertable.Customer.Artist.IntegrationTests/Concertable.Customer.Artist.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/Concert/Tests/Concertable.Customer.Concert.IntegrationTests/Concertable.Customer.Concert.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/Preference/Tests/Concertable.Customer.Preference.IntegrationTests/Concertable.Customer.Preference.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/Review/Tests/Concertable.Customer.Review.IntegrationTests/Concertable.Customer.Review.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/Ticket/Tests/Concertable.Customer.Ticket.IntegrationTests/Concertable.Customer.Ticket.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/User/Tests/Concertable.Customer.User.IntegrationTests/Concertable.Customer.User.IntegrationTests.csproj",
    "api/Concertable.Customer/src/Modules/Venue/Tests/Concertable.Customer.Venue.IntegrationTests/Concertable.Customer.Venue.IntegrationTests.csproj"
)
$searchProjects = @(
    "api/Concertable.Search/tests/Concertable.Search.IntegrationTests/Concertable.Search.IntegrationTests.csproj"
)
$paymentProjects = @(
    "api/Concertable.Payment/tests/Concertable.Payment.IntegrationTests/Concertable.Payment.IntegrationTests.csproj"
)

$allProjects = $authProjects + $b2bProjects + $customerProjects + $paymentProjects + $searchProjects

function Invoke-IntegrationProject([string]$csproj, [string[]]$extra) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
    $logPath = Join-Path (Split-Path $csproj -Parent) 'integration-tests.last.log'
    Write-Host ""
    Write-Host "=== $name ===" -ForegroundColor Cyan
    $cmdArgs = @($csproj, '--logger', 'console;verbosity=normal') + $extra
    & $localPlatform test @cmdArgs 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    return $LASTEXITCODE
}

function Invoke-Projects([string]$label, [string[]]$projects, [string[]]$extra) {
    Write-Host ""
    Write-Host ">>> $label ($($projects.Count) project$(if ($projects.Count -ne 1) { 's' }))" -ForegroundColor Yellow
    $failures = @()
    foreach ($p in $projects) {
        $code = Invoke-IntegrationProject $p $extra
        if ($code -ne 0) { $failures += $p }
    }
    Write-Host ""
    if ($failures.Count -eq 0) {
        Write-Host "$label OK: $($projects.Count)/$($projects.Count) projects passed." -ForegroundColor Green
        return 0
    }
    Write-Host "$label FAILED: $($failures.Count) project(s) had test failures:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $([System.IO.Path]::GetFileNameWithoutExtension($_))" -ForegroundColor Red }
    return 1
}

function Find-ByModule([string]$module) {
    $needle = ".$module.IntegrationTests."
    return $allProjects | Where-Object { $_ -like "*$needle*" }
}

switch ($cmd) {
    "run" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'All integration tests' $allProjects $rest
        exit $exit
    }
    "auth" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Auth integration tests' $authProjects $rest
        exit $exit
    }
    "b2b" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'B2B integration tests' $b2bProjects $rest
        exit $exit
    }
    "customer" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Customer integration tests' $customerProjects $rest
        exit $exit
    }
    "search" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Search integration tests' $searchProjects $rest
        exit $exit
    }
    "payment" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Payment integration tests' $paymentProjects $rest
        exit $exit
    }
    "list" {
        Write-Host ""
        Write-Host "Auth:" -ForegroundColor Yellow
        $authProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "B2B:" -ForegroundColor Yellow
        $b2bProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Customer:" -ForegroundColor Yellow
        $customerProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Payment:" -ForegroundColor Yellow
        $paymentProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Search:" -ForegroundColor Yellow
        $searchProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
    }
    default {
        if ($cmd) {
            $matches = Find-ByModule $cmd
            if ($matches.Count -gt 0) {
                & $localPlatform prepare
                if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                $exit = Invoke-Projects "Module: $cmd" $matches $rest
                exit $exit
            }
            Write-Host "Unknown command or module: '$cmd'" -ForegroundColor Red
            Write-Host ""
        }
        Write-Host "  Usage: ./scripts/integration.ps1 <command> [-- <extra dotnet test args>]" -ForegroundColor White
        Write-Host ""
        Write-Host "  Commands:" -ForegroundColor DarkGray
        Write-Host "    run        Run all integration tests (Auth + B2B + Customer + Payment + Search)"
        Write-Host "    auth       Run Auth integration tests only"
        Write-Host "    b2b        Run B2B integration tests only"
        Write-Host "    customer   Run Customer integration tests only"
        Write-Host "    payment    Run Payment integration tests only"
        Write-Host "    search     Run Search integration tests only"
        Write-Host "    <module>   Run a specific module (e.g. artist, concert, venue, user, tenant, review, ticket)"
        Write-Host "    list       List all integration test projects"
        Write-Host ""
        Write-Host "  Examples:" -ForegroundColor DarkGray
        Write-Host "    ./scripts/integration.ps1 run"
        Write-Host "    ./scripts/integration.ps1 b2b"
        Write-Host "    ./scripts/integration.ps1 concert"
        Write-Host "    ./scripts/integration.ps1 artist --filter ""FullyQualifiedName~ArtistApiTests.Create"""
        Write-Host ""
    }
}
