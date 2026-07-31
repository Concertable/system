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
    private ILocator Switcher => page.GetByTestId("tenant-switcher");

    private ILocator MessageFrom(string sender) =>
        page.GetByTestId("mailbox-message").Filter(new() { HasText = sender });

    public Task GotoHomeAsync() => page.GotoSpaAsync($"{spaBaseUrl}/");

    public async Task OpenAsync()
    {
        await Assertions.Expect(Trigger).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Trigger.ClickAsync();
    }

    public async Task SwitchOrganizationAsync(string legalName)
    {
        await Switcher.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = legalName, Exact = true }).ClickAsync();
    }

    public Task ExpectMessageFromAsync(string sender) =>
        Assertions.Expect(MessageFrom(sender)).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public Task ExpectUnreadCountAsync(string count) =>
        Assertions.Expect(UnreadBadge).ToHaveTextAsync(count, new() { Timeout = 30_000 });

    public Task ExpectNoUnreadAsync() =>
        Assertions.Expect(UnreadBadge).ToHaveCountAsync(0, new() { Timeout = 30_000 });
}
