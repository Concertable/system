using Reqnroll;

namespace Concertable.Testing.E2E.Ui;

public static class ScenarioContextExtensions
{
    public static bool HasTag(this ScenarioContext scenario, string tag) =>
        scenario.ScenarioInfo.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
}
