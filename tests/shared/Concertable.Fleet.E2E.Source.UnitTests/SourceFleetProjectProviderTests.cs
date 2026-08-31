using Concertable.Fleet.E2E.Source;

namespace Concertable.Fleet.E2E.Source.UnitTests;

public sealed class SourceFleetProjectProviderTests
{
    private readonly SourceFleetProjectProvider provider;

    public SourceFleetProjectProviderTests()
    {
        this.provider = new SourceFleetProjectProvider();
    }

    [Fact]
    public void SearchWeb_SourceMode_ReferencesSearchWebProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Web", "Concertable.Search.Web.csproj"),
            this.provider.SearchWeb.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchWorkers_SourceMode_ReferencesSearchWorkersProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Workers", "Concertable.Search.Workers.csproj"),
            this.provider.SearchWorkers.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
