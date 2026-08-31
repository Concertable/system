using Concertable.SystemTesting.E2E.Source;

namespace Concertable.SystemTesting.E2E.Source.UnitTests;

public sealed class SourceSystemAppHostFactoryTests
{
    private readonly SourceSystemAppHostFactory factory;

    public SourceSystemAppHostFactoryTests()
    {
        this.factory = new SourceSystemAppHostFactory();
    }

    [Fact]
    public void SearchWeb_SourceMode_ReferencesSearchWebProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Web", "Concertable.Search.Web.csproj"),
            this.factory.SearchWeb.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchWorkers_SourceMode_ReferencesSearchWorkersProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Workers", "Concertable.Search.Workers.csproj"),
            this.factory.SearchWorkers.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
