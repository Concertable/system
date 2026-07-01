using Concertable.Customer.E2ETests.Ui.Hooks;
using Concertable.E2ETests.Support;
using Microsoft.Extensions.Logging;

namespace Concertable.Customer.E2ETests.Ui.Support;

public sealed class Browser : IAsyncDisposable, IDisposable, IPageAccessor
{
    private readonly ILogger<Browser> logger;
    private IBrowser playwrightBrowser = null!;
    private UiFixture fixture = null!;
    private Role? currentRole;

    public IBrowserContext Context { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;

    public Browser(ILogger<Browser> logger)
    {
        this.logger = logger;
    }

    public async Task InitializeAsync(IBrowser playwrightBrowser, Role? role, UiFixture fixture)
    {
        this.playwrightBrowser = playwrightBrowser;
        this.fixture = fixture;
        await CreateContextAsync(role);
    }

    private async Task CreateContextAsync(Role? role)
    {
        var options = new BrowserNewContextOptions { IgnoreHTTPSErrors = true };
        if (role is not null) options.StorageState = await LoginCaptureHooks.GetOrCaptureAsync(fixture);
        Context = await playwrightBrowser.NewContextAsync(options);
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = false,
        });
        Page = await Context.NewPageAsync();
        Page.Response += async (_, response) =>
        {
            if (response.Status < 400) return;
            string body;
            try { body = await response.TextAsync(); }
            catch { body = "<unreadable>"; }
            logger.HttpErrorResponse(response.Status, response.Request.Method, response.Url, body);
        };
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") logger.BrowserConsoleError(msg.Text);
            if (msg.Type == "warning") logger.BrowserConsoleError($"[console warn] {msg.Text}");
        };
        currentRole = role;
    }

    private async Task SaveTraceAndDisposeAsync()
    {
        if (Context is null) return;
        Directory.CreateDirectory("playwright-traces");
        await Context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = $"playwright-traces/trace-customer-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip",
        });
        logger.PlaywrightTraceSaved();
        await Context.DisposeAsync();
        Context = null!;
    }

    public async Task CaptureFailureAsync(string scenarioTitle)
    {
        if (Page is null) return;

        var failuresDir = Path.Combine(AppContext.BaseDirectory, "playwright-failures");
        Directory.CreateDirectory(failuresDir);
        var safeName = new string(scenarioTitle.Take(60).Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var path = Path.Combine(failuresDir, $"{safeName}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        logger.FailureScreenshot(path);

        string[] selectors =
        [
            "[role='alert']",
            "[data-sonner-toast]",
            "[data-testid*='error']",
            "[data-testid*='toast']",
            ".text-destructive",
        ];

        foreach (var selector in selectors)
        {
            var locator = Page.Locator(selector);
            var count = await locator.CountAsync();
            for (var i = 0; i < count; i++)
            {
                try
                {
                    var text = (await locator.Nth(i).InnerTextAsync(new() { Timeout = 1_000 })).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        logger.OnScreenError(selector, text);
                }
                catch { }
            }
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await SaveTraceAndDisposeAsync();
    }
}
