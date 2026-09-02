using Concertable.E2E;
using Concertable.E2E.Source;

namespace Concertable.E2E.Source.UnitTests;

public sealed class SourceCompositionTests
{
    private readonly SourceComposition composition;

    public SourceCompositionTests()
    {
        this.composition = new SourceComposition();
    }

    [Fact]
    public void Source_ResolvesSourceCompositionByAssemblyName()
    {
        Assert.IsType<SourceComposition>(Compositions.Source());
    }

    [Fact]
    public void SearchWeb_SourceMode_ReferencesSearchWebProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Web", "Concertable.Search.Web.csproj"),
            this.composition.SearchWeb.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchWorkers_SourceMode_ReferencesSearchWorkersProject()
    {
        Assert.EndsWith(
            Path.Combine("Concertable.Search", "src", "Concertable.Search.Workers", "Concertable.Search.Workers.csproj"),
            this.composition.SearchWorkers.ProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
