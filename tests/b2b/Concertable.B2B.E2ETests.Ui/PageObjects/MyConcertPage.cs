namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class MyConcertPage
{
    private readonly IPage page;
    private readonly string? url;

    public MyConcertPage(IPage page, string? spaBaseUrl = null)
    {
        this.page = page;
        this.url = spaBaseUrl;
    }

    private ILocator CancelButton => page.GetByTestId("cancel-booking");
    private ILocator ConfirmCancelButton => page.GetByTestId("cancel-booking-confirm");
    private ILocator DownloadContractButton => page.GetByTestId("download-contract");

    private ILocator DeclareDoorRevenueButton => page.GetByTestId("declare-door-revenue");
    private ILocator DoorTakeInput => page.GetByTestId("door-revenue-input");
    private ILocator ConfirmDoorTakingsButton => page.GetByTestId("declare-door-revenue-confirm");
    private ILocator ConcertableSales => page.GetByTestId("door-revenue-concertable");
    private ILocator DoorTakingsTotal => page.GetByTestId("door-revenue-total");

    public Task GotoAsync(int concertId) =>
        page.GotoSpaAsync($"{url}/my/concerts/concert/{concertId}");

    public async Task EnterDoorTakingsAsync(decimal externalTake)
    {
        await DeclareDoorRevenueButton.ClickAsync();
        await DoorTakeInput.FillAsync(externalTake.ToString());
    }

    public async Task ExpectBreakdownAsync(decimal concertable, decimal total)
    {
        await Assertions.Expect(ConcertableSales).ToHaveTextAsync(Money.Pounds(concertable));
        await Assertions.Expect(DoorTakingsTotal).ToHaveTextAsync(Money.Pounds(total));
    }

    public Task ConfirmDoorTakingsAsync() => ConfirmDoorTakingsButton.ClickAsync();

    public Task WaitUntilDoorTakingsRecordedAsync() =>
        Assertions.Expect(page.GetByText("Door takings recorded. The artist's share will settle shortly."))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

    public async Task CancelBookingAsync()
    {
        await CancelButton.ClickAsync();
        await ConfirmCancelButton.ClickAsync();
    }

    public async Task<string> DownloadContractAsync()
    {
        var pdf = page.WaitForResponseAsync(r => r.Url.Contains("/contract/pdf") && r.Status == 200);
        await DownloadContractButton.ClickAsync();
        var response = await pdf;

        return Pdf.ExtractText(await response.BodyAsync());
    }

    public Task WaitUntilCancelledAsync() =>
        Assertions.Expect(page.GetByText("Booking cancelled. Any payment held is refunded in full."))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
}
