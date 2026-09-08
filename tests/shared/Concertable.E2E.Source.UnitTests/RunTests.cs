using System.Globalization;
using Concertable.E2E;

namespace Concertable.E2E.Source.UnitTests;

public sealed class RunTests
{
    [Fact]
    public void AuthEnvironmentVariables_RelaxesCredentialLimitAcrossIsolatedScenarios()
    {
        var environment = Run.AuthEnvironmentVariables();

        Assert.Equal(
            int.MaxValue.ToString(CultureInfo.InvariantCulture),
            environment["RateLimiting__credential__PermitLimit"]);
    }
}
