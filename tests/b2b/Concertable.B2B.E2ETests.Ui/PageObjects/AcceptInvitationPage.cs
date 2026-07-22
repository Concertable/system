namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class AcceptInvitationPage
{
    private readonly IPage page;
    private readonly string spaBaseUrl;

    public AcceptInvitationPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.spaBaseUrl = spaBaseUrl;
    }

    public Task GotoAsync(Guid invitationId) =>
        page.GotoSpaAsync($"{spaBaseUrl}/settings/members/accept/{invitationId}");
}
