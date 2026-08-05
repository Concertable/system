using System.Text.Json;
using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class CookieConsentSteps
{
    private static readonly string[] NonEssentialCookiePrefixes =
        ["_ga", "_gid", "_gat", "_fbp", "_hj", "ajs_", "mp_"];

    private readonly Browser browser;
    private CookieConsentPage? page;

    public CookieConsentSteps(Browser browser) => this.browser = browser;

    private CookieConsentPage Page => page ??= new CookieConsentPage(browser.Page);

    [When(@"they accept all cookies")]
    public Task AcceptAll() => Page.AcceptAllAsync();

    [When(@"they reject all cookies")]
    public Task RejectAll() => Page.RejectAllAsync();

    [When(@"they reload the page")]
    public Task Reload() =>
        browser.Page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });

    [When(@"they open cookie preferences from the footer")]
    public Task OpenPreferencesFromFooter() => Page.OpenPreferencesFromFooterAsync();

    [Then(@"the cookie consent banner is shown")]
    public Task BannerShown() => Page.WaitForBannerAsync();

    [Then(@"the cookie consent banner is dismissed")]
    public Task BannerDismissed() => Page.WaitForBannerDismissedAsync();

    [Then(@"the cookie preferences dialog is shown")]
    public Task PreferencesShown() => Page.WaitForPreferencesAsync();

    [Then(@"no non-essential cookies are stored")]
    public async Task NoNonEssentialCookies()
    {
        var cookies = await browser.Context.CookiesAsync();
        var offenders = cookies
            .Where(cookie => NonEssentialCookiePrefixes.Any(prefix =>
                cookie.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Select(cookie => cookie.Name)
            .ToArray();
        Assert.Empty(offenders);
    }

    [Then(@"the stored consent denies every optional category")]
    public async Task StoredConsentDeniesEveryCategory()
    {
        foreach (var (name, granted) in await ReadCategoriesAsync())
            Assert.False(granted, $"category '{name}' should be denied");
    }

    [Then(@"the stored consent grants every optional category")]
    public async Task StoredConsentGrantsEveryCategory()
    {
        var categories = await ReadCategoriesAsync();
        Assert.NotEmpty(categories);
        foreach (var (name, granted) in categories)
            Assert.True(granted, $"category '{name}' should be granted");
    }

    private async Task<List<(string Name, bool Granted)>> ReadCategoriesAsync()
    {
        var raw = await Page.ReadStoredConsentAsync();
        Assert.NotNull(raw);
        using var document = JsonDocument.Parse(raw!);
        return document.RootElement
            .GetProperty("categories")
            .EnumerateObject()
            .Select(category => (category.Name, category.Value.GetBoolean()))
            .ToList();
    }
}
