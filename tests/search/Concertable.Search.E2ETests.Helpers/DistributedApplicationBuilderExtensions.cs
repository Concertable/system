using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Testing;
using Concertable.Auth.Hosting;
using Concertable.Messaging.AzureServiceBus.Options;

namespace Concertable.Search.E2ETests.Helpers;

/// <summary>
/// Adds the real Search service to an E2E composition. The standalone AppHosts deliberately do not
/// run Search (data services never run their peers), but both surfaces' find pages are Search-backed
/// (B2B's /find/venue, Customer's find page), so the E2E suites that drive them run it themselves.
/// </summary>
public static class DistributedApplicationBuilderExtensions
{
    private const string Database = "SearchDb";
    private const string WebResource = "search-web";
    private const string WorkersResource = "search-workers";
    private const string ServiceName = "concertable-search";

    extension(IDistributedApplicationBuilder builder)
    {
        public IDistributedApplicationBuilder AddSearchService(
            IProjectMetadata searchWebProject,
            IProjectMetadata searchWorkersProject,
            string searchApiBaseUrl,
            string authBaseUrl)
        {
            var sql = builder.Resources.OfType<SqlServerServerResource>().Single();
            var asb = builder.Resources.OfType<AzureServiceBusResource>().Single();
            var auth = builder.Resources
                .Single(r => r.Name == AuthConstants.Resource);

            var searchDb = builder.CreateResourceBuilder(sql).AddDatabase(Database);
            var authBuilder = builder.CreateResourceBuilder((IResourceWithServiceDiscovery)auth);
            var asbBuilder = builder.CreateResourceBuilder(asb);
            var searchApiUri = new Uri(searchApiBaseUrl);

            var searchWeb = builder.Resources.SingleOrDefault(resource => resource.Name == WebResource);
            if (searchWeb is null)
            {
                searchWeb = builder.AddResource(new ProjectResource(WebResource))
                    .WithAnnotation(searchWebProject)
                    .WithReference(searchDb)
                    .WaitFor(searchDb)
                    .WaitFor(authBuilder)
                    .Resource;
            }
            else
            {
                LaunchAs(searchWeb, searchWebProject);
            }

            PinHttpsEndpoint(builder, searchWeb, searchApiUri.Port);
            searchWeb.Annotations.Add(new HealthCheckAnnotation("/health", "https"));
            searchWeb.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "E2E";
                context.EnvironmentVariables["ASPNETCORE_URLS"] = searchApiBaseUrl;
                context.EnvironmentVariables["Auth__Authority"] = authBaseUrl;
            }));

            var searchWorkers = builder.Resources.SingleOrDefault(resource => resource.Name == WorkersResource);
            if (searchWorkers is null)
            {
                searchWorkers = builder.AddResource(new ProjectResource(WorkersResource))
                    .WithAnnotation(searchWorkersProject)
                    .WithReference(searchDb)
                    .WaitFor(searchDb)
                    .WithReference(asbBuilder)
                    .WaitFor(asbBuilder)
                    .WaitFor(builder.CreateResourceBuilder((IResourceWithWaitSupport)searchWeb))
                    .Resource;
            }
            else
            {
                LaunchAs(searchWorkers, searchWorkersProject);
            }

            searchWorkers.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables[AzureServiceBusOptions.ServiceNameEnvVar] = ServiceName;
                context.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "E2E";
            }));

            return builder;
        }
    }

    private static void LaunchAs(IResource resource, IProjectMetadata project)
    {
        if (resource is not ProjectResource)
            return;

        foreach (var metadata in resource.Annotations.OfType<IProjectMetadata>().ToList())
            resource.Annotations.Remove(metadata);
        resource.Annotations.Add(project);
    }

    private static void PinHttpsEndpoint(
        IDistributedApplicationBuilder builder,
        IResource resource,
        int port)
    {
        foreach (var endpoint in resource.Annotations
                     .OfType<EndpointAnnotation>()
                     .Where(endpoint => endpoint.Name == "https")
                     .ToList())
            resource.Annotations.Remove(endpoint);

        builder.CreateResourceBuilder((IResourceWithEndpoints)resource)
            .WithHttpsEndpoint(port: port, isProxied: false);
    }
}
