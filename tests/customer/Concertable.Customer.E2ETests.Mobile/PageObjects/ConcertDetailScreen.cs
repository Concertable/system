using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.PageObjects;

public sealed class ConcertDetailScreen
{
    private readonly MobileApp app;

    public ConcertDetailScreen(MobileApp app) => this.app = app;

    private AppiumElement BuyTicketsButton => app.Driver.GetByTestId("buy-tickets");

    public void WaitUntilLoaded() => _ = BuyTicketsButton;

    public void ClickBuyTickets() => BuyTicketsButton.Click();
}
