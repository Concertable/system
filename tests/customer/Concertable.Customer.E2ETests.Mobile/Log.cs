using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Concertable.Customer.E2ETests.Mobile.Support;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Appium session started for app {Package}")]
    internal static partial void AppiumSessionStarted(this ILogger logger, string package);

    [LoggerMessage(Level = LogLevel.Information, Message = "Screenshot saved to {Path}")]
    internal static partial void ScreenshotSaved(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to capture screenshot")]
    internal static partial void FailedToCaptureScreenshot(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to quit Appium driver")]
    internal static partial void FailedToQuitAppiumDriver(this ILogger logger, Exception ex);
}
