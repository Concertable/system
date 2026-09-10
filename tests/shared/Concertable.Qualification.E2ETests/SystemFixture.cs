using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.AppHost;
using Concertable.Testing.E2E;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Concertable.Qualification.E2ETests;

public sealed class SystemFixture : IAsyncLifetime
{
    private static readonly TimeSpan BootTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(2);

    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<SystemFixture> logger;

    private DistributedApplication? application;
    private AspireResourceLogger? resourceLogger;
    private HttpClient? client;
    private HttpClient? probeClient;

    public SystemFixture()
    {
        this.loggerFactory = LoggerFactory.Create(builder => builder
            .AddSimpleConsole(options => options.SingleLine = true)
            .AddProvider(new FileLoggerProvider(
                Path.Combine(AppContext.BaseDirectory, "qualification-diagnostics.log")))
            .SetMinimumLevel(LogLevel.Information));
        this.logger = this.loggerFactory.CreateLogger<SystemFixture>();
    }

    public CompatibilityManifest Manifest { get; private set; } = null!;

    public IReadOnlyDictionary<string, Uri> HttpServices { get; private set; } =
        new Dictionary<string, Uri>();

    public HttpClient Client => this.client
        ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async Task InitializeAsync()
    {
        this.Manifest = CompatibilityManifest.Load(AppContext.BaseDirectory);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();

        // Resolved through the image rather than the resource name, which need not agree with the
        // manifest key: B2B's workers resource is "workers" while its image is b2b-workers.
        var byImage = builder.Resources
            .OfType<ServiceContainerResource>()
            .ToDictionary(
                resource => resource.Annotations.OfType<ContainerImageAnnotation>().Single().Image,
                resource => resource,
                StringComparer.Ordinal);

        var resourceNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var service in this.Manifest.ServiceNames)
        {
            var repository = this.Manifest[service].Repository;
            if (!byImage.TryGetValue(repository, out var resource))
                throw new InvalidOperationException($"The composition runs no container for '{repository}'.");

            resourceNames.Add(service, resource.Name);

            // The images expose /health only in the Development or E2E environment. Configuring the
            // container is deployment, not a change to the artefact under test.
            builder.CreateResourceBuilder(resource)
                   .WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E")
                   .WithEnvironment("DOTNET_ENVIRONMENT", "E2E");
        }

        this.application = await builder.BuildAsync();

        // Started before StartAsync so a container that dies during boot is diagnosable. Without it a
        // composition that never answers fails as a bare timeout with nothing said about why.
        this.resourceLogger = new AspireResourceLogger(
            this.application.ResourceNotifications,
            this.application.Services.GetRequiredService<ResourceLoggerService>(),
            this.logger);

        await this.application.StartAsync();

        this.client = CreateClient(timeout: null);
        this.probeClient = CreateClient(ProbeTimeout);

        var endpoints = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var (service, resourceName) in resourceNames)
        {
            if (TryGetHttpEndpoint(this.application, resourceName) is { } endpoint)
                endpoints.Add(service, endpoint);
        }

        this.HttpServices = endpoints;

        using var cancellation = new CancellationTokenSource(BootTimeout);
        var probes = await Task.WhenAll(endpoints.Select(
            entry => PollUntilHealthyAsync(entry.Key, new Uri(entry.Value, "health"), cancellation.Token)));

        if (probes.OfType<string>().ToList() is { Count: > 0 } unhealthy)
            throw new TimeoutException(
                $"The pinned composition never became healthy within {BootTimeout}. Last error per service: "
                + string.Join("; ", unhealthy)
                + ". Container output is in qualification-diagnostics.log.");
    }

    public async Task DisposeAsync()
    {
        this.client?.Dispose();
        this.probeClient?.Dispose();
        if (this.application is not null)
            await this.application.DisposeAsync();
        if (this.resourceLogger is not null)
            await this.resourceLogger.DisposeAsync();
        this.loggerFactory.Dispose();
    }

    private static HttpClient CreateClient(TimeSpan? timeout)
    {
        var created = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, _, _, _) =>
                message.RequestUri?.IsLoopback == true,
        });

        if (timeout is { } value)
            created.Timeout = value;

        return created;
    }

    private static Uri? TryGetHttpEndpoint(DistributedApplication application, string resourceName)
    {
        try
        {
            return application.GetEndpoint(resourceName, "https");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Polls one probe until it answers, returning null on success and the last error otherwise.
    /// A resource whose port is proxied but whose backend is not listening accepts the connection and never
    /// answers, so every attempt carries its own timeout and every failure is caught: letting the HttpClient
    /// timeout escape as TaskCanceledException abandoned the whole boot wait after a single attempt and
    /// reported it as a cancelled request rather than as an unhealthy service.</summary>
    private async Task<string?> PollUntilHealthyAsync(string service, Uri url, CancellationToken cancellationToken)
    {
        var lastError = "never attempted";
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var response = await this.probeClient!.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return null;

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                lastError = $"no response within {ProbeTimeout}";
            }
            catch (HttpRequestException exception)
            {
                lastError = exception.Message;
            }

            this.logger.QualificationProbeFailed(service, url.ToString(), lastError);

            try
            {
                await Task.Delay(ProbeInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return $"{service} ({url}) → {lastError}";
    }
}

[CollectionDefinition(Name)]
public sealed class SystemCollection : ICollectionFixture<SystemFixture>
{
    public const string Name = "system";
}
