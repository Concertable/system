using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class GroupInboxSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;

    private MailboxPage mailbox = null!;

    public GroupInboxSteps(UiFixture fixture, Browser browser)
    {
        this.fixture = fixture;
        this.browser = browser;
    }

    private string VenueSpaUrl => fixture.App.VenueSpaUrl;
    private string ColleagueEmail => fixture.App.SeedState.VenueManager3.Email;
    private string VenueOrgName => fixture.App.SeedState.VenueManager1.Email;

    [Given(@"the venue owner opens their mailbox")]
    public async Task OwnerOpensTheirMailbox()
    {
        mailbox = new MailboxPage(browser.Page, VenueSpaUrl);
        await mailbox.GotoHomeAsync();
        await mailbox.OpenAsync();
    }

    [Then(@"the mailbox shows a message from ""(.*)""")]
    public Task MailboxShowsMessageFrom(string sender) =>
        mailbox.ExpectMessageFromAsync(sender);

    [Then(@"the owner has no unread messages")]
    public Task OwnerHasNoUnread() => mailbox.ExpectNoUnreadAsync();

    [When(@"a colleague of the venue signs in and switches to the venue organization")]
    public async Task ColleagueSignsInAndSwitches()
    {
        await browser.UseFreshContextAsync();

        var login = new LoginPage(browser.Page, VenueSpaUrl);
        await login.GotoAsync();
        await login.SignInAsync(ColleagueEmail, SeedState.TestPassword);
        await browser.Page.WaitForURLAsync($"{VenueSpaUrl}/", new() { Timeout = 30_000 });

        mailbox = new MailboxPage(browser.Page, VenueSpaUrl);
        await mailbox.ChooseOrganizationAsync(VenueOrgName);
    }

    [Then(@"the colleague has (\d+) unread message")]
    public Task ColleagueHasUnread(int count) =>
        mailbox.ExpectUnreadCountAsync(count.ToString());

    [When(@"the colleague opens their mailbox")]
    public Task ColleagueOpensTheirMailbox() => mailbox.OpenAsync();
}
