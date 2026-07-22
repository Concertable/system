namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class MembersPage
{
    private readonly IPage page;
    private readonly string spaBaseUrl;

    public MembersPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.spaBaseUrl = spaBaseUrl;
    }

    private ILocator InviteEmail => page.GetByTestId("invite-email");
    private ILocator InviteSubmit => page.GetByTestId("invite-submit");
    private ILocator Roster => page.GetByTestId("members-roster");
    private ILocator Switcher => page.GetByTestId("tenant-switcher");

    public ILocator MemberRow(Guid userId) => page.GetByTestId($"member-row-{userId}");
    private ILocator MemberRole(Guid userId) => page.GetByTestId($"member-role-{userId}");
    private ILocator RemoveMember(Guid userId) => page.GetByTestId($"remove-member-{userId}");

    public Task GotoAsync() => page.GotoSpaAsync($"{spaBaseUrl}/settings/members");

    public Task WaitForRosterAsync() =>
        Assertions.Expect(Roster).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public Task WaitForSwitcherAsync() =>
        Assertions.Expect(Switcher).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public async Task<Guid> InviteAsync(string email)
    {
        var invited = page.WaitForResponseAsync(r =>
            r.Url.Contains("/api/organizations/invitations")
            && r.Request.Method == "POST");
        await InviteEmail.FillAsync(email);
        await InviteSubmit.ClickAsync();
        var response = await invited;
        var body = await response.JsonAsync();
        return body!.Value.GetProperty("id").GetGuid();
    }

    public async Task ChangeRoleAsync(Guid userId, string role)
    {
        await MemberRole(userId).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = role, Exact = true }).ClickAsync();
    }

    public Task ExpectRoleAsync(Guid userId, string role) =>
        Assertions.Expect(MemberRole(userId)).ToContainTextAsync(role);

    public Task RemoveAsync(Guid userId) => RemoveMember(userId).ClickAsync();

    public async Task SwitchOrganizationAsync(string legalName)
    {
        await Switcher.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = legalName, Exact = true }).ClickAsync();
    }
}
