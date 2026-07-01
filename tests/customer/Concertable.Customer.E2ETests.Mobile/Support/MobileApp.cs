using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;

namespace Concertable.Customer.E2ETests.Mobile.Support;

public sealed class MobileApp : IAsyncDisposable, IDisposable
{
    private readonly ILogger<MobileApp> logger;
    private AndroidDriver driver = null!;

    public AndroidDriver Driver => driver;

    public MobileApp(ILogger<MobileApp> logger) => this.logger = logger;

    public Task InitializeAsync(MobileFixture fixture)
    {
        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = AutomationName.AndroidUIAutomator2,
            App = fixture.ApkPath,
            DeviceName = fixture.AvdName,
        };

        options.AddAdditionalAppiumOption("appPackage", fixture.AppPackage);
        options.AddAdditionalAppiumOption("appActivity", fixture.AppActivity);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("noReset", false);
        options.AddAdditionalAppiumOption("fullReset", false);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);

        driver = new AndroidDriver(fixture.AppiumServerUri, options, TimeSpan.FromSeconds(180));
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

        logger.AppiumSessionStarted(fixture.AppPackage);
        return Task.CompletedTask;
    }

    public void SaveScreenshotOnFailure(string scenarioTitle)
    {
        if (driver is null) return;
        try
        {
            Directory.CreateDirectory("mobile-traces");
            var screenshot = driver.GetScreenshot();
            var safeTitle = string.Join("_", scenarioTitle.Split(Path.GetInvalidFileNameChars()));
            var path = $"mobile-traces/screenshot-{safeTitle}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png";
            screenshot.SaveAsFile(path);
            logger.ScreenshotSaved(path);
        }
        catch (Exception ex)
        {
            logger.FailedToCaptureScreenshot(ex);
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
    {
        try { driver?.Quit(); }
        catch (Exception ex) { logger.FailedToQuitAppiumDriver(ex); }
        driver?.Dispose();
        driver = null!;
        return ValueTask.CompletedTask;
    }
}
