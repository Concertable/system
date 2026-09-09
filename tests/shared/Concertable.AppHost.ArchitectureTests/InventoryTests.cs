using Concertable.Testing.Architecture;
using Xunit;

namespace Concertable.AppHost.ArchitectureTests;

public sealed class InventoryTests
{
    [Fact]
    public void AllExecutableProjects_DeclareCoverageOrExclusion() =>
        ExecutableHostInventory.Validate(
            Path.Combine(RepositoryRoot(), "src"),
            "Concertable.AppHost/Concertable.AppHost.csproj");

    // ExecutableHostInventory.FindRepositoryRoot only recognises a root containing `api/`, which is the
    // monorepo's shape and not this one's.
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Concertable.System.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException(
            $"Could not locate Concertable.System.slnx walking up from '{AppContext.BaseDirectory}'.");
    }
}
