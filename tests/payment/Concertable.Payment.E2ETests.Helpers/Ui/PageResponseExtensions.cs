using Microsoft.Playwright;

namespace Concertable.Payment.E2ETests.Helpers.Ui;

public static class PageResponseExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> and awaits the first response matching <paramref name="urlPredicate"/>,
    /// then fails with the observed status and body when it is not successful. Filtering the wait itself on
    /// success would make an erroring endpoint indistinguishable from a request that never fired, turning a
    /// 4xx/5xx into an opaque timeout.
    /// </summary>
    public static async Task<IResponse> RunAndWaitForOkResponseAsync(
        this IPage page,
        Func<Task> action,
        Func<IResponse, bool> urlPredicate,
        float timeoutMs)
    {
        var response = await page.RunAndWaitForResponseAsync(
            action,
            urlPredicate,
            new PageRunAndWaitForResponseOptions { Timeout = timeoutMs });

        if (response.Ok) return response;

        string body;
        try { body = await response.TextAsync(); }
        catch { body = "<unreadable>"; }

        throw new PlaywrightException(
            $"Expected a successful response from {response.Request.Method} {response.Url} but got HTTP {response.Status}: {body}");
    }
}
