namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class MyVenuePage
{
    private readonly IPage page;
    private readonly string url;

    public MyVenuePage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = $"{spaBaseUrl}/my";
    }

    private ILocator EditButton => page.GetByTestId("edit");
    private ILocator SaveButton => page.GetByTestId("save");
    private ILocator AddOpportunityButton => page.GetByTestId("opportunity-add");
    private ILocator LastCardEdit => page.GetByTestId("opportunity-card-edit").Last;
    private ILocator FlatFeeFeeInput => LastCardEdit.GetByTestId("deal-flatfee-fee");
    private ILocator DealTypeSelect => LastCardEdit.GetByTestId("opportunity-deal-type");
    private ILocator VenueHireFeeInput => LastCardEdit.GetByTestId("deal-venuehire-fee");
    private ILocator DoorSplitPercentInput => LastCardEdit.GetByTestId("deal-doorsplit-percent");
    private ILocator VersusGuaranteeInput => LastCardEdit.GetByTestId("deal-versus-guarantee");
    private ILocator VersusPercentInput => LastCardEdit.GetByTestId("deal-versus-percent");

    public Task GotoAsync() => page.GotoSpaAsync(url);

    public async Task PostFlatFeeOpportunityAsync(decimal fee)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await FlatFeeFeeInput.FillAsync(fee.ToString());
        await SaveButton.ClickAsync();
    }

    public async Task PostVenueHireOpportunityAsync(decimal fee)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await DealTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Venue Hire" }).ClickAsync();
        await VenueHireFeeInput.FillAsync(fee.ToString());
        await SaveButton.ClickAsync();
    }

    public async Task PostDoorSplitOpportunityAsync(int doorPercent)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await DealTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Door Split" }).ClickAsync();
        await DoorSplitPercentInput.FillAsync(doorPercent.ToString());
        await SaveButton.ClickAsync();
    }

    public async Task PostVersusOpportunityAsync(int guarantee, int doorPercent)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await DealTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Versus" }).ClickAsync();
        await VersusGuaranteeInput.FillAsync(guarantee.ToString());
        await VersusPercentInput.FillAsync(doorPercent.ToString());
        await SaveButton.ClickAsync();
    }

    public Task WaitUntilSavedAsync() =>
        Assertions.Expect(EditButton).ToHaveTextAsync("Edit", new() { Timeout = 15_000 });
}
