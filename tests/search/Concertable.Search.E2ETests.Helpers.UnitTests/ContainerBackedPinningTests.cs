using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Concertable.Testing.E2E;

namespace Concertable.Search.E2ETests.Helpers.UnitTests;

public sealed class ContainerBackedPinningTests
{
    [Fact]
    public void SubstituteE2EProject_ImageBackedPaymentResource_RunsPaymentOwnedProjectAndRetargetsWaits()
    {
        var builder = DistributedApplication.CreateBuilder();
        var payment = builder.AddContainer(PaymentConstants.WebResource, "test-image")
            .WithHttpsEndpoint(port: 7098, targetPort: 8080)
            .WithEnvironment("ConnectionStrings__PaymentDb", "test-connection")
            .Resource;
        var dependent = builder.AddResource(new ProjectResource("dependent"))
            .WaitFor(builder.CreateResourceBuilder((IResourceWithWaitSupport)payment))
            .Resource;
        var metadata = new TestProjectMetadata("payment-e2e-web.csproj");

        var e2ePayment = Concertable.Testing.E2E.DistributedApplicationBuilderExtensions
            .SubstituteE2EProject(builder, payment, metadata);
        Concertable.Testing.E2E.DistributedApplicationBuilderExtensions
            .PinHttpsEndpoint(builder, e2ePayment, 7098);

        var project = Assert.IsType<ProjectResource>(e2ePayment);
        Assert.Equal("payment-web-e2e", project.Name);
        Assert.Same(metadata, Assert.Single(project.Annotations.OfType<IProjectMetadata>()));
        Assert.Single(payment.Annotations.OfType<ExplicitStartupAnnotation>());
        Assert.DoesNotContain(
            payment.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Port == 7098);
        Assert.Single(
            builder.Resources.SelectMany(resource => resource.Annotations.OfType<EndpointAnnotation>()),
            annotation => annotation.Port == 7098);
        Assert.Equal("test-connection", Environment(project)["ConnectionStrings__PaymentDb"]);
        Assert.DoesNotContain(
            dependent.Annotations.OfType<WaitAnnotation>(),
            annotation => ReferenceEquals(annotation.Resource, payment));
        Assert.Contains(
            dependent.Annotations.OfType<WaitAnnotation>(),
            annotation => ReferenceEquals(annotation.Resource, project));
    }

    [Fact]
    public void PinHttpsEndpoint_ImageBackedAuthResource_PublishesTheContractPortProxyless()
    {
        var builder = DistributedApplication.CreateBuilder();
        var auth = builder.AddContainer(AuthConstants.Resource, "test-image")
            .WithHttpsEndpoint(targetPort: 8080, name: "https")
            .Resource;

        Concertable.Testing.E2E.DistributedApplicationBuilderExtensions
            .PinHttpsEndpoint(builder, auth, 7083);

        var endpoint = Assert.Single(
            auth.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Name == "https");
        Assert.Equal(7083, endpoint.Port);
        Assert.Equal(8080, endpoint.TargetPort);

        // DCP honours a declared public port under RandomizePorts only for a proxyless endpoint, and the
        // Aspire testing builder always sets RandomizePorts.
        Assert.False(endpoint.IsProxied);
    }

    [Fact]
    public void SubstituteE2EProject_ImageBackedWorkers_CarriesConnectionStringReferences()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sql = builder.AddSqlServer("sql");
        var paymentDb = sql.AddDatabase("PaymentDb");
        var asb = builder.AddAzureServiceBus("asb");
        var workers = builder.AddContainer(PaymentConstants.WorkersResource, "test-image")
            .WithReference(paymentDb).WaitFor(paymentDb)
            .WithReference(asb).WaitFor(asb)
            .Resource;

        var e2eWorkers = Concertable.Testing.E2E.DistributedApplicationBuilderExtensions
            .SubstituteE2EProject(builder, workers, new TestProjectMetadata("payment-workers-e2e.csproj"));

        var environment = Environment(e2eWorkers);
        Assert.Contains("ConnectionStrings__asb", environment.Keys);
        Assert.Contains("ConnectionStrings__PaymentDb", environment.Keys);

        // The substituted project must reference the connection-string resources so Aspire resolves
        // them at launch — copying the env callbacks alone leaves ConnectionStrings:asb null and the
        // Payment workers host throws on startup.
        var referenced = e2eWorkers.Annotations.OfType<ResourceRelationshipAnnotation>()
            .Select(relationship => relationship.Resource.Name)
            .ToList();
        Assert.Contains("asb", referenced);
        Assert.Contains("PaymentDb", referenced);
    }

    [Fact]
    public void AddSearchService_ImageBackedResources_PreservesContainersAndPinsE2EConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        var digest = $"sha256:{new string('a', 64)}";
        builder.AddSqlServer("sql");
        builder.AddAzureServiceBus("messaging");
        builder.AddContainerImage(AuthConstants.Resource, "test-image", digest);
        var searchWeb = builder.AddContainerImage("search-web", "test-image", digest).Resource;

        var searchWorkers = builder.AddContainerImage("search-workers", "test-image", digest).Resource;

        builder.AddSearchService(
            new TestProjectMetadata("search-web.csproj"),
            new TestProjectMetadata("search-workers.csproj"),
            "https://localhost:7097",
            "https://localhost:7096");

        Assert.Same(searchWeb, builder.Resources.Single(resource => resource.Name == "search-web"));
        Assert.Same(searchWorkers, builder.Resources.Single(resource => resource.Name == "search-workers"));
        Assert.Single(searchWeb.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Single(searchWorkers.Annotations.OfType<ContainerImageAnnotation>());

        var endpoint = Assert.Single(
            searchWeb.Annotations.OfType<EndpointAnnotation>(),
            annotation => annotation.Name == "https");
        Assert.Equal(7097, endpoint.Port);
        Assert.False(endpoint.IsProxied);

        var webEnvironment = Environment(searchWeb);
        Assert.Equal("E2E", webEnvironment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("https://localhost:7097", webEnvironment["ASPNETCORE_URLS"]);
        Assert.Equal("https://localhost:7096", webEnvironment["Auth__Authority"]);

        var workersEnvironment = Environment(searchWorkers);
        Assert.Equal("E2E", workersEnvironment["DOTNET_ENVIRONMENT"]);
        Assert.Equal("concertable-search", workersEnvironment["ServiceBus__ServiceName"]);
    }

    private static Dictionary<string, object> Environment(IResource resource)
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environment,
            CancellationToken.None);

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            annotation.Callback(context);

        return environment;
    }

    private sealed class TestProjectMetadata(string projectPath) : IProjectMetadata
    {
        public string ProjectPath { get; } = projectPath;
    }
}
