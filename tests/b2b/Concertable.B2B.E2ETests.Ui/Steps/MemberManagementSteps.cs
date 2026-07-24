using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class MemberManagementSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;

    private MembersPage membersPage = null!;
    private Guid invitationId;

    public MemberManagementSteps(UiFixture fixture, Browser browser)
    {
        this.fixture = fixture;
        this.browser = browser;
    }

    private string VenueSpaUrl => fixture.App.VenueSpaUrl;
    private string ColleagueEmail => fixture.App.SeedState.VenueManager2.Email;
    private Guid ColleagueId => fixture.App.SeedState.VenueManager2.Id;
    private Guid OwnerId => fixture.App.SeedState.VenueManager1.Id;

    [Given(@"the venue owner is on the members page")]
    public async Task OwnerIsOnTheMembersPage()
    {
        membersPage = new MembersPage(browser.Page, VenueSpaUrl);
        await membersPage.GotoAsync();
        await membersPage.WaitForRosterAsync();
    }

    [Given(@"the owner invites a colleague to the organization")]
    [When(@"the owner invites a colleague to the organization")]
    public async Task OwnerInvitesAColleague()
    {
        invitationId = await membersPage.InviteAsync(ColleagueEmail);
    }

    [Given(@"the colleague accepts the invitation through the emailed link")]
    [When(@"the colleague accepts the invitation through the emailed link")]
    public async Task ColleagueAcceptsTheInvitation()
    {
        await browser.UseFreshContextAsync();

        var login = new LoginPage(browser.Page, VenueSpaUrl);
        await login.GotoAsync();
        await login.SignInAsync(ColleagueEmail, SeedState.TestPassword);
        await browser.Page.WaitForURLAsync($"{VenueSpaUrl}/", new() { Timeout = 30_000 });

        var acceptPage = new AcceptInvitationPage(browser.Page, VenueSpaUrl);
        await acceptPage.GotoAsync(invitationId);

        membersPage = new MembersPage(browser.Page, VenueSpaUrl);
        await membersPage.WaitForRosterAsync();
    }

    [When(@"the owner returns to the members page")]
    public async Task OwnerReturnsToTheMembersPage()
    {
        await browser.UseRoleAsync(Role.VenueManager);
        membersPage = new MembersPage(browser.Page, VenueSpaUrl);
        await membersPage.GotoAsync();
        await membersPage.WaitForRosterAsync();
    }

    [Then(@"the colleague appears in the member roster")]
    public Task ColleagueAppearsInTheRoster() =>
        Assertions.Expect(membersPage.MemberRow(ColleagueId)).ToBeVisibleAsync(new() { Timeout = 30_000 });

    [When(@"the owner changes the colleague's role to (\w+)")]
    public Task OwnerChangesColleagueRole(string role) =>
        membersPage.ChangeRoleAsync(ColleagueId, role);

    [Then(@"the colleague's role shows as (\w+)")]
    public Task ColleagueRoleShowsAs(string role) =>
        membersPage.ExpectRoleAsync(ColleagueId, role);

    [When(@"the owner removes the colleague")]
    public Task OwnerRemovesTheColleague() => membersPage.RemoveAsync(ColleagueId);

    [Then(@"the colleague no longer appears in the roster")]
    public Task ColleagueNoLongerAppears() =>
        Assertions.Expect(membersPage.MemberRow(ColleagueId)).ToBeHiddenAsync(new() { Timeout = 30_000 });

    [Then(@"the tenant switcher offers the colleague both organizations")]
    public Task SwitcherIsAvailable() => membersPage.WaitForSwitcherAsync();

    [When(@"the colleague switches to their own organization")]
    public Task ColleagueSwitchesToOwnOrganization() =>
        membersPage.SwitchOrganizationAsync(ColleagueEmail);

    [Then(@"member management shows only their own organization's members")]
    public async Task OnlyOwnOrganizationMembersAreShown()
    {
        await Assertions.Expect(membersPage.MemberRow(ColleagueId)).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(membersPage.MemberRow(OwnerId)).ToBeHiddenAsync(new() { Timeout = 30_000 });
    }
}
