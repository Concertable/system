namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class MyConcertPage
{
    private readonly IPage page;

    public MyConcertPage(IPage page) => this.page = page;

    private ILocator CancelButton => page.GetByTestId("cancel-booking");
    private ILocator ConfirmCancelButton => page.GetByTestId("cancel-booking-confirm");

    public async Task CancelBookingAsync()
    {
        await CancelButton.ClickAsync();
        await ConfirmCancelButton.ClickAsync();
    }

    public Task WaitUntilCancelledAsync() =>
        Assertions.Expect(page.GetByText("Booking cancelled. Any payment held is refunded in full."))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
}
