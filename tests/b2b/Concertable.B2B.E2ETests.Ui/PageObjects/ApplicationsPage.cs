namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class ApplicationsPage
{
    private readonly IPage page;
    private readonly string url;

    public ApplicationsPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = spaBaseUrl;
    }

    private ILocator Application(int id) => page.GetByTestId($"application-{id}");
    private ILocator AcceptButton(int id) => Application(id).GetByTestId("accept");

    public Task GotoAsync(int opportunityId) => page.GotoSpaAsync($"{url}/my/opportunities/{opportunityId}/applications");

    public Task ClickAcceptAsync(int applicationId) => AcceptButton(applicationId).ClickAsync();
}
