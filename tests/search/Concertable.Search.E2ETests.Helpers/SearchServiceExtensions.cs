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
public static class SearchServiceExtensions
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
            var auth = builder.Resources.OfType<ProjectResource>()
                .Single(r => r.Name == AuthConstants.Resource);

            var searchDb = builder.CreateResourceBuilder(sql).AddDatabase(Database);
            var authBuilder = builder.CreateResourceBuilder(auth);
            var asbBuilder = builder.CreateResourceBuilder(asb);
            var searchApiUri = new Uri(searchApiBaseUrl);

            var searchWeb = builder.AddResource(new ProjectResource(WebResource))
                .WithAnnotation(searchWebProject)
                .WithHttpsEndpoint(port: searchApiUri.Port, isProxied: false)
                .WithHttpHealthCheck("/health", endpointName: "https")
                .WithReference(searchDb)
                .WaitFor(searchDb)
                .WaitFor(authBuilder)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "E2E")
                .WithEnvironment("ASPNETCORE_URLS", searchApiBaseUrl)
                .WithEnvironment("Auth__Authority", authBaseUrl);

            builder.AddResource(new ProjectResource(WorkersResource))
                .WithAnnotation(searchWorkersProject)
                .WithReference(searchDb)
                .WaitFor(searchDb)
                .WithReference(asbBuilder)
                .WaitFor(asbBuilder)
                .WaitFor(searchWeb)
                .WithEnvironment(AzureServiceBusOptions.ServiceNameEnvVar, ServiceName)
                .WithEnvironment("DOTNET_ENVIRONMENT", "E2E");

            return builder;
        }
    }
}
