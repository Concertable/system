namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class MailboxPage
{
    private readonly IPage page;
    private readonly string spaBaseUrl;

    public MailboxPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.spaBaseUrl = spaBaseUrl;
    }

    private ILocator Trigger => page.GetByTestId("mailbox-trigger");
    private ILocator UnreadBadge => page.GetByTestId("mailbox-unread");

    private ILocator MessageFrom(string sender) =>
        page.GetByTestId("mailbox-message").Filter(new() { HasText = sender });

    private ILocator ReportTriggerFor(string sender) =>
        MessageFrom(sender).GetByTestId("message-report-trigger");

    private ILocator ReportCategory => page.GetByTestId("report-category");
    private ILocator ReportDetails => page.GetByTestId("report-details");
    private ILocator ReportSubmit => page.GetByTestId("report-submit");
    private ILocator ReportConfirmation => page.GetByTestId("report-confirmation");

    public Task GotoHomeAsync() => page.GotoSpaAsync($"{spaBaseUrl}/");

    public async Task OpenAsync()
    {
        await Assertions.Expect(Trigger).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Trigger.ClickAsync();
    }

    // A member of two orgs lands on the full-page TenantChooser after a fresh sign-in (no active tenant),
    // not the header switcher — pick the org there.
    public Task ChooseOrganizationAsync(string legalName) =>
        page.GetByRole(AriaRole.Button, new() { Name = legalName, Exact = true }).ClickAsync();

    public Task ExpectMessageFromAsync(string sender) =>
        Assertions.Expect(MessageFrom(sender)).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public Task ExpectUnreadCountAsync(string count) =>
        Assertions.Expect(UnreadBadge).ToHaveTextAsync(count, new() { Timeout = 30_000 });

    public Task ExpectNoUnreadAsync() =>
        Assertions.Expect(UnreadBadge).ToHaveCountAsync(0, new() { Timeout = 30_000 });

    public async Task ReportMessageAsync(string sender, string category, string details)
    {
        await ReportTriggerFor(sender).ClickAsync();

        await ReportCategory.GetByRole(AriaRole.Combobox).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = category, Exact = true }).ClickAsync();

        await ReportDetails.FillAsync(details);
        await ReportSubmit.ClickAsync();
    }

    public Task ExpectReportConfirmationAsync() =>
        Assertions.Expect(ReportConfirmation).ToBeVisibleAsync(new() { Timeout = 30_000 });
}
