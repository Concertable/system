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
    private ILocator FlatFeeFeeInput => LastCardEdit.GetByTestId("contract-flatfee-fee");
    private ILocator ContractTypeSelect => LastCardEdit.GetByTestId("opportunity-contract-type");
    private ILocator VenueHireFeeInput => LastCardEdit.GetByTestId("contract-venuehire-fee");
    private ILocator DoorSplitPercentInput => LastCardEdit.GetByTestId("contract-doorsplit-percent");
    private ILocator VersusGuaranteeInput => LastCardEdit.GetByTestId("contract-versus-guarantee");
    private ILocator VersusPercentInput => LastCardEdit.GetByTestId("contract-versus-percent");

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
        await ContractTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Venue Hire" }).ClickAsync();
        await VenueHireFeeInput.FillAsync(fee.ToString());
        await SaveButton.ClickAsync();
    }

    public async Task PostDoorSplitOpportunityAsync(int doorPercent)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await ContractTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Door Split" }).ClickAsync();
        await DoorSplitPercentInput.FillAsync(doorPercent.ToString());
        await SaveButton.ClickAsync();
    }

    public async Task PostVersusOpportunityAsync(int guarantee, int doorPercent)
    {
        await EditButton.ClickAsync();
        await Assertions.Expect(EditButton).ToHaveTextAsync("Editing");
        await AddOpportunityButton.ClickAsync();
        await ContractTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "Versus" }).ClickAsync();
        await VersusGuaranteeInput.FillAsync(guarantee.ToString());
        await VersusPercentInput.FillAsync(doorPercent.ToString());
        await SaveButton.ClickAsync();
    }

    public Task WaitUntilSavedAsync() =>
        Assertions.Expect(EditButton).ToHaveTextAsync("Edit", new() { Timeout = 15_000 });
}
