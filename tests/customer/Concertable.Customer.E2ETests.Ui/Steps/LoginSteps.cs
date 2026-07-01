using Concertable.Customer.E2ETests.Ui.PageObjects;
using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Steps;

[Binding]
public sealed class LoginSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private HomePage homePage = null!;
    private LoginPage loginPage = null!;

    public LoginSteps(UiFixture fixture, Browser browser)
    {
        this.fixture = fixture;
        this.browser = browser;
    }

    [Given(@"a visitor is on the home page")]
    public async Task VisitorOnHomePage()
    {
        homePage = new HomePage(browser.Page, fixture.App.CustomerSpaUrl);
        loginPage = new LoginPage(browser.Page, fixture.App.CustomerSpaUrl);
        await homePage.GotoAsync();
    }

    [When(@"they click sign in")]
    public Task ClickSignIn() => homePage.ClickSignInAsync();

    [When(@"they submit seeded customer credentials")]
    public Task SubmitCustomerCredentials()
    {
        var seed = fixture.App.SeedState;
        return loginPage.SignInAsync(seed.Customer1.Email, SeedState.TestPassword);
    }

    [Then(@"they are returned to the home page")]
    public Task ReturnedToHomePage() => homePage.WaitUntilLoadedAsync();
}
