using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.Hooks;

[Binding]
public sealed class EmulatorHooks
{
    public static MobileFixture Fixture { get; internal set; } = null!;

    private readonly MobileFixture fixture;

    public EmulatorHooks(MobileFixture fixture) => this.fixture = fixture;

    [BeforeScenario(Order = 1)]
    public async Task BeforeScenario() =>
        await fixture.App.ResetAsync();
}
