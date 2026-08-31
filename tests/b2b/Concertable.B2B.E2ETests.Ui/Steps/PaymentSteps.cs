using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class PaymentSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly WorkflowState state;

    public PaymentSteps(UiFixture fixture, Browser browser, WorkflowState state)
    {
        this.fixture = fixture;
        this.browser = browser;
        this.state = state;
    }

    [Then(@"the payment is rejected")]
    public Task PaymentIsRejected() =>
        Assertions.Expect(browser.Page.GetByTestId("payment-error"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

    [Then(@"a payment hold of £(\d+) is captured from the artist")]
    public async Task PaymentHoldCaptured(decimal amount)
    {
        var bookingId = await fixture.App.DbFixture.Booking.GetIdByApplicationIdAsync(state.ApplicationId);
        var paymentIntentId = await fixture.App.DbFixture.Payment.GetEscrowPaymentIntentIdAsync(bookingId);
        var hold = await fixture.App.Stripe.GetCapturedHoldAsync(paymentIntentId, amount);

        Assert.NotNull(hold);
    }

    [Then(@"a Stripe transfer of £(\d+) is made to the venue manager")]
    public async Task StripeTransferMade(decimal amount)
    {
        var transfer = await fixture.App.Stripe.FindTransferAsync(
            Concertable.Payment.TestKit.StripeTestAccounts.BySeedUserId[fixture.App.SeedState.VenueManager1.Id], amount);

        Assert.NotNull(transfer);
    }
}
