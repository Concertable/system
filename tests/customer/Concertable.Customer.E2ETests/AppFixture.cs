using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Customer.TestKit;
using Concertable.E2E;
using Concertable.Payment.E2ETests.Helpers;
using Concertable.Payment.Hosting;
using Concertable.Payment.TestKit;
using Concertable.Seed.Identity;
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
    private PaymentIntentService stripePaymentIntents = null!;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<AppFixture> logger;
    private readonly IConfiguration configuration;
    private readonly TestTokenMinter tokenMinter;

    private readonly string customerWebUrl;
    private readonly string searchWebUrl;
    private readonly string paymentWebUrl;
    private readonly string authUrl;
    private readonly string customerSpaUrl;

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
        var composition = Compositions.Source();
        var builder = await composition.CreateBuilderAsync(Surface.Customer);
        var stripeSecretKey = builder.Configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured for the Customer E2E fixture.");
        var stripeClient = new StripeClient(stripeSecretKey);
        stripePaymentIntents = new PaymentIntentService(stripeClient);
        StripeCustomerResolver = await Concertable.Testing.E2E.StripeCustomerResolver.CreateAsync(stripeClient);
        var run = Run.Create(Profile.Customer(customerWebUrl, searchWebUrl, authUrl, paymentWebUrl));

        builder.AddE2EStack(run, composition, StripeCustomerResolver);

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
            run.AdminKey);
        var paymentTestClient = new PaymentTestClient(
            paymentAdminClient,
            run.AdminKey);
        DbFixture = new DbFixture(customerTestClient, paymentTestClient);
        await DbFixture.ResetAsync();
        SeedState = await customerTestClient.GetSeedStateAsync();

        var payoutAccounts = new PayoutAccountDb(
            await app.GetConnectionStringAsync(PaymentConstants.Database)
                ?? throw new InvalidOperationException("Payment connection string is missing."));
        await Polling.UntilAsync(
            async () => (
                Chargeable: await payoutAccounts.GetChargeableOwnerIdsAsync(),
                Payable: await payoutAccounts.GetPayableOwnerIdsAsync()),
            provisioned =>
                provisioned.Chargeable.Contains(SeedCustomers.CustomerId(1))
                && StripeTestAccounts.ByOwnerId.Keys.All(provisioned.Payable.Contains),
            timeout: TimeSpan.FromMinutes(3));

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

    public Task ConfirmPaymentAsync(string clientSecret)
    {
        var separatorIndex = clientSecret.IndexOf("_secret_", StringComparison.Ordinal);
        if (separatorIndex <= 0)
            throw new ArgumentException("The payment client secret is invalid.", nameof(clientSecret));

        return stripePaymentIntents.ConfirmAsync(
            clientSecret[..separatorIndex],
            new PaymentIntentConfirmOptions { PaymentMethod = "pm_card_visa" });
    }

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
