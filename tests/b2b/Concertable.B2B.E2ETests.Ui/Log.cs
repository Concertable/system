using Microsoft.Extensions.Logging;

namespace Concertable.B2B.E2ETests.Ui.Support;

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Uncaught JS exception: {Message}")]
    internal static partial void UncaughtJsException(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Request failed (network): {Method} {Url} — {Failure}")]
    internal static partial void RequestFailed(this ILogger logger, string method, string url, string? failure);

    [LoggerMessage(Level = LogLevel.Information, Message = "Navigated to: {Url}")]
    internal static partial void NavigatedTo(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "API {Status} {Method} {Url}: {Body}")]
    internal static partial void ApiSuccessResponse(this ILogger logger, int status, string method, string url, string body);
}
