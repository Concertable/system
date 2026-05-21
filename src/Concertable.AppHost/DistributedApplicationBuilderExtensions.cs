using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

internal static class DistributedApplicationBuilderExtensions
{
    public static (IResourceBuilder<SqlServerDatabaseResource> defaultDb, IResourceBuilder<SqlServerDatabaseResource> customerDb, IResourceBuilder<SqlServerDatabaseResource> searchDb, IResourceBuilder<SqlServerDatabaseResource> paymentDb) AddSqlServer(this IDistributedApplicationBuilder builder)
    {
        var sql = builder.AddSqlServer("sql").WithDataVolume("concertable-sql-data");
        var defaultDb = sql.AddDatabase("DefaultConnection");
        var customerDb = sql.AddDatabase("CustomerDb");
        var searchDb = sql.AddDatabase("SearchDb");
        var paymentDb = sql.AddDatabase("PaymentDb");
        return (defaultDb, customerDb, searchDb, paymentDb);
    }

    public static IResourceBuilder<AzureServiceBusResource> AddServiceBus(this IDistributedApplicationBuilder builder)
    {
        return builder.AddAzureServiceBus("asb")
            .AddTopic("event-artistchangedevent", ["concertable-search"])
            .AddTopic("event-venuechangedevent", ["concertable-search"])
            .AddTopic("event-concertchangedevent", ["concertable-search"])
            .AddTopic("event-reviewsubmittedevent", ["concertable-customer"])
            .AddTopic("event-artistratingupdatedevent", ["concertable-search"])
            .AddTopic("event-venueratingupdatedevent", ["concertable-search"])
            .AddTopic("event-concertratingupdatedevent", ["concertable-search"])
            .AddTopic("event-customerregisteredevent", ["concertable-payment"])
            .AddTopic("event-venuemanagerregisteredevent", ["concertable-payment"])
            .AddTopic("event-artistmanagerregisteredevent", ["concertable-payment"])
            .AddTopic("event-paymentsucceededevent", ["concertable-b2b", "concertable-customer", "concertable-payment"])
            .AddTopic("event-paymentfailedevent", ["concertable-b2b", "concertable-customer", "concertable-payment"])
            .RunAsEmulator();
    }

    public static (IResourceBuilder<AzureStorageResource> storage, IResourceBuilder<AzureBlobStorageResource> blobs) AddAzureStorage(this IDistributedApplicationBuilder builder)
    {
        var storage = builder.AddAzureStorage("storage")
                             .RunAsEmulator(c => c.WithDataVolume("concertable-azurite-data"));
        var blobs = storage.AddBlobs("blobs");
        return (storage, blobs);
    }

    public static IResourceBuilder<ProjectResource> AddAuth(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        var auth = builder.AddProject<Projects.Concertable_Auth>("auth")
                          .WithReference(sql)
                          .WaitFor(sql)
                          .AddSecrets(builder, "ServiceAuth:B2BClientSecret", "ServiceAuth:CustomerClientSecret");

        var lanIp = builder.Configuration["MobileLanIp"];
        if (!string.IsNullOrEmpty(lanIp))
        {
            auth.WithEnvironment("Auth__ExpoGoRedirectUri__Customer", $"exp://{lanIp}:8082");
            auth.WithEnvironment("Auth__ExpoGoRedirectUri__Business", $"exp://{lanIp}:8083");
        }

        return auth;
    }

    public static IResourceBuilder<ProjectResource> AddApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> sql,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<AzureStorageResource> storage,
        IResourceBuilder<AzureBlobStorageResource> blobs,
        IResourceBuilder<AzureServiceBusResource> asb,
        IResourceBuilder<ProjectResource> paymentWeb)
    {
        var b2bSecret = builder.Configuration["ServiceAuth:B2BClientSecret"];
        return builder.AddProject<Projects.Concertable_B2B_Web>("api")
                      .WithReference(sql)
                      .WaitFor(sql)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(blobs)
                      .WaitFor(storage)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithReference(paymentWeb)
                      .WaitFor(paymentWeb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .WithEnvironment("ServiceAuth__ClientId", "concertable-b2b")
                      .WithEnvironment("ServiceAuth__ClientSecret", b2bSecret ?? "")
                      .AddSecrets(builder, "Stripe:SecretKey");
    }

    public static IResourceBuilder<AzureFunctionsProjectResource> AddWorkers(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        return builder.AddAzureFunctionsProject<Projects.Concertable_B2B_Workers>("workers")
                      .WithReference(sql)
                      .WaitFor(sql);
    }

    public static IResourceBuilder<ProjectResource> AddCustomerWeb(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<SqlServerDatabaseResource> customerDb,
        IResourceBuilder<AzureServiceBusResource> asb,
        IResourceBuilder<ProjectResource> paymentWeb)
    {
        var customerSecret = builder.Configuration["ServiceAuth:CustomerClientSecret"];
        return builder.AddProject<Projects.Concertable_Customer_Web>("customer-web")
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(customerDb)
                      .WaitFor(customerDb)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithReference(paymentWeb)
                      .WaitFor(paymentWeb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .WithEnvironment("ServiceAuth__ClientId", "concertable-customer")
                      .WithEnvironment("ServiceAuth__ClientSecret", customerSecret ?? "");
    }

    public static IResourceBuilder<ProjectResource> AddSearchWeb(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<SqlServerDatabaseResource> searchDb)
    {
        return builder.AddProject<Projects.Concertable_Search_Web>("search-web")
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(searchDb)
                      .WaitFor(searchDb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"));
    }

    public static IResourceBuilder<ProjectResource> AddSearchWorkers(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> searchDb,
        IResourceBuilder<AzureServiceBusResource> asb)
    {
        return builder.AddProject<Projects.Concertable_Search_Workers>("search-workers")
                      .WithReference(searchDb)
                      .WaitFor(searchDb)
                      .WithReference(asb)
                      .WaitFor(asb);
    }

    public static IResourceBuilder<ProjectResource> AddPaymentWeb(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
    {
        return builder.AddProject<Projects.Concertable_Payment_Web>("payment-web")
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .AddSecrets(builder, "Stripe:SecretKey", "Stripe:WebhookSecret", "ExternalServices:UseRealStripe");
    }

    public static IResourceBuilder<ProjectResource> AddPaymentWorkers(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
    {
        return builder.AddProject<Projects.Concertable_Payment_Workers>("payment-workers")
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .AddSecrets(builder, "Stripe:SecretKey", "ExternalServices:UseRealStripe");
    }

    public static IResourceBuilder<NodeAppResource> AddCustomerSpa(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth) =>
        AddSpaSurface(builder, api, auth, "customer", 5174);

    public static IResourceBuilder<NodeAppResource> AddVenueSpa(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth) =>
        AddSpaSurface(builder, api, auth, "venue", 5175);

    public static IResourceBuilder<NodeAppResource> AddArtistSpa(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth) =>
        AddSpaSurface(builder, api, auth, "artist", 5176);

    public static IResourceBuilder<NodeAppResource> AddBusinessSpa(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth) =>
        AddSpaSurface(builder, api, auth, "business", 5177);

    private static IResourceBuilder<NodeAppResource> AddSpaSurface(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth, string surface, int port) =>
        builder.AddNpmApp(surface, $"../../app/web/{surface}", "dev")
               .WithHttpsEndpoint(port: port, isProxied: false)
               .WithReference(api)
               .WithReference(auth)
               .WaitFor(api);

    public static void AddMobile(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth)
    {
        if (!builder.Configuration.GetValue<bool>("RunMobile"))
            return;

        var tunnel = builder.AddDevTunnel("concertable-dev").WithAnonymousAccess();
        var lanIp = builder.Configuration["MobileLanIp"] ?? "localhost";

        tunnel.WithReference(auth, allowAnonymous: true);
        tunnel.WithReference(api, allowAnonymous: true);
        auth.WithEnvironment(ctx =>
        {
            if (ctx.EnvironmentVariables.TryGetValue("services__auth__https__0", out var authUrl))
                ctx.EnvironmentVariables["Auth__PublicUrl"] = authUrl;
        });

        AddMobileSurface(builder, api, auth, tunnel, lanIp, "customer");
        AddMobileSurface(builder, api, auth, tunnel, lanIp, "business");
    }

    private static IResourceBuilder<NodeAppResource> AddMobileSurface(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> api,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<DevTunnelResource> tunnel,
        string lanIp,
        string surface)
    {
        var mobile = builder.AddNpmApp($"mobile-{surface}", $"../../app/mobile/{surface}", "start:ci")
               .WithEnvironment("REACT_NATIVE_PACKAGER_HOSTNAME", lanIp)
               .WithReference(api, tunnel)
               .WithReference(auth, tunnel)
               .WaitFor(api)
               .WaitFor(tunnel)
               .WithEnvironment(ctx =>
               {
                   if (ctx.EnvironmentVariables.TryGetValue("services__api__https__0", out var apiUrl))
                       ctx.EnvironmentVariables["EXPO_PUBLIC_API_URL"] = apiUrl;
                   if (ctx.EnvironmentVariables.TryGetValue("services__auth__https__0", out var authUrl))
                       ctx.EnvironmentVariables["EXPO_PUBLIC_AUTH_AUTHORITY"] = authUrl;
               });

        mobile.WithCommand(
            name: "clear-metro-cache",
            displayName: "Clear Metro Cache",
            executeCommand: async ctx =>
            {
                var mobileDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "app", "mobile", surface));
                File.WriteAllText(Path.Combine(mobileDir, ".metro-clear"), "");

                var commands = ctx.ServiceProvider.GetRequiredService<ResourceCommandService>();
                await commands.ExecuteCommandAsync(mobile.Resource, KnownResourceCommands.RestartCommand, ctx.CancellationToken);
                return new ExecuteCommandResult { Success = true };
            },
            commandOptions: new CommandOptions { IconName = "ArrowCounterclockwise" });

        return mobile;
    }

    public static void AddStripeCli(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api)
    {
        var secretKey = builder.Configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return;

        if (builder.ExecutionContext.IsRunMode)
        {
            builder.AddExecutable(
                name: "stripe-cli",
                command: "stripe",
                workingDirectory: ".",
                "listen", "--api-key", secretKey,
                "--forward-to", "https://localhost:7086/api/webhook",
                "--skip-verify");
            return;
        }

        var webhookSecret = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var stripeCli = builder.AddContainer("stripe-cli", "stripe/stripe-cli")
               .WithVolume("stripe-cli-config", "/root/.config/stripe")
               .WithArgs("listen", "--api-key", secretKey, "--forward-to",
                   ReferenceExpression.Create($"{api.GetEndpoint("http")}/api/webhook"));

        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var logs = evt.Services.GetRequiredService<ResourceLoggerService>();
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var line in logs.WatchLinesAsync(stripeCli.Resource, ct))
                    {
                        var match = Regex.Match(line.Content, @"whsec_\w+");
                        if (match.Success)
                        {
                            webhookSecret.TrySetResult(match.Value);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    webhookSecret.TrySetCanceled(ct);
                }
            }, ct);
            return Task.CompletedTask;
        });

        api.WithEnvironment(async ctx =>
        {
            ctx.EnvironmentVariables["Stripe__WebhookSecret"] =
                await webhookSecret.Task.WaitAsync(TimeSpan.FromSeconds(60));
        });
    }

    private static async IAsyncEnumerable<LogLine> WatchLinesAsync(
        this ResourceLoggerService logs,
        IResource resource,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var batch in logs.WatchAsync(resource).WithCancellation(ct))
            foreach (var line in batch)
                yield return line;
    }

    private static IResourceBuilder<ProjectResource> AddSecrets(
        this IResourceBuilder<ProjectResource> resource, 
        IDistributedApplicationBuilder builder, 
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = builder.Configuration[key];
            if (!string.IsNullOrEmpty(value))
                resource = resource.WithEnvironment(key.Replace(":", "__"), value);
        }
        return resource;
    }
}
