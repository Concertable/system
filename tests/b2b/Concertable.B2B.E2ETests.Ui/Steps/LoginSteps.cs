using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class LoginSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private LoginPage loginPage = null!;

    public LoginSteps(UiFixture fixture, Browser browser)
    {
        this.fixture = fixture;
        this.browser = browser;
    }

    [Given(@"a visitor is on the business home page")]
    public async Task VisitorOnBusinessHomePage()
    {
        loginPage = new LoginPage(browser.Page, fixture.App.BusinessSpaUrl);
        await browser.Page.GotoAsync(fixture.App.BusinessSpaUrl, new() { WaitUntil = WaitUntilState.Load });
    }

    [When(@"they click sign in")]
    public Task ClickSignIn() =>
        browser.Page.GetByTestId("header-login").ClickAsync();

    [When(@"they submit seeded venue manager credentials")]
    public Task SubmitVenueManagerCredentials() =>
        loginPage.SignInAsync(fixture.App.SeedState.VenueManager1.Email, SeedState.TestPassword);

    [Then(@"they are returned to the business home page")]
    public Task ReturnedToBusinessHomePage() =>
        browser.Page.WaitForLoadStateAsync(LoadState.Load);
}
