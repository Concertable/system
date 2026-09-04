using Concertable.Testing.Architecture;
using Xunit;

namespace Concertable.AppHost.ArchitectureTests;

public sealed class InventoryTests
{
    [Fact]
    public void AllExecutableProjects_DeclareCoverageOrExclusion()
    {
        var root = ExecutableHostInventory.FindRepositoryRoot();
        ExecutableHostInventory.Validate(Path.Combine(root, "api"),
            "Concertable.AppHost/Concertable.AppHost.csproj",
            "Concertable.Auth/src/Concertable.Auth.AppHost/Concertable.Auth.AppHost.csproj",
            "Concertable.Auth/src/Concertable.Auth/Concertable.Auth.csproj",
            "Concertable.B2B/src/Concertable.B2B.AppHost/Concertable.B2B.AppHost.csproj",
            "Concertable.B2B/src/Concertable.B2B.Web/Concertable.B2B.Web.csproj",
            "Concertable.B2B/src/Concertable.B2B.Workers/Concertable.B2B.Workers.csproj",
            "Concertable.B2B/src/Seed/Concertable.B2B.Seed.Simulator/Concertable.B2B.Seed.Simulator.csproj",
            "Concertable.Customer/src/Concertable.Customer.AppHost/Concertable.Customer.AppHost.csproj",
            "Concertable.Customer/src/Concertable.Customer.Web/Concertable.Customer.Web.csproj",
            "Concertable.Search/src/Concertable.Search.AppHost/Concertable.Search.AppHost.csproj",
            "Concertable.Search/src/Concertable.Search.Web/Concertable.Search.Web.csproj",
            "Concertable.Search/src/Concertable.Search.Workers/Concertable.Search.Workers.csproj",
            "Concertable.Payment/src/Concertable.Payment.AppHost/Concertable.Payment.AppHost.csproj",
            "Concertable.Payment/src/Concertable.Payment.Web/Concertable.Payment.Web.csproj",
            "Concertable.Payment/src/Concertable.Payment.Workers/Concertable.Payment.Workers.csproj");
    }
}
