using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Concertable.Auth.Hosting;

namespace Concertable.Search.E2ETests.Helpers.UnitTests;

public sealed class DistributedApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddSearchService_DistinctProjectMetadata_AttachesToMatchingResources()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSqlServer("sql");
        builder.AddAzureServiceBus("messaging");
        builder.AddResource(new ProjectResource(AuthConstants.Resource))
            .WithAnnotation(new TestProjectMetadata("auth.csproj"));
        var searchWebProject = new TestProjectMetadata("search-web.csproj");
        var searchWorkersProject = new TestProjectMetadata("search-workers.csproj");

        builder.AddSearchService(
            searchWebProject,
            searchWorkersProject,
            "https://localhost:7097",
            "https://localhost:7096");

        var searchWeb = builder.Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == "search-web");
        var searchWorkers = builder.Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == "search-workers");
        Assert.Same(searchWebProject, Assert.Single(searchWeb.Annotations.OfType<IProjectMetadata>()));
        Assert.Same(searchWorkersProject, Assert.Single(searchWorkers.Annotations.OfType<IProjectMetadata>()));
    }

    private sealed class TestProjectMetadata : IProjectMetadata
    {
        public TestProjectMetadata(string projectPath)
        {
            this.ProjectPath = projectPath;
        }

        public string ProjectPath { get; }
    }
}
