using Microsoft.Playwright;

namespace Concertable.Testing.E2E.Ui;

public static class CookieConsentState
{
    private const string DeniedConsentInitScript = """
        if (location.hostname === 'localhost') {
            localStorage.setItem('cookie-consent', JSON.stringify({
                version: 1,
                decidedAtUtc: new Date().toISOString(),
                categories: { analytics: false, marketing: false }
            }));
        }
        """;

    public static Task EstablishDeniedAsync(IBrowserContext context) =>
        context.AddInitScriptAsync(DeniedConsentInitScript);
}
