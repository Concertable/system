using Concertable.Customer.E2ETests.Ui.PageObjects;
using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Steps;

[Binding]
public sealed class SignUpSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly WorkflowState state;

    private HomePage homePage = null!;
    private LoginPage loginPage = null!;
    private RegisterPage registerPage = null!;

    public SignUpSteps(UiFixture fixture, Browser browser, WorkflowState state)
    {
        this.fixture = fixture;
        this.browser = browser;
        this.state = state;
    }

    [Given(@"a visitor is on the customer home page")]
    public async Task VisitorOnCustomerHome()
    {
        homePage = new HomePage(browser.Page, fixture.App.CustomerSpaUrl);
        loginPage = new LoginPage(browser.Page, fixture.App.CustomerSpaUrl);
        registerPage = new RegisterPage(browser.Page, fixture.App.AuthUrl);
        await homePage.GotoAsync();
    }

    [When(@"they go to sign in")]
    public Task GoToSignIn() => homePage.ClickSignInAsync();

    [When(@"they click the sign up link")]
    public async Task ClickSignUpLink()
    {
        await loginPage.WaitForUrlAsync($"{fixture.App.AuthUrl}/Account/Login**");
        await loginPage.ClickSignUpAsync();
        await registerPage.WaitForLoadAsync();
    }

    [When(@"they register with a new email")]
    public async Task RegisterWithNewEmail()
    {
        state.SignUpEmail = $"signup-{Guid.NewGuid():N}@e2e.test";
        state.SignUpPassword = "P@ssw0rd!";
        await registerPage.RegisterAsync(state.SignUpEmail!, state.SignUpPassword!);
        await registerPage.ClickSignInAsync();
    }

    [When(@"they sign in with their new credentials")]
    public async Task SignInWithNewCredentials()
    {
        await loginPage.WaitForUrlAsync($"{fixture.App.AuthUrl}/Account/Login**");
        await loginPage.SignInAsync(state.SignUpEmail!, state.SignUpPassword!);
    }

    [Then(@"they are returned to the customer home page authenticated")]
    public Task ReturnedToCustomerHome() =>
        browser.Page.WaitForURLAsync($"{fixture.App.CustomerSpaUrl}/", new() { Timeout = 30_000 });
}
