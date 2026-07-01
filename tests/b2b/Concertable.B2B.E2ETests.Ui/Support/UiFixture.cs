namespace Concertable.B2B.E2ETests.Ui.Support;

public sealed class UiFixture
{
    private IPlaywright playwright = null!;

    public AppFixture App { get; } = new();
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await App.InitializeAsync();
        playwright = await Playwright.CreateAsync();
        var headless = Environment.GetEnvironmentVariable("CI") == "true"
            || Environment.GetEnvironmentVariable("HEADLESS") == "true";
        Browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = headless,
            SlowMo = headless ? 0 : 250,
            Args = [
                "--disable-features=IsolateOrigins,site-per-process",
                "--disable-site-isolation-trials"
                ]
        });
        await WarmUpSpaAsync();
    }

    private async Task WarmUpSpaAsync()
    {
        await using var context = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        foreach (var url in new[] { App.VenueSpaUrl, App.ArtistSpaUrl, App.BusinessSpaUrl })
            await page.GotoAsync(url, new() { Timeout = 120_000, WaitUntil = WaitUntilState.DOMContentLoaded });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        playwright.Dispose();
        await App.DisposeAsync();
    }
}
