namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class VenueDetailsPage
{
    private readonly IPage page;
    private readonly string url;

    public VenueDetailsPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = $"{spaBaseUrl}/find/venue";
    }

    private ILocator Opportunity(int id) => page.GetByTestId($"opportunity-{id}");
    private ILocator ApplyButton(int id) => Opportunity(id).GetByTestId("apply");

    public Task GotoAsync(int venueId) => page.GotoSpaAsync($"{url}/{venueId}");

    public Task ApplyAsync(int opportunityId) => ApplyButton(opportunityId).ClickAsync();

    public Task WaitUntilAppliedAsync(int opportunityId) =>
        Assertions.Expect(page.GetByText("Application submitted!")).ToBeVisibleAsync();
}
