namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class CookieConsentPage(IPage page)
{
    private ILocator Banner => page.GetByTestId("cookie-banner");
    private ILocator Preferences => page.GetByTestId("cookie-prefs");

    public Task WaitForBannerAsync() =>
        Banner.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    public Task WaitForBannerDismissedAsync() =>
        Banner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

    public Task WaitForPreferencesAsync() =>
        Preferences.WaitForAsync(new() { State = WaitForSelectorState.Visible });

    public Task AcceptAllAsync() => page.GetByTestId("cookie-accept-all").ClickAsync();
    public Task RejectAllAsync() => page.GetByTestId("cookie-reject-all").ClickAsync();
    public Task OpenPreferencesFromFooterAsync() => page.GetByTestId("cookie-manage-footer").ClickAsync();

    public Task<string?> ReadStoredConsentAsync() =>
        page.EvaluateAsync<string?>("() => window.localStorage.getItem('cookie-consent')");
}
