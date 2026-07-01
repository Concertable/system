using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.Hooks;

[Binding]
public sealed class AppiumHooks
{
    private readonly MobileApp app;
    private readonly ScenarioContext scenarioContext;

    public AppiumHooks(MobileApp app, ScenarioContext scenarioContext)
    {
        this.app = app;
        this.scenarioContext = scenarioContext;
    }

    [BeforeScenario(Order = 10)]
    public Task BeforeScenario() =>
        app.InitializeAsync(EmulatorHooks.Fixture);

    [AfterScenario]
    public async Task AfterScenario()
    {
        if (scenarioContext.TestError is not null)
            app.SaveScreenshotOnFailure(scenarioContext.ScenarioInfo.Title);
        await app.DisposeAsync();
    }
}
