using Concertable.B2B.E2ETests.Ui.Support;

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
    // The signature dialog renders in a portal at the page root, not inside the opportunity card.
    private ILocator SignatureName => page.GetByTestId("e-sign");
    private ILocator ConfirmApplyButton => page.GetByTestId("confirm-apply");

    public Task GotoAsync(int venueId) => page.GotoSpaAsync($"{url}/{venueId}");

    public Task ApplyAsync(int opportunityId) => ApplyButton(opportunityId).ClickAsync();

    public async Task AgreeAndApplyAsync(int opportunityId)
    {
        await ApplyButton(opportunityId).ClickAsync();
        await SignatureName.FillAsync("Artie Artist");
        await ConfirmApplyButton.ClickAsync();
    }

    public Task WaitUntilAppliedAsync(int opportunityId) =>
        Assertions.Expect(page.GetByText("Application submitted!")).ToBeVisibleAsync();
}
