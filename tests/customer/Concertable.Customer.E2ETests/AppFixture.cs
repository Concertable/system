using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Customer.TestKit;
using Concertable.Fleet.E2E;
using Concertable.Payment.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Net.Http.Headers;

namespace Concertable.Customer.E2ETests;

public sealed class AppFixture : IAsyncLifetime
{
    private DistributedApplication app = null!;
    private AspireResourceLogger resourceLogger = null!;
    private HealthWaiter healthWaiter = null!;
    private HttpClient customerAdminClient = null!;
    private HttpClient paymentAdminClient = null!;
    private CustomerTestClient customerTestClient = null!;
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
    public DbFixture DbFixture { get; private set; } = null!;
    public StripeCustomerResolver StripeCustomerResolver { get; private set; } = null!;
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
        var projectProvider = FleetProjectProviders.Source();
        var builder = await projectProvider.CreateBuilderAsync(FleetSurface.Customer);
        var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured for the Customer E2E fixture.");
        var stripeClient = new StripeClient(stripeSecretKey);
        StripeCustomerResolver = await Concertable.Testing.E2E.StripeCustomerResolver.CreateAsync(stripeClient);
        var fleetRun = FleetRun.Create(FleetProfile.Customer(customerWebUrl, searchWebUrl, authUrl, paymentWebUrl));

        builder.AddE2EStack(fleetRun, projectProvider, StripeCustomerResolver);

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

        customerAdminClient = new HttpClient { BaseAddress = new Uri(customerWebUrl) };
        paymentAdminClient = new HttpClient { BaseAddress = new Uri(paymentWebUrl) };
        customerTestClient = new CustomerTestClient(
            customerAdminClient,
            fleetRun.AdminKey);
        var paymentTestClient = new PaymentTestClient(
            paymentAdminClient,
            fleetRun.AdminKey);
        DbFixture = new DbFixture(customerTestClient, paymentTestClient);
        await DbFixture.ResetAsync();
        SeedState = await customerTestClient.GetSeedStateAsync();

        logger.E2ETestFixtureReady();
    }

    public async Task ResetAsync()
    {
        logger.ResettingTestState();
        await DbFixture.ResetAsync();
        SeedState = await customerTestClient.GetSeedStateAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var token = await tokenMinter.MintAsync(email, SeedState.TestPassword);
        var client = new HttpClient { BaseAddress = new Uri(customerWebUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task WaitForTokenMintingAsync(string email, string password) =>
        tokenMinter.WaitUntilMintableAsync(email, password, Polling);

    public async Task DisposeAsync()
    {
        try
        {
            CustomerClient?.Dispose();
            customerAdminClient?.Dispose();
            paymentAdminClient?.Dispose();
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
                loggerFactory.Dispose();
            }
        }
    }

    public ResourceNotificationService ResourceNotifications => app.ResourceNotifications;

}
