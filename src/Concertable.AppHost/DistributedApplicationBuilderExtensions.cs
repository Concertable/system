using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

internal static class DistributedApplicationBuilderExtensions
{
    public static IResourceBuilder<SqlServerDatabaseResource> AddSqlServer(this IDistributedApplicationBuilder builder)
    {
        return builder.AddSqlServer("sql")
                      .WithDataVolume("concertable-sql-data")
                      .AddDatabase("DefaultConnection");
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
                          .WaitFor(sql);

        var lanIp = builder.Configuration["MobileLanIp"];
        if (!string.IsNullOrEmpty(lanIp))
            auth.WithEnvironment("Auth__ExpoGoRedirectUri", $"exp://{lanIp}:8082");

        return auth;
    }

    public static IResourceBuilder<ProjectResource> AddApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> sql,
        IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<AzureStorageResource> storage,
        IResourceBuilder<AzureBlobStorageResource> blobs)
    {
        return builder.AddProject<Projects.Concertable_Web>("api")
                      .WithReference(sql)
                      .WaitFor(sql)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(blobs)
                      .WaitFor(storage)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .AddSecrets(builder, "Stripe:SecretKey");
    }

    public static IResourceBuilder<AzureFunctionsProjectResource> AddWorkers(this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> sql)
    {
        return builder.AddAzureFunctionsProject<Projects.Concertable_Workers>("workers")
                      .WithReference(sql)
                      .WaitFor(sql);
    }

    public static IResourceBuilder<NodeAppResource> AddFrontend(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth)
    {
        return builder.AddNpmApp("frontend", "../../app/web", "dev")
                      .WithHttpsEndpoint(port: 5173, isProxied: false)
                      .WithHttpHealthCheck(endpointName: "https", path: "/")
                      .WithReference(api)
                      .WaitFor(api);
    }

    public static void AddMobile(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api, IResourceBuilder<ProjectResource> auth)
    {
        if (!builder.Configuration.GetValue<bool>("RunMobile"))
            return;

        var tunnel = builder.AddDevTunnel("concertable-dev").WithAnonymousAccess();
        var lanIp = builder.Configuration["MobileLanIp"] ?? "localhost";

        tunnel.WithReference(auth, allowAnonymous: true);
        auth.WithEnvironment(ctx =>
        {
            if (ctx.EnvironmentVariables.TryGetValue("services__auth__https__0", out var authUrl))
                ctx.EnvironmentVariables["Auth__PublicUrl"] = authUrl;
        });

        tunnel.WithReference(api, allowAnonymous: true);

        builder.AddNpmApp("mobile", "../../app/mobile", "start:ci")
               .WithEnvironment("REACT_NATIVE_PACKAGER_HOSTNAME", lanIp)
               .WithReference(api, tunnel)
               .WithReference(auth, tunnel)
               .WaitFor(api)
               .WithEnvironment(ctx =>
               {
                   if (ctx.EnvironmentVariables.TryGetValue("services__api__https__0", out var apiUrl))
                       ctx.EnvironmentVariables["EXPO_PUBLIC_API_URL"] = apiUrl;
                   if (ctx.EnvironmentVariables.TryGetValue("services__auth__https__0", out var authUrl))
                       ctx.EnvironmentVariables["EXPO_PUBLIC_AUTH_AUTHORITY"] = authUrl;
               })
               .WithCommand(
                   name: "clear-metro-cache",
                   displayName: "Clear Metro Cache",
                   executeCommand: async ctx =>
                   {
                       var mobileDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "app", "mobile"));
                       File.WriteAllText(Path.Combine(mobileDir, ".metro-clear"), "");

                       using var kill = new System.Diagnostics.Process();
                       kill.StartInfo = new System.Diagnostics.ProcessStartInfo
                       {
                           FileName = "powershell",
                           Arguments = "-c \"(Get-NetTCPConnection -LocalPort 8082 -ErrorAction SilentlyContinue).OwningProcess | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }\"",
                           UseShellExecute = false,
                           CreateNoWindow = true,
                       };
                       kill.Start();
                       await kill.WaitForExitAsync(ctx.CancellationToken);

                       var pidFile = Path.Combine(mobileDir, ".metro-pid");
                       if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
                       {
                           try { System.Diagnostics.Process.GetProcessById(pid).Kill(entireProcessTree: true); }
                           catch { }
                       }

                       return new ExecuteCommandResult { Success = true };
                   },
                   iconName: "ArrowCounterclockwise");
    }

    public static void AddStripeCli(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api)
    {
        var secretKey = builder.Configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return;

        var webhookSecret = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var stripeCli = builder.AddContainer("stripe-cli", "stripe/stripe-cli")
               .WithArgs("listen",
                   "--api-key", secretKey,
                   "--forward-to", "http://host.docker.internal:5161/api/webhook")
               .WithVolume("stripe-cli-config", "/root/.config/stripe");

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

        api.WaitFor(stripeCli)
           .WithEnvironment(async ctx =>
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

    private static IResourceBuilder<ProjectResource> AddSecrets(this IResourceBuilder<ProjectResource> resource, IDistributedApplicationBuilder builder, params string[] keys)
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
