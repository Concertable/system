namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class HomePage
{
    private readonly IPage page;
    private readonly string url;

    public HomePage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = spaBaseUrl;
    }

    private ILocator SignInLink => page.GetByTestId("header-login");

    public Task GotoAsync() => page.GotoSpaAsync(url);

    public Task ClickSignInAsync() => SignInLink.ClickAsync();

    public Task WaitUntilLoadedAsync() =>
        page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
}
