using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.PageObjects;

public sealed class CheckoutSuccessScreen
{
    private readonly MobileApp app;

    public CheckoutSuccessScreen(MobileApp app) => this.app = app;

    private AppiumElement SuccessScreen => app.Driver.GetByTestId("checkout-success", TimeSpan.FromSeconds(45));
    private AppiumElement ViewTicketsButton => app.Driver.GetByTestId("view-tickets");

    public void WaitUntilVisible() => _ = SuccessScreen;

    public void ClickViewTickets() => ViewTicketsButton.Click();
}
