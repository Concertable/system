using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Hooks;

[Binding]
public sealed class PlaywrightHooks
{
    public static UiFixture Fixture { get; private set; } = null!;

    [BeforeTestRun(Order = 1)]
    public static async Task BeforeRun()
    {
        Fixture = new UiFixture();
        await Fixture.InitializeAsync();
    }

    [AfterTestRun]
    public static Task AfterRun() => Fixture.DisposeAsync();

    private readonly Browser browser;
    private readonly UiFixture fixture;

    public PlaywrightHooks(Browser browser, UiFixture fixture)
    {
        this.browser = browser;
        this.fixture = fixture;
    }

    [BeforeScenario(Order = 1)]
    public async Task BeforeScenario(ScenarioContext scenarioContext)
    {
        await fixture.App.ResetAsync();
        LoginCaptureHooks.Reset();

        var tags = scenarioContext.ScenarioInfo.Tags;
        var isSignUp = tags.Contains("SignUp");

        var role = isSignUp ? null : tags
            .Select(tag => Enum.TryParse<Role>(tag, out var r) ? (Role?)r : null)
            .FirstOrDefault(r => r is not null);

        await browser.InitializeAsync(fixture.Browser, role, fixture);
    }

    [AfterScenario]
    public async Task AfterScenario(ScenarioContext scenarioContext)
    {
        if (scenarioContext.TestError is not null)
            await browser.CaptureFailureAsync(scenarioContext.ScenarioInfo.Title);
        await browser.DisposeAsync();
    }
}
