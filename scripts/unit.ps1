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
    "api/Concertable.Auth/tests/Concertable.Auth.UnitTests/Concertable.Auth.UnitTests.csproj"
)
$dataAccessProjects = @(
    "api/Concertable.DataAccess/Tests/Concertable.DataAccess.UnitTests/Concertable.DataAccess.UnitTests.csproj"
)
$b2bProjects = @(
    "api/Concertable.B2B/src/Concertable.B2B.DataAccess/Tests/Concertable.B2B.DataAccess.UnitTests/Concertable.B2B.DataAccess.UnitTests.csproj",
    "api/Concertable.B2B/src/Modules/Concert/Tests/Concertable.B2B.Concert.UnitTests/Concertable.B2B.Concert.UnitTests.csproj",
    "api/Concertable.B2B/src/Modules/Conversations/Tests/Concertable.B2B.Conversations.UnitTests/Concertable.B2B.Conversations.UnitTests.csproj",
    "api/Concertable.B2B/src/Modules/Deal/Tests/Concertable.B2B.Deal.UnitTests/Concertable.B2B.Deal.UnitTests.csproj",
    "api/Concertable.B2B/src/Modules/Tenant/Tests/Concertable.B2B.Tenant.UnitTests/Concertable.B2B.Tenant.UnitTests.csproj",
    "api/Concertable.B2B/tests/Concertable.B2B.Workers.UnitTests/Concertable.B2B.Workers.UnitTests.csproj"
)
$customerProjects = @(
    "api/Concertable.Customer/src/Modules/Concert/Tests/Concertable.Customer.Concert.UnitTests/Concertable.Customer.Concert.UnitTests.csproj",
    "api/Concertable.Customer/src/Modules/Review/Tests/Concertable.Customer.Review.UnitTests/Concertable.Customer.Review.UnitTests.csproj",
    "api/Concertable.Customer/src/Modules/Ticket/Tests/Concertable.Customer.Ticket.UnitTests/Concertable.Customer.Ticket.UnitTests.csproj",
    "api/Concertable.Customer/src/Modules/User/Tests/Concertable.Customer.User.UnitTests/Concertable.Customer.User.UnitTests.csproj"
)
$searchProjects = @(
    "api/Concertable.Search/tests/Concertable.Search.UnitTests/Concertable.Search.UnitTests.csproj"
)
$paymentProjects = @(
    "api/Concertable.Payment/tests/Concertable.Payment.UnitTests/Concertable.Payment.UnitTests.csproj",
    "api/Concertable.Payment/tests/E2ETests/Concertable.Payment.E2ETests.Helpers.UnitTests/Concertable.Payment.E2ETests.Helpers.UnitTests.csproj"
)
$sharedProjects = @(
    "api/Concertable.Shared/tests/Concertable.Grpc.UnitTests/Concertable.Grpc.UnitTests.csproj",
    "api/Concertable.Shared/tests/Concertable.Contracts.UnitTests/Concertable.Contracts.UnitTests.csproj",
    "api/Concertable.Shared/tests/Concertable.Kernel.UnitTests/Concertable.Kernel.UnitTests.csproj",
    "api/Concertable.Shared/tests/Concertable.Shared.Api.UnitTests/Concertable.Shared.Api.UnitTests.csproj",
    "api/Concertable.Messaging/Tests/Concertable.Messaging.UnitTests/Concertable.Messaging.UnitTests.csproj",
    "api/Concertable.Messaging/Tests/Concertable.Messaging.AzureServiceBus.UnitTests/Concertable.Messaging.AzureServiceBus.UnitTests.csproj"
)

$allProjects = $authProjects + $dataAccessProjects + $b2bProjects + $customerProjects + $searchProjects + $paymentProjects + $sharedProjects

function Invoke-UnitProject([string]$csproj, [string[]]$extra) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
    $logPath = Join-Path (Split-Path $csproj -Parent) 'unit-tests.last.log'
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
        $code = Invoke-UnitProject $p $extra
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
    $needle = ".$module.UnitTests."
    return $allProjects | Where-Object { $_ -like "*$needle*" }
}

switch ($cmd) {
    "run" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'All unit tests' $allProjects $rest
        exit $exit
    }
    "auth" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Auth unit tests' $authProjects $rest
        exit $exit
    }
    "b2b" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'B2B unit tests' $b2bProjects $rest
        exit $exit
    }
    "customer" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Customer unit tests' $customerProjects $rest
        exit $exit
    }
    "search" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Search unit tests' $searchProjects $rest
        exit $exit
    }
    "payment" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Payment unit tests' $paymentProjects $rest
        exit $exit
    }
    "shared" {
        & $localPlatform prepare
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $exit = Invoke-Projects 'Shared unit tests' $sharedProjects $rest
        exit $exit
    }
    "list" {
        Write-Host ""
        Write-Host "Auth:" -ForegroundColor Yellow
        $authProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "DataAccess:" -ForegroundColor Yellow
        $dataAccessProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "B2B:" -ForegroundColor Yellow
        $b2bProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Customer:" -ForegroundColor Yellow
        $customerProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Search:" -ForegroundColor Yellow
        $searchProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Payment:" -ForegroundColor Yellow
        $paymentProjects | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "Shared:" -ForegroundColor Yellow
        $sharedProjects | ForEach-Object { Write-Host "  $_" }
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
        Write-Host "  Usage: ./scripts/unit.ps1 <command> [-- <extra dotnet test args>]" -ForegroundColor White
        Write-Host ""
        Write-Host "  Commands:" -ForegroundColor DarkGray
        Write-Host "    run        Run all unit tests (Auth + DataAccess + B2B + Customer + Search + Payment + Shared)"
        Write-Host "    auth       Run Auth unit tests only"
        Write-Host "    b2b        Run B2B unit tests only"
        Write-Host "    customer   Run Customer unit tests only"
        Write-Host "    search     Run Search unit tests only"
        Write-Host "    payment    Run Payment unit tests only"
        Write-Host "    shared     Run Shared unit tests only (Kernel + Messaging)"
        Write-Host "    <module>   Run a specific module (e.g. concert, deal, tenant, workers, user, review, ticket, kernel, messaging)"
        Write-Host "    list       List all unit test projects"
        Write-Host ""
        Write-Host "  Examples:" -ForegroundColor DarkGray
        Write-Host "    ./scripts/unit.ps1 run"
        Write-Host "    ./scripts/unit.ps1 b2b"
        Write-Host "    ./scripts/unit.ps1 concert"
        Write-Host "    ./scripts/unit.ps1 concert --filter ""FullyQualifiedName~LifecycleStateMachineTests"""
        Write-Host ""
    }
}
