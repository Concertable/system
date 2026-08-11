using Concertable.B2B.E2ETests.Ui.Hooks;
using Microsoft.Extensions.Logging;

namespace Concertable.B2B.E2ETests.Ui.Support;

public sealed class Browser : IAsyncDisposable, IDisposable, IPageAccessor
{
    private readonly ILogger<Browser> logger;
    private IBrowser playwrightBrowser = null!;
    private UiFixture fixture = null!;
    private LoginPersona? currentPersona;
    private bool establishDeniedCookieConsent;

    public IBrowserContext Context { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;

    public Browser(ILogger<Browser> logger)
    {
        this.logger = logger;
    }

    public async Task InitializeAsync(
        IBrowser playwrightBrowser,
        LoginPersona? persona,
        UiFixture fixture,
        bool establishDeniedCookieConsent)
    {
        this.playwrightBrowser = playwrightBrowser;
        this.fixture = fixture;
        this.establishDeniedCookieConsent = establishDeniedCookieConsent;
        await CreateContextAsync(persona);
    }

    public async Task UsePersonaAsync(LoginPersona persona)
    {
        if (currentPersona == persona) return;
        await SaveTraceAndDisposeAsync();
        await CreateContextAsync(persona);
    }

    public async Task UseFreshContextAsync()
    {
        await SaveTraceAndDisposeAsync();
        await CreateContextAsync(null);
    }

    private async Task CreateContextAsync(LoginPersona? persona)
    {
        var options = new BrowserNewContextOptions { IgnoreHTTPSErrors = true };
        if (persona is not null) options.StorageState = await LoginCaptureHooks.GetOrCaptureAsync(fixture, persona.Value);
        Context = await playwrightBrowser.NewContextAsync(options);
        if (establishDeniedCookieConsent) await CookieConsentState.EstablishDeniedAsync(Context);
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = false,
        });
        Page = await Context.NewPageAsync();
        Page.Response += async (_, response) =>
        {
            string body;
            try { body = await response.TextAsync(); }
            catch { body = "<unreadable>"; }
            if (response.Status >= 400)
            {
                logger.HttpErrorResponse(response.Status, response.Request.Method, response.Url, body);
                return;
            }
            if (response.Url.Contains("/api/"))
                logger.ApiSuccessResponse(response.Status, response.Request.Method, response.Url, body.Length > 500 ? body[..500] + "…" : body);
        };
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") logger.BrowserConsoleError(msg.Text);
            if (msg.Type == "warning") logger.BrowserConsoleError($"[console warn] {msg.Text}");
        };
        Page.PageError += (_, error) => logger.UncaughtJsException(error);
        Page.RequestFailed += (_, request) => logger.RequestFailed(request.Method, request.Url, request.Failure);
        Page.FrameNavigated += (_, frame) =>
        {
            if (frame == Page.MainFrame) logger.NavigatedTo(frame.Url);
        };
        currentPersona = persona;
    }

    private async Task SaveTraceAndDisposeAsync()
    {
        if (Context is null) return;
        Directory.CreateDirectory("playwright-traces");
        await Context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = $"playwright-traces/trace-{currentPersona}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.zip",
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
