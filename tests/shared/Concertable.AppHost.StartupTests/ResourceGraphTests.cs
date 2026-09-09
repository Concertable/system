using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.Testing.Architecture;
using Xunit;

namespace Concertable.AppHost.StartupTests;

public sealed class ResourceGraphTests
{
    [Fact]
    public async Task EveryServiceContainer_RunsAPinnedImageAtItsPinnedDigest()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();

        Assert.Empty(builder.Resources.OfType<ProjectResource>());
        Assert.Empty(builder.Resources.OfType<NodeAppResource>());

        var manifest = CompatibilityManifest.Load(AppContext.BaseDirectory);
        var pinned = manifest.ServiceNames.ToDictionary(
            service => manifest[service].Repository,
            service => manifest[service].Digest["sha256:".Length..],
            StringComparer.Ordinal);

        // Matched on image rather than resource name: the manifest is keyed by image, and a resource
        // name does not have to agree with it — B2B's workers resource is "workers", not "b2b-workers".
        var composed = builder.Resources
            .OfType<ServiceContainerResource>()
            .Select(resource => (
                resource.Name,
                Image: Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>().ToArray())))
            .ToArray();

        Assert.Equal(pinned.Count, composed.Length);
        foreach (var (name, image) in composed)
        {
            Assert.True(pinned.ContainsKey(image.Image), $"{name} runs unpinned image '{image.Image}'.");
            Assert.Equal(pinned[image.Image], image.SHA256);
        }

        await using var app = await builder.BuildAsync();
    }

    [Fact]
    public async Task ProductionGraph_IsValid()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "concertable-dev");
        var auth = builder.Resources.Single(resource => resource.Name == "auth");
        var authEnvironment = await GetRawEnvironmentAsync(auth, CancellationToken.None);
        Assert.DoesNotContain("Auth__PublicUrl", authEnvironment.Keys);
        await using var app = await builder.BuildAsync();
    }

    [Fact]
    public async Task AuthSpaClients_AreOwnedAndCollisionFree()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        var surfaces = SystemLocalSpaSurfaces.All;
        Assert.Equal(5, surfaces.Count);
        Assert.Equal(surfaces.Count, surfaces.Select(surface => surface.ResourceName).Distinct().Count());
        Assert.Equal(surfaces.Count, surfaces.Select(surface => surface.HttpsPort).Distinct().Count());
        await using var app = await builder.BuildAsync();

        var auth = builder.Resources.Single(resource => resource.Name == "auth");
        var environment = await GetRawEnvironmentAsync(auth, CancellationToken.None);
        Assert.Equal("true", environment["Auth__SpaClients__RestrictToEnabledClients"]);
        Assert.Equal(
            new[] { "Customer", "Venue", "Artist", "Admin" },
            environment
                .Where(pair => pair.Key.StartsWith("Auth__SpaClients__EnabledClients__", StringComparison.Ordinal))
                .OrderBy(pair => pair.Key)
                .Select(pair => Assert.IsType<string>(pair.Value)));
        var authClientKeys = environment.Keys
            .Where(key => key.StartsWith("Auth__SpaClients__", StringComparison.Ordinal)
                && !key.StartsWith("Auth__SpaClients__EnabledClients__", StringComparison.Ordinal)
                && key != "Auth__SpaClients__RestrictToEnabledClients")
            .Order()
            .ToArray();
        var expectedKeys = SystemLocalSpaSurfaces.AuthClients
            .SelectMany(registration => new[]
            {
                $"Auth__SpaClients__{registration.ClientName}__AllowedCorsOrigins__0",
                $"Auth__SpaClients__{registration.ClientName}__PostLogoutRedirectUri",
                $"Auth__SpaClients__{registration.ClientName}__RedirectUri"
            })
            .Order()
            .ToArray();

        Assert.Equal(expectedKeys, authClientKeys);
        foreach (var (surface, clientName) in SystemLocalSpaSurfaces.AuthClients)
        {
            Assert.Equal($"{surface.Origin}/auth/callback",
                environment[$"Auth__SpaClients__{clientName}__RedirectUri"]);
            Assert.Equal(surface.Origin,
                environment[$"Auth__SpaClients__{clientName}__PostLogoutRedirectUri"]);
            Assert.Equal(surface.Origin,
                environment[$"Auth__SpaClients__{clientName}__AllowedCorsOrigins__0"]);
        }
        Assert.DoesNotContain(authClientKeys, key => key.Contains("__Business__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidLifetimeGraph_IsRejected()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        builder.Services.AddInvalidLifetimeGraph();
        await Assert.ThrowsAnyAsync<Exception>(async () => await builder.BuildAsync());
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
}
