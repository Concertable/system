using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Azure.Storage.Blobs;
using Concertable.B2B.Artist.Infrastructure.Extensions;
using Concertable.B2B.Concert.Infrastructure.Extensions;
using Concertable.B2B.Contract.Infrastructure.Extensions;
using Concertable.B2B.Conversations.Infrastructure.Extensions;
using Concertable.B2B.Tenant.Infrastructure.Extensions;
using Concertable.B2B.Seed.Infrastructure;
using Concertable.B2B.Seed.Contracts;
using Concertable.B2B.User.Infrastructure.Extensions;
using Concertable.B2B.Venue.Infrastructure.Extensions;
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
using Concertable.Shared.Blob.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Net.Http.Headers;
using B2BDevDbInitializer = Concertable.B2B.Web.DevDbInitializer;

namespace Concertable.B2B.E2ETests;

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
    private readonly string authUrl;

    public const string TestPaymentMethodId = "pm_card_visa";

    public string B2BWebUrl { get; }
    public string SearchWebUrl { get; }
    public string PaymentWebUrl { get; }
    public string AuthUrl => authUrl;
    public string VenueSpaUrl { get; }
    public string ArtistSpaUrl { get; }
    public string BusinessSpaUrl { get; }
    public HttpClient B2BClient { get; private set; } = null!;
    public HttpClient SearchClient { get; private set; } = null!;
    public HttpClient PaymentClient { get; private set; } = null!;
    public WorkersFixture Workers { get; private set; } = null!;
    public IPollingService Polling { get; private set; } = null!;
    public PaymentIntentService StripePaymentIntents { get; private set; } = null!;
    public StripeFixture Stripe { get; private set; } = null!;
    public SeedState SeedState { get; private set; } = null!;
    public DbFixture DbFixture { get; private set; } = null!;

    public AppFixture()
    {
        loggerFactory = LoggerFactory.Create(b => b
            .AddSimpleConsole(o => o.SingleLine = true)
            .AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "e2e-diagnostics.log")))
            .SetMinimumLevel(LogLevel.Warning)
            .AddFilter("Concertable", LogLevel.Information));
        logger = loggerFactory.CreateLogger<AppFixture>();
        Polling = new PollingService(loggerFactory.CreateLogger<PollingService>());

        configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.E2E.json"))
            .AddEnvironmentVariables()
            .Build();

        var endpoints = configuration.GetSection("Endpoints").Get<E2EEndpoints>()
            ?? throw new InvalidOperationException("Endpoints section is missing from appsettings.E2E.json.");

        B2BWebUrl = endpoints.B2BWeb;
        SearchWebUrl = endpoints.SearchWeb;
        PaymentWebUrl = endpoints.PaymentWeb;
        authUrl = endpoints.Auth;
        VenueSpaUrl = endpoints.VenueSpa;
        ArtistSpaUrl = endpoints.ArtistSpa;
        BusinessSpaUrl = endpoints.BusinessSpa;

        tokenMinter = new TestTokenMinter(configuration);
    }

    public async Task InitializeAsync()
    {
        logger.InitializingE2ETestFixture();

        healthWaiter = new HealthWaiter(loggerFactory.CreateLogger<HealthWaiter>());

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Concertable_B2B_AppHost>();

        builder.AddB2BE2E(B2BWebUrl, SearchWebUrl, authUrl, PaymentWebUrl);
        var stripeClient = new StripeClient(configuration["Stripe:SecretKey"]);
        StripePaymentIntents = new PaymentIntentService(stripeClient);
        Stripe = new StripeFixture(stripeClient);

        app = await builder.BuildAsync();
        resourceLogger = new AspireResourceLogger(
            app.ResourceNotifications, app.Services.GetRequiredService<ResourceLoggerService>(), logger);
        await app.StartAsync();

        B2BClient = new HttpClient { BaseAddress = new Uri(B2BWebUrl) };
        SearchClient = new HttpClient { BaseAddress = new Uri(SearchWebUrl) };
        PaymentClient = new HttpClient { BaseAddress = new Uri(PaymentWebUrl) };
        Workers = new WorkersFixture(app, Polling);

        // WORKAROUND (TECH_DEBT.md): 12 not 6 — the 71 demo users seed via the async
        // credential-registration chain, slow on CI's ASB emulator. Revert to 6 once seed is faster.
        await healthWaiter.WaitForAllHealthyAsync(
            [B2BWebUrl, SearchWebUrl, PaymentWebUrl],
            TimeSpan.FromMinutes(12));

        var paymentConnectionString = await app.GetConnectionStringAsync(AppHostConstants.Databases.Payment);
        await healthWaiter.WaitForPayoutAccountsAsync(paymentConnectionString, 4, TimeSpan.FromMinutes(3));

        DbFixture = new DbFixture(app);
        await DbFixture.InitializeAsync();
        await DbFixture.ResetAsync();

        var b2bConnectionString = await app.GetConnectionStringAsync(AppHostConstants.Databases.B2B);
        var blobConnectionString = await app.GetConnectionStringAsync("blobs");
        var asbConnectionString = await app.GetConnectionStringAsync("asb")
            ?? throw new InvalidOperationException("ASB connection string is missing.");

        var b2bSeedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{AppHostConstants.Databases.B2B}"] = b2bConnectionString,
                ["BlobStorage:ContainerName"] = "images",
                ["ExternalServices:UseRealBlob"] = "false",
            })
            .Build();

        host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IConfiguration>(b2bSeedConfig);
                services.AddLogging(b => b
                    .AddSimpleConsole(o => o.SingleLine = true)
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddFilter("Concertable.B2B.Web.DevDbInitializer", LogLevel.Information));
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<SeedCatalog>();
                services.AddCurrentUser();
                services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
                services.AddScoped<AuditInterceptor>();
                services.AddScoped<TenantInterceptor>();
                services.AddScoped<IDomainEventDispatchInterceptor, SeedingDomainEventDispatchInterceptor>();
                services.AddGeometry();
                services.AddOutbox(opt => opt.UseSqlServer(b2bConnectionString), runDispatcher: false);
                services.AddInbox(opt => opt.UseSqlServer(b2bConnectionString));
                services.AddSeedingInfrastructure();
                services.AddScoped<SeedState>();
                services.AddSingleton(new BlobServiceClient(blobConnectionString));
                services.AddSharedBlob(b2bSeedConfig);
                services.AddUserModule(b2bSeedConfig);
                services.AddTenantModule(b2bSeedConfig);
                services.AddArtistModule(b2bSeedConfig);
                services.AddVenueModule(b2bSeedConfig);
                services.AddContractModule(b2bSeedConfig);
                services.AddConcertModule(b2bSeedConfig);
                services.AddConversationsModule(b2bSeedConfig);
                services.AddBlobDevSeeder();
                services.AddUserDevSeeder();
                services.AddTenantDevSeeder();
                services.AddArtistDevSeeder();
                services.AddVenueDevSeeder();
                services.AddContractDevSeeder();
                services.AddConcertDevSeeder();
                services.AddConversationsDevSeeder();
                services.AddScoped<IDbInitializer, B2BDevDbInitializer>();
            })
            .Build();

        await host.StartAsync();
        await ReseedAsync();

        logger.E2ETestFixtureReady();
    }

    public async Task ResetAsync()
    {
        logger.ResettingTestState();
        Stripe.Reset();
        await DbFixture.ResetAsync();
        await ReseedAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var token = await tokenMinter.MintAsync(email, SeedState.TestPassword);
        var client = new HttpClient { BaseAddress = new Uri(B2BWebUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        B2BClient.Dispose();
        SearchClient.Dispose();
        PaymentClient.Dispose();
        Workers.Dispose();
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
    }
}
