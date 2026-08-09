namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class TicketCheckoutPage
{
    private readonly IPage page;
    private readonly IStripePayment payment;

    public TicketCheckoutPage(IPage page, IStripePayment payment)
    {
        this.page = page;
        this.payment = payment;
    }

    private ILocator ConfirmButton => page.GetByTestId("confirm");
    private ILocator SuccessScreen => page.GetByTestId("checkout-success");
    private ILocator ViewTicketsButton => page.GetByTestId("view-tickets");
    private ILocator Toast(string text) =>
        page.Locator("[data-sonner-toast]").Filter(new() { HasText = text });

    public Task PayWithTestCardAsync() => payment.PayWithSavedCardAsync();

    public Task PayWithNewCardAsync(string cardNumber) =>
        payment.PayWithNewCardAsync(cardNumber);

    public Task WaitForSuccessScreenAsync() =>
        Assertions.Expect(SuccessScreen).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public Task WaitForTicketPurchasedToastAsync() =>
        Assertions.Expect(Toast("You purchased")).ToBeVisibleAsync(new() { Timeout = 30_000 });

    public Task ClickViewTicketsAsync() =>
        ViewTicketsButton.ClickAsync();
}
