using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.B2B.Hosting;
using Concertable.B2B.TestKit;
using Concertable.Fleet.E2E;
using Concertable.Payment.Hosting;
using Concertable.Payment.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Net.Http.Headers;

namespace Concertable.B2B.E2ETests;

public sealed class AppFixture : IAsyncLifetime
{
    private DistributedApplication app = null!;
    private AspireResourceLogger resourceLogger = null!;
    private HealthWaiter healthWaiter = null!;
    private HttpClient b2bAdminClient = null!;
    private HttpClient paymentAdminClient = null!;
    private B2BTestClient b2bTestClient = null!;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<AppFixture> logger;
    private readonly IConfiguration configuration;
    private readonly TestTokenMinter tokenMinter;
    private readonly string authUrl;
    private readonly SemaphoreSlim resetGate = new(1, 1);

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
    public StripeCustomerResolver StripeCustomerResolver { get; private set; } = null!;
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
        var projectProvider = FleetProjectProviders.Source();
        var builder = await projectProvider.CreateBuilderAsync(FleetSurface.B2B);
        var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured for the B2B E2E fixture.");
        var stripeClient = new StripeClient(stripeSecretKey);
        StripeCustomerResolver = await Concertable.Testing.E2E.StripeCustomerResolver.CreateAsync(stripeClient);
        var fleetRun = FleetRun.Create(FleetProfile.B2B(B2BWebUrl, SearchWebUrl, authUrl, PaymentWebUrl));

        builder.AddE2EStack(fleetRun, projectProvider, StripeCustomerResolver);
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

        var paymentConnectionString = await app.GetConnectionStringAsync(PaymentConstants.Database)
            ?? throw new InvalidOperationException("Payment connection string is missing.");
        await healthWaiter.WaitForPayoutAccountsAsync(paymentConnectionString, 4, TimeSpan.FromMinutes(3));

        b2bAdminClient = new HttpClient { BaseAddress = new Uri(B2BWebUrl) };
        paymentAdminClient = new HttpClient { BaseAddress = new Uri(PaymentWebUrl) };
        b2bTestClient = new B2BTestClient(
            b2bAdminClient,
            fleetRun.AdminKey);
        var paymentTestClient = new PaymentTestClient(
            paymentAdminClient,
            fleetRun.AdminKey);
        DbFixture = new DbFixture(b2bTestClient, paymentTestClient);
        await DbFixture.ResetAsync();
        SeedState = await b2bTestClient.GetSeedStateAsync();

        logger.E2ETestFixtureReady();
    }

    public async Task ResetAsync()
    {
        await resetGate.WaitAsync();
        try
        {
            logger.ResettingTestState();
            Stripe.Reset();
            await DbFixture.ResetAsync();
            SeedState = await b2bTestClient.GetSeedStateAsync();
        }
        finally
        {
            resetGate.Release();
        }
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var token = await tokenMinter.MintAsync(email, SeedState.TestPassword);
        var client = new HttpClient { BaseAddress = new Uri(B2BWebUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task WaitForTokenMintingAsync(string email, string password) =>
        tokenMinter.WaitUntilMintableAsync(email, password, Polling);

    public async Task DisposeAsync()
    {
        try
        {
            B2BClient?.Dispose();
            SearchClient?.Dispose();
            PaymentClient?.Dispose();
            b2bAdminClient?.Dispose();
            paymentAdminClient?.Dispose();
            Workers?.Dispose();
            tokenMinter.Dispose();
            healthWaiter?.Dispose();
            if (app is not null)
                await app.DisposeAsync();
            if (resourceLogger is not null)
                await resourceLogger.DisposeAsync();
        }
        finally
        {
            try
            {
                if (StripeCustomerResolver is not null)
                    await StripeCustomerResolver.DisposeAsync();
            }
            finally
            {
                resetGate.Dispose();
                loggerFactory.Dispose();
            }
        }
    }

    public ResourceNotificationService ResourceNotifications => app.ResourceNotifications;

    // AddNpmApp has no HTTP readiness check, so Aspire reports a SPA 'Running' before its Vite dev
    // server actually serves. UI runs must gate on real serving (polls until 200, throws on timeout)
    // before driving a browser at it — otherwise the first navigation races Vite's startup.
    public Task WaitForSpasServingAsync(TimeSpan timeout) =>
        healthWaiter.WaitForAllServingAsync([VenueSpaUrl, ArtistSpaUrl, BusinessSpaUrl], timeout);

}
