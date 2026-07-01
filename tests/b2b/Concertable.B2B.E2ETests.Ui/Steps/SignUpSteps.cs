using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class SignUpSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly WorkflowState state;

    private BusinessGatewayPage businessGateway = null!;
    private LoginPage loginPage = null!;
    private RegisterPage registerPage = null!;
    private CreateVenuePage createVenuePage = null!;
    private CreateArtistPage createArtistPage = null!;

    private string surfaceUrl = null!;

    public SignUpSteps(UiFixture fixture, Browser browser, WorkflowState state)
    {
        this.fixture = fixture;
        this.browser = browser;
        this.state = state;
    }

    [Given(@"a visitor is on the business gateway")]
    public async Task VisitorOnBusinessGateway()
    {
        businessGateway = new BusinessGatewayPage(browser.Page, fixture.App.BusinessSpaUrl);
        loginPage = new LoginPage(browser.Page, fixture.App.BusinessSpaUrl);
        registerPage = new RegisterPage(browser.Page, fixture.App.AuthUrl);
        await businessGateway.GotoAsync();
    }

    [When(@"they click get started as a venue")]
    public async Task ClickGetStartedVenue()
    {
        surfaceUrl = fixture.App.VenueSpaUrl;
        createVenuePage = new CreateVenuePage(browser.Page, surfaceUrl);
        await businessGateway.ClickGetStartedVenueAsync();
    }

    [When(@"they click get started as an artist")]
    public async Task ClickGetStartedArtist()
    {
        surfaceUrl = fixture.App.ArtistSpaUrl;
        createArtistPage = new CreateArtistPage(browser.Page, surfaceUrl);
        await businessGateway.ClickGetStartedArtistAsync();
    }

    [When(@"they click the sign up link")]
    public async Task ClickSignUpLink()
    {
        await loginPage.WaitForUrlAsync($"{fixture.App.AuthUrl}/Account/Login**");
        await loginPage.ClickSignUpAsync();
        await registerPage.WaitForLoadAsync();
    }

    [When(@"they register as (.*)")]
    public async Task RegisterAsRole(string role)
    {
        _ = role;
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

    [When(@"they fill in the create venue form")]
    public async Task FillCreateVenue()
    {
        await createVenuePage.WaitForLoadAsync();
        await createVenuePage.FillAsync(
            name: "E2E Venue",
            about: "Created by an E2E test",
            bannerPath: FixturePath("banner.png"),
            avatarPath: FixturePath("avatar.png"));
    }

    [When(@"they submit the create venue form")]
    public Task SubmitCreateVenue() => createVenuePage.SubmitAsync();

    [When(@"they fill in the create artist form")]
    public async Task FillCreateArtist()
    {
        await createArtistPage.WaitForLoadAsync();
        await createArtistPage.FillAsync(
            name: "E2E Artist",
            about: "Created by an E2E test",
            bannerPath: FixturePath("banner.png"),
            avatarPath: FixturePath("avatar.png"));
    }

    [When(@"they submit the create artist form")]
    public Task SubmitCreateArtist() => createArtistPage.SubmitAsync();

    [Then(@"they land on the venue surface authenticated")]
    public Task LandedOnVenueSurface() =>
        browser.Page.WaitForURLAsync($"{fixture.App.VenueSpaUrl}/", new() { Timeout = 30_000 });

    [Then(@"they land on the artist surface authenticated")]
    public Task LandedOnArtistSurface() =>
        browser.Page.WaitForURLAsync($"{fixture.App.ArtistSpaUrl}/", new() { Timeout = 30_000 });

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
