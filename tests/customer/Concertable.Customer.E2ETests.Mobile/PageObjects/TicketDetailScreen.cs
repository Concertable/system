using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.PageObjects;

public sealed class TicketDetailScreen
{
    private readonly MobileApp app;

    public TicketDetailScreen(MobileApp app) => this.app = app;

    private AppiumElement QrCode => app.Driver.GetByTestId("ticket-qr", TimeSpan.FromSeconds(15));

    public void WaitForQrCode() => _ = QrCode;
}
