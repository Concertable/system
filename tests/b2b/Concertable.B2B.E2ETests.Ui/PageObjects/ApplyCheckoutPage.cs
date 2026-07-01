namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class ApplyCheckoutPage
{
    private readonly IPage page;
    private readonly IStripePayment payment;

    public ApplyCheckoutPage(IPage page, IStripePayment payment)
    {
        this.page = page;
        this.payment = payment;
    }

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        await page.WaitForURLAsync("**/opportunity/checkout/**");
        await payment.PayWithNewCardAsync(cardNumber);
    }

    public async Task PayWithSavedCardAsync()
    {
        await page.WaitForURLAsync("**/opportunity/checkout/**");
        await payment.PayWithSavedCardAsync();
    }
}
