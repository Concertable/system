using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Steps;

[Binding]
public sealed class PaymentSteps
{
    private readonly Browser browser;

    public PaymentSteps(Browser browser)
    {
        this.browser = browser;
    }

    [Then(@"the payment is rejected")]
    public Task PaymentIsRejected() =>
        Assertions.Expect(browser.Page.GetByTestId("payment-error"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
}
