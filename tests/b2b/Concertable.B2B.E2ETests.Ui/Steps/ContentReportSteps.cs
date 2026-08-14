using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class ContentReportSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;

    private MailboxPage mailbox = null!;

    public ContentReportSteps(UiFixture fixture, Browser browser)
    {
        this.fixture = fixture;
        this.browser = browser;
    }

    private string VenueSpaUrl => fixture.App.VenueSpaUrl;

    [When(@"the owner reports the message from ""(.*)"" as ""(.*)"" with details ""(.*)""")]
    public async Task OwnerReportsMessage(string sender, string category, string details)
    {
        mailbox = new MailboxPage(browser.Page, VenueSpaUrl);
        await mailbox.ReportMessageAsync(sender, category, details);
    }

    [Then(@"the owner sees the report confirmation")]
    public Task OwnerSeesReportConfirmation() => mailbox.ExpectReportConfirmationAsync();
}
