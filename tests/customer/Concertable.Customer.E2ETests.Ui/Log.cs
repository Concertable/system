using Microsoft.Extensions.Logging;

namespace Concertable.Customer.E2ETests.Ui.Support;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Playwright trace saved to playwright-traces/")]
    internal static partial void PlaywrightTraceSaved(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP {Status} {Method} {Url}\n{Body}")]
    internal static partial void HttpErrorResponse(this ILogger logger, int status, string method, string url, string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Browser console error: {Message}")]
    internal static partial void BrowserConsoleError(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Failure screenshot: {Path}")]
    internal static partial void FailureScreenshot(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "On-screen error [{Selector}]: {Text}")]
    internal static partial void OnScreenError(this ILogger logger, string selector, string text);
}
