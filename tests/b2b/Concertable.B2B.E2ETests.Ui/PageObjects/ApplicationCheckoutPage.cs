using Concertable.B2B.E2ETests.Ui.Support;

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

    private ILocator AgreeToTerms => page.GetByTestId("agree-to-terms");

    public async Task SubmitWithSavedCardAsync()
    {
        await AgreeToTerms.EnsureCheckedAsync();
        await payment.PayWithSavedCardAsync();
    }

    public async Task SubmitWithSavedCardAndVerifyAsync()
    {
        await AgreeToTerms.EnsureCheckedAsync();
        await payment.PayWithSavedCardAsync();
        await payment.CompleteChallengeIfRequiredAsync();
    }

    public async Task SubmitWithNewCardAsync(string cardNumber)
    {
        await AgreeToTerms.EnsureCheckedAsync();
        await payment.PayWithNewCardAsync(cardNumber);
    }
}
