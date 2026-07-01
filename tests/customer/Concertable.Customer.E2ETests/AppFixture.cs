using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.B2B.Seed.Contracts;
using Concertable.Customer.Artist.Infrastructure.Extensions;
using Concertable.Customer.Concert.Infrastructure.Extensions;
using Concertable.Customer.Preference.Infrastructure.Extensions;
using Concertable.Customer.Seed.Infrastructure;
using Concertable.Customer.Venue.Infrastructure.Extensions;
using Concertable.DataAccess.Application;
using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Kernel;
using Concertable.Kernel.Events;
using Concertable.Kernel.Extensions;
using Concertable.Kernel.Identity;
using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Messaging.Infrastructure.Inbox;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Seed.Shared;
using Concertable.Seed.Infrastructure;
using Concertable.Seed.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using CustomerDevDbInitializer = Concertable.Customer.Web.DevDbInitializer;

namespace Concertable.Customer.E2ETests;

public sealed class AppFixture : IAsyncLifetime
{
    private DistributedApplication app = null!;
    private AspireResourceLogger resourceLogger = null!;
    private IHost host = null!;
    private HealthWaiter healthWaiter = null!;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<AppFixture> logger;
    private readonly IConfiguration configuration;
    private readonly TestTokenMinter tokenMinter;

    private readonly string customerWebUrl;
    private readonly string searchWebUrl;
    private readonly string paymentWebUrl;
    private readonly string authUrl;
    private readonly string customerSpaUrl;

    public const string TestPaymentMethodId = "pm_card_visa";

    public HttpClient CustomerClient { get; private set; } = null!;
    public IPollingService Polling { get; private set; } = null!;
    public SeedState SeedState { get; private set; } = null!;
    public SeedCatalog Catalog { get; private set; } = null!;
    public DbFixture DbFixture { get; private set; } = null!;
    public string AuthUrl => authUrl;
    public string CustomerSpaUrl => customerSpaUrl;

    public AppFixture()
    {
        loggerFactory = LoggerFactory.Create(b => b
            .AddSimpleConsole(o => o.SingleLine = true)
            .AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "e2e-diagnostics.log")))
            .SetMinimumLevel(LogLevel.Information));
        logger = loggerFactory.CreateLogger<AppFixture>();
        Polling = new PollingService(loggerFactory.CreateLogger<PollingService>());

        configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.E2E.json"))
            .AddEnvironmentVariables()
            .Build();

        customerWebUrl = configuration["Endpoints:CustomerWeb"]
            ?? throw new InvalidOperationException("Endpoints:CustomerWeb is missing from appsettings.E2E.json.");
        searchWebUrl = configuration["Endpoints:SearchWeb"]
            ?? throw new InvalidOperationException("Endpoints:SearchWeb is missing from appsettings.E2E.json.");
        paymentWebUrl = configuration["Endpoints:PaymentWeb"]
            ?? throw new InvalidOperationException("Endpoints:PaymentWeb is missing from appsettings.E2E.json.");
        authUrl = configuration["Endpoints:Auth"]
            ?? throw new InvalidOperationException("Endpoints:Auth is missing from appsettings.E2E.json.");
        customerSpaUrl = configuration["Endpoints:CustomerSpa"]
            ?? throw new InvalidOperationException("Endpoints:CustomerSpa is missing from appsettings.E2E.json.");

        tokenMinter = new TestTokenMinter(configuration);
    }

    public async Task InitializeAsync()
    {
        logger.InitializingE2ETestFixture();

        healthWaiter = new HealthWaiter(loggerFactory.CreateLogger<HealthWaiter>());

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Concertable_Customer_AppHost>();

        builder.AddCustomerE2E(customerWebUrl, searchWebUrl, authUrl, paymentWebUrl);

        app = await builder.BuildAsync();
        resourceLogger = new AspireResourceLogger(
            app.ResourceNotifications, app.Services.GetRequiredService<ResourceLoggerService>(), logger);
        await app.StartAsync();

        CustomerClient = new HttpClient { BaseAddress = new Uri(customerWebUrl) };

        // WORKAROUND (TECH_DEBT.md): 12 not 6 — demo users seed via the async credential-
        // registration chain, slow on CI's ASB emulator. Revert to 6 once seed is faster.
        await healthWaiter.WaitForAllHealthyAsync(
            [customerWebUrl, searchWebUrl, paymentWebUrl],
            TimeSpan.FromMinutes(12));

        DbFixture = new DbFixture(app);
        await DbFixture.InitializeAsync();
        await DbFixture.ResetAsync();

        var customerConnectionString = await app.GetConnectionStringAsync(AppHostConstants.Databases.Customer)
            ?? throw new InvalidOperationException("Customer DB connection string is missing.");

        var customerSeedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{AppHostConstants.Databases.Customer}"] = customerConnectionString,
            })
            .Build();

        host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IConfiguration>(customerSeedConfig);
                services.AddLogging(b => b
                    .AddSimpleConsole(o => o.SingleLine = true)
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddFilter("Concertable.Customer.Web.DevDbInitializer", LogLevel.Information));
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<SeedCatalog>();
                services.AddCurrentUser();
                services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
                services.AddScoped<AuditInterceptor>();
                services.AddScoped<IDomainEventDispatchInterceptor, SeedingDomainEventDispatchInterceptor>();
                services.AddOutbox(opt => opt.UseSqlServer(customerConnectionString), runDispatcher: false);
                services.AddInbox(opt => opt.UseSqlServer(customerConnectionString));
                services.AddSeedingInfrastructure();
                services.AddScoped<SeedState>();
                services.AddVenueModule(customerSeedConfig);
                services.AddArtistModule(customerSeedConfig);
                services.AddConcertModule(customerSeedConfig);
                services.AddPreferenceModule(customerSeedConfig);
                services.AddPreferenceDevSeeder();
                services.AddScoped<IDbInitializer, CustomerDevDbInitializer>();
            })
            .Build();

        await host.StartAsync();
        await ReseedAsync();

        logger.E2ETestFixtureReady();
    }

    public async Task ResetAsync()
    {
        logger.ResettingTestState();
        await DbFixture.ResetAsync();
        await ReseedAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var token = await tokenMinter.MintAsync(email, SeedState.TestPassword);
        var client = new HttpClient { BaseAddress = new Uri(customerWebUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        CustomerClient.Dispose();
        tokenMinter.Dispose();
        healthWaiter.Dispose();
        await DbFixture.DisposeAsync();
        await host.StopAsync();
        host.Dispose();
        await app.DisposeAsync();
        await resourceLogger.DisposeAsync();
        loggerFactory.Dispose();
    }

    public ResourceNotificationService ResourceNotifications => app.ResourceNotifications;

    private async Task ReseedAsync()
    {
        await using var scope = host.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        await initializer.InitializeAsync();
        SeedState = scope.ServiceProvider.GetRequiredService<SeedState>();
        Catalog = scope.ServiceProvider.GetRequiredService<SeedCatalog>();
    }
}
