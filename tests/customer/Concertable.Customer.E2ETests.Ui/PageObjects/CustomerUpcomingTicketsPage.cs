namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class CustomerUpcomingTicketsPage
{
    private readonly IPage page;

    public CustomerUpcomingTicketsPage(IPage page) => this.page = page;

    private ILocator TicketList => page.GetByTestId("upcoming-tickets-list");
    private ILocator TicketCards => page.GetByTestId("ticket-card");
    private ILocator QrTrigger => page.GetByTestId("qr-trigger").First;
    private ILocator QrDialog => page.GetByTestId("qr-dialog");
    private ILocator QrImage => page.GetByTestId("qr-image");

    public Task WaitForTicketListAsync() =>
        Assertions.Expect(TicketList).ToBeVisibleAsync(new() { Timeout = 15_000 });

    public Task WaitForTicketCardsAsync() =>
        Assertions.Expect(TicketCards.First).ToBeVisibleAsync();

    public Task OpenQrCodeAsync() =>
        QrTrigger.ClickAsync();

    public Task WaitForQrDialogAsync() =>
        Assertions.Expect(QrDialog).ToBeVisibleAsync();

    public Task WaitForQrImageAsync() =>
        Assertions.Expect(QrImage).ToBeVisibleAsync();
}
