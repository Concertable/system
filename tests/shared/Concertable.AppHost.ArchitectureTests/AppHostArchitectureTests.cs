using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Testing.Architecture;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Concertable.AppHost.ArchitectureTests;

public sealed class AppHostArchitectureTests
{
    [Fact]
    public async Task Build_ProductionGraph_IsValid()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        Assert.DoesNotContain(builder.Resources.OfType<NodeAppResource>(),
            resource => resource.Name.StartsWith("mobile-", StringComparison.Ordinal));
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "concertable-dev");
        var auth = builder.Resources.Single(resource => resource.Name == "auth");
        var authEnvironment = await GetRawEnvironmentAsync(auth, CancellationToken.None);
        Assert.DoesNotContain("Auth__PublicUrl", authEnvironment.Keys);
        await using var app = await builder.BuildAsync();
    }

    [Fact]
    public async Task Build_AllFrontendSurfaces_AreOwnedAndCollisionFree()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>(
            ["--RunMobile=true"]);
        Assert.Equal(
            new[] { "admin", "artist", "business", "customer", "mobile-b2b", "mobile-customer", "venue" },
            builder.Resources.OfType<NodeAppResource>().Select(resource => resource.Name).Order());
        Assert.Single(builder.Resources, resource => resource.Name == "concertable-dev");
        var surfaces = SystemLocalSpaSurfaces.All;
        Assert.Equal(5, surfaces.Count);
        Assert.Equal(surfaces.Count, surfaces.Select(surface => surface.ResourceName).Distinct().Count());
        Assert.Equal(surfaces.Count, surfaces.Select(surface => surface.HttpsPort).Distinct().Count());
        await using var app = await builder.BuildAsync();

        var auth = builder.Resources.Single(resource => resource.Name == "auth");
        var environment = await GetRawEnvironmentAsync(auth, CancellationToken.None);
        Assert.Equal(
            new[] { "Customer", "Venue", "Artist", "Admin" },
            environment
                .Where(pair => pair.Key.StartsWith("Auth__SpaClients__EnabledClients__", StringComparison.Ordinal))
                .OrderBy(pair => pair.Key)
                .Select(pair => Assert.IsType<string>(pair.Value)));
        var authClientKeys = environment.Keys
            .Where(key => key.StartsWith("Auth__SpaClients__", StringComparison.Ordinal)
                && !key.StartsWith("Auth__SpaClients__EnabledClients__", StringComparison.Ordinal))
            .Order()
            .ToArray();
        var expectedKeys = surfaces
            .Where(surface => surface.AuthClient is not null)
            .SelectMany(surface => new[]
            {
                $"Auth__SpaClients__{surface.AuthClient}__AllowedCorsOrigins__0",
                $"Auth__SpaClients__{surface.AuthClient}__PostLogoutRedirectUri",
                $"Auth__SpaClients__{surface.AuthClient}__RedirectUri"
            })
            .Order()
            .ToArray();

        Assert.Equal(expectedKeys, authClientKeys);
        foreach (var surface in surfaces.Where(surface => surface.AuthClient is not null))
        {
            Assert.Equal($"{surface.Origin}/auth/callback",
                environment[$"Auth__SpaClients__{surface.AuthClient}__RedirectUri"]);
            Assert.Equal(surface.Origin,
                environment[$"Auth__SpaClients__{surface.AuthClient}__PostLogoutRedirectUri"]);
            Assert.Equal(surface.Origin,
                environment[$"Auth__SpaClients__{surface.AuthClient}__AllowedCorsOrigins__0"]);
        }
        Assert.DoesNotContain(authClientKeys, key => key.Contains("__Business__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Build_MobileUrls_ResolveThroughOwnedTunnel()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>(
            ["--RunMobile=true", "--MobileLanIp=192.0.2.42"]);
        await using var app = await builder.BuildAsync();
        var ports = builder.Resources.OfType<Aspire.Hosting.DevTunnels.DevTunnelPortResource>().ToArray();
        Assert.NotEmpty(ports);
        foreach (var port in ports)
        {
            foreach (var endpoint in port.Annotations.OfType<EndpointAnnotation>())
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, $"{port.Name}.example.test", 443);
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var mobile in builder.Resources.OfType<NodeAppResource>()
            .Where(resource => resource.Name.StartsWith("mobile-", StringComparison.Ordinal)))
        {
            AssertClearMetroCacheCommand(mobile);
            var environment = await GetResolvedEnvironmentAsync(mobile, cancellation.Token);
            Assert.Equal("192.0.2.42", environment["REACT_NATIVE_PACKAGER_HOSTNAME"]);
            foreach (var (key, service) in new[]
            {
                ("EXPO_PUBLIC_API_URL", "b2b-web"),
                ("EXPO_PUBLIC_AUTH_AUTHORITY", "auth"),
                ("EXPO_PUBLIC_SEARCH_API_URL", "search-web"),
                ("EXPO_PUBLIC_CUSTOMER_API_URL", "customer-web"),
                ("EXPO_PUBLIC_PAYMENT_API_URL", "payment-web")
            })
            {
                Assert.Equal($"https://concertable-dev-{service}-https.example.test:443", environment[key]);
            }
        }

        var auth = builder.Resources.Single(resource => resource.Name == "auth");
        var authEnvironment = await GetRawEnvironmentAsync(auth, cancellation.Token);
        var publicUrl = await Assert.IsAssignableFrom<IValueProvider>(authEnvironment["Auth__PublicUrl"])
            .GetValueAsync(cancellation.Token);
        Assert.Equal("https://concertable-dev-auth-https.example.test:443", publicUrl);
    }

    private static async Task<Dictionary<string, string>> GetResolvedEnvironmentAsync(
        IResource resource, CancellationToken cancellationToken)
    {
        var environmentResource = Assert.IsAssignableFrom<IResourceWithEnvironment>(resource);
        var configuration = await ExecutionConfigurationBuilder.Create(environmentResource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                NullLogger.Instance, cancellationToken);
        return configuration.EnvironmentVariables.ToDictionary();
    }

    private static void AssertClearMetroCacheCommand(NodeAppResource mobile)
    {
        var command = Assert.Single(mobile.Annotations.OfType<ResourceCommandAnnotation>(),
            command => command.Name == "clear-metro-cache");
        Assert.Equal("Clear Metro Cache", command.DisplayName);
        Assert.Equal("ArrowCounterclockwise", command.IconName);
    }

    private static async Task<Dictionary<string, object>> GetRawEnvironmentAsync(
        IResource resource, CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource, environment, cancellationToken);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray())
            await annotation.Callback(context);
        return environment;
    }

    [Fact]
    public async Task Build_InvalidLifetimeGraph_IsRejected()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        builder.Services.AddInvalidLifetimeGraph();
        await Assert.ThrowsAnyAsync<Exception>(async () => await builder.BuildAsync());
    }

    [Fact]
    public void Inventory_AllExecutableProjectsDeclareCoverageOrExclusion()
    {
        var root = ExecutableHostInventory.FindRepositoryRoot();
        ExecutableHostInventory.Validate(Path.Combine(root, "api"),
            "Concertable.AppHost/Concertable.AppHost.csproj",
            "Concertable.Auth/src/Concertable.Auth.AppHost/Concertable.Auth.AppHost.csproj",
            "Concertable.Auth/src/Concertable.Auth/Concertable.Auth.csproj",
            "Concertable.B2B/src/Concertable.B2B.AppHost/Concertable.B2B.AppHost.csproj",
            "Concertable.B2B/src/Concertable.B2B.Web/Concertable.B2B.Web.csproj",
            "Concertable.B2B/src/Concertable.B2B.Workers/Concertable.B2B.Workers.csproj",
            "Concertable.B2B/src/Seed/Concertable.B2B.Seed.Simulator/Concertable.B2B.Seed.Simulator.csproj",
            "Concertable.Customer/src/Concertable.Customer.AppHost/Concertable.Customer.AppHost.csproj",
            "Concertable.Customer/src/Concertable.Customer.Web/Concertable.Customer.Web.csproj",
            "Concertable.Search/src/Concertable.Search.AppHost/Concertable.Search.AppHost.csproj",
            "Concertable.Search/src/Concertable.Search.Web/Concertable.Search.Web.csproj",
            "Concertable.Search/src/Concertable.Search.Workers/Concertable.Search.Workers.csproj",
            "Concertable.Payment/src/Concertable.Payment.AppHost/Concertable.Payment.AppHost.csproj",
            "Concertable.Payment/src/Concertable.Payment.Web/Concertable.Payment.Web.csproj",
            "Concertable.Payment/src/Concertable.Payment.Workers/Concertable.Payment.Workers.csproj");
    }
}
