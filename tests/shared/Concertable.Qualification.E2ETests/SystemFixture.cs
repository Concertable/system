using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.AppHost;
using Xunit;

namespace Concertable.Qualification.E2ETests;

public sealed class SystemFixture : IAsyncLifetime
{
    private static readonly TimeSpan BootTimeout = TimeSpan.FromMinutes(15);

    private DistributedApplication? application;
    private HttpClient? client;

    public CompatibilityManifest Manifest { get; private set; } = null!;

    public IReadOnlyDictionary<string, Uri> HttpServices { get; private set; } =
        new Dictionary<string, Uri>();

    public HttpClient Client => this.client
        ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async Task InitializeAsync()
    {
        this.Manifest = CompatibilityManifest.Load(AppContext.BaseDirectory);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();

        // The images expose /health only in the Development or E2E environment. Configuring the
        // container is deployment, not a change to the artefact under test.
        foreach (var service in this.Manifest.ServiceNames)
        {
            var resource = builder.Resources
                .OfType<ServiceContainerResource>()
                .Single(candidate => candidate.Name == service);

            builder.CreateResourceBuilder(resource)
                   .WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E")
                   .WithEnvironment("DOTNET_ENVIRONMENT", "E2E");
        }

        this.application = await builder.BuildAsync();
        await this.application.StartAsync();

        this.client = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, _, _, _) =>
                message.RequestUri?.IsLoopback == true,
        });

        var endpoints = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var service in this.Manifest.ServiceNames)
        {
            if (TryGetHttpEndpoint(this.application, service) is { } endpoint)
                endpoints.Add(service, endpoint);
        }

        this.HttpServices = endpoints;

        using var cancellation = new CancellationTokenSource(BootTimeout);
        await Task.WhenAll(endpoints.Values.Select(
            endpoint => PollUntilHealthyAsync(new Uri(endpoint, "health"), cancellation.Token)));
    }

    public async Task DisposeAsync()
    {
        this.client?.Dispose();
        if (this.application is not null)
            await this.application.DisposeAsync();
    }

    private static Uri? TryGetHttpEndpoint(DistributedApplication application, string service)
    {
        try
        {
            return application.GetEndpoint(service, "https");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task PollUntilHealthyAsync(Uri url, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await this.Client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SystemCollection : ICollectionFixture<SystemFixture>
{
    public const string Name = "system";
}
