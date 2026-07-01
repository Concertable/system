namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class ApplicationCheckoutPage
{
    private readonly IPage page;
    private readonly IStripePayment payment;

    public ApplicationCheckoutPage(IPage page, IStripePayment payment)
    {
        this.page = page;
        this.payment = payment;
    }

    public Task SubmitWithSavedCardAsync() => payment.PayWithSavedCardAsync();

    public async Task SubmitWithSavedCardAndVerifyAsync()
    {
        await payment.PayWithSavedCardAsync();
        await payment.CompleteChallengeIfRequiredAsync();
    }

    public Task SubmitWithNewCardAsync(string cardNumber) => payment.PayWithNewCardAsync(cardNumber);
}
