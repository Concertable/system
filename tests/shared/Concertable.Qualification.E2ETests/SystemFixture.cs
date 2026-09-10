using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Concertable.AppHost;
using Concertable.B2B.Hosting;
using Concertable.Customer.Hosting;
using Concertable.E2E;
using Concertable.Testing.E2E;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Concertable.Qualification.E2ETests;

public sealed class SystemFixture : IAsyncLifetime
{
    private const char LineFeed = (char)10;

    private readonly ConcurrentDictionary<string, int> firstChanceExceptions = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ServiceClientSecrets = new(StringComparer.Ordinal)
    {
        [B2BConstants.WebResource] = Run.B2BServiceAuthSecret,
        [B2BConstants.WorkersResource] = Run.B2BServiceAuthSecret,
        [CustomerConstants.WebResource] = Run.CustomerServiceAuthSecret,
    };

    private static readonly TimeSpan BootTimeout = TimeSpan.FromMinutes(20);

    private static readonly string[] TerminalFailureStates =
    [
        KnownResourceStates.FailedToStart,
        KnownResourceStates.Exited,
        KnownResourceStates.RuntimeUnhealthy,
    ];

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
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Aspire.Hosting", LogLevel.Debug)
            .AddFilter("Aspire.Hosting.Dcp", LogLevel.Trace));
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

        // Auth refuses to start without its service-client secrets, and AddSecrets forwards them only
        // when the composition already holds a value. Supplying them as command-line configuration keeps
        // the fixed E2E credentials in the harness rather than in the composition itself.
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>(
            [.. Run.AuthEnvironmentVariables()
                  .Select(setting => $"--{setting.Key.Replace("__", ":", StringComparison.Ordinal)}={setting.Value}")]);

        // The orchestrator states a resource is FailedToStart but says why only through its own logger,
        // which no resource log stream carries. Without this the reason a container never ran is lost.
        builder.Services.AddLogging(logging => logging
            .AddProvider(new FileLoggerProvider(
                Path.Combine(AppContext.BaseDirectory, "apphost-diagnostics.log")))
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Aspire.Hosting", LogLevel.Debug)
            .AddFilter("Aspire.Hosting.Dcp", LogLevel.Trace));

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
            var configured = builder.CreateResourceBuilder(resource)
                   .WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E")
                   .WithEnvironment("DOTNET_ENVIRONMENT", "E2E");

            // Each service authenticates to Auth as its own client, so its secret has to be the one Auth
            // was told to expect for it.
            if (ServiceClientSecrets.GetValueOrDefault(resource.Name) is { } clientSecret)
                configured.WithEnvironment("ServiceAuth__ClientSecret", clientSecret);
        }

        // The orchestrator marks a resource FailedToStart and discards the exception behind it, and
        // that resource never reaches DCP, so nothing downstream can report the cause. A first-chance
        // handler sees the throw itself.
        AppDomain.CurrentDomain.FirstChanceException += this.OnFirstChanceException;

        this.application = await builder.BuildAsync();

        // Started before StartAsync so a container that dies during boot is diagnosable. Without it a
        // composition that never answers fails as a bare timeout with nothing said about why.
        this.resourceLogger = new AspireResourceLogger(
            this.application.ResourceNotifications,
            this.application.Services.GetRequiredService<ResourceLoggerService>(),
            this.logger);

        await LogEnvironmentResolutionAsync(resourceNames, byImage, this.logger);

        await this.application.StartAsync();

        this.client = CreateClient(timeout: null);
        this.probeClient = CreateClient(ProbeTimeout);

        var endpoints = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var (service, resourceName) in resourceNames)
        {
            if (FindHttpEndpoint(this.application, resourceName) is { } endpoint)
                endpoints.Add(service, endpoint);
        }

        this.HttpServices = endpoints;

        using var cancellation = new CancellationTokenSource(BootTimeout);
        using var stopWatching = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);

        // A resource that reaches a terminal state will never answer, so probing it for the rest of the
        // boot timeout only delays the report: three runs spent twenty minutes each polling an Auth
        // container that had already failed within a minute.
        var failures = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var watching = this.WatchForTerminalFailureAsync(
            resourceNames, failures, cancellation, stopWatching.Token);

        string?[] probes;
        try
        {
            probes = await Task.WhenAll(endpoints.Select(
                entry => PollUntilHealthyAsync(entry.Key, new Uri(entry.Value, "health"), cancellation.Token)));
        }
        finally
        {
            await stopWatching.CancelAsync();
            await watching;
        }

        if (!failures.IsEmpty)
        {
            this.LogFirstChanceExceptions();
            throw new InvalidOperationException(
                "The pinned composition could not start: "
                + string.Join("; ", failures.Select(failure => $"{failure.Key} → {failure.Value}"))
                + ". The orchestrator's own output is in apphost-diagnostics.log; container output is in"
                + " qualification-diagnostics.log.");
        }

        if (probes.OfType<string>().ToList() is { Count: > 0 } unhealthy)
            throw new TimeoutException(
                $"The pinned composition never became healthy within {BootTimeout}. Last error per service: "
                + string.Join("; ", unhealthy)
                + ". Container output is in qualification-diagnostics.log.");
    }

    /// <summary>Resolves each pinned resource's environment before the composition starts. A callback that
    /// throws here fails the orchestrator's BeforeResourceStartedEvent instead, which records only
    /// FailedToStart and discards the exception, leaving no reason anywhere for a container that never ran.
    /// Diagnostic only: an endpoint that is legitimately unallocated this early must not fail the run.</summary>
    private static async Task LogEnvironmentResolutionAsync(
        IReadOnlyDictionary<string, string> resourceNames,
        IReadOnlyDictionary<string, ServiceContainerResource> byImage,
        ILogger logger)
    {
        var byName = byImage.Values.ToDictionary(resource => resource.Name, StringComparer.Ordinal);

        foreach (var resourceName in resourceNames.Values)
        {
            if (!byName.TryGetValue(resourceName, out var resource))
                continue;

            try
            {
#pragma warning disable CS0618 // the replacement builder does not expose a resolve-and-throw path
                var environment = await resource.GetEnvironmentVariableValuesAsync();
#pragma warning restore CS0618
                logger.QualificationEnvironmentResolved(resourceName, environment.Count);

                logger.QualificationResourceAnnotations(
                    resourceName,
                    string.Join(", ", resource.Annotations
                        .Select(annotation => annotation.GetType().Name)
                        .OrderBy(name => name, StringComparer.Ordinal)));

                foreach (var mount in resource.Annotations.OfType<ContainerMountAnnotation>())
                    logger.QualificationContainerMount(
                        resourceName,
                        mount.Source ?? "(none)",
                        mount.Target,
                        mount.Source is null || Path.Exists(mount.Source));
            }
            catch (Exception exception)
            {
                logger.QualificationEnvironmentUnresolved(resourceName, exception);
            }
        }
    }

    /// <summary>Counts every first-chance exception by type and origin, because filtering by frame missed
    /// the one that matters: a first-chance stack reaches only the throw site, so an exception raised inside
    /// the Kubernetes client that creates the DCP object carries none of the orchestrator's frames.
    /// Deduplicating instead of logging each occurrence keeps the emulators' hundreds of transport retries
    /// from burying it.</summary>
    private void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
    {
        var origin = args.Exception.StackTrace?.Split(LineFeed).FirstOrDefault()?.Trim() ?? "(no stack)";
        var key = $"{args.Exception.GetType().Name}: {Truncate(args.Exception.Message)} @ {origin}";
        this.firstChanceExceptions.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : value[..200];

    private void LogFirstChanceExceptions()
    {
        foreach (var (exception, count) in this.firstChanceExceptions.OrderByDescending(entry => entry.Value))
            this.logger.QualificationFirstChanceException(count, exception);
    }

    private async Task WatchForTerminalFailureAsync(
        IReadOnlyDictionary<string, string> resourceNames,
        ConcurrentDictionary<string, string> failures,
        CancellationTokenSource boot,
        CancellationToken cancellationToken)
    {
        var services = resourceNames.ToDictionary(
            entry => entry.Value, entry => entry.Key, StringComparer.Ordinal);

        try
        {
            await foreach (var change in this.application!.ResourceNotifications.WatchAsync(cancellationToken))
            {
                if (!services.TryGetValue(change.Resource.Name, out var service))
                    continue;

                if (change.Snapshot.State?.Text is not { } state
                    || !TerminalFailureStates.Contains(state, StringComparer.Ordinal))
                    continue;

                failures[service] = change.Snapshot.ExitCode is { } exitCode
                    ? $"{state} (exit code {exitCode})"
                    : state;

                await boot.CancelAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task DisposeAsync()
    {
        AppDomain.CurrentDomain.FirstChanceException -= this.OnFirstChanceException;
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

    private static Uri? FindHttpEndpoint(DistributedApplication application, string resourceName) =>
        FindEndpoint(application, resourceName, "https")
        ?? FindEndpoint(application, resourceName, "http");

    private static Uri? FindEndpoint(DistributedApplication application, string resourceName, string endpointName)
    {
        try
        {
            return application.GetEndpoint(resourceName, endpointName);
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
