using Concertable.Customer.TestKit;
using Concertable.Payment.TestKit;

namespace Concertable.Customer.E2ETests;

public sealed class DbFixture
{
    private readonly CustomerTestClient customer;
    private readonly PaymentTestClient payment;

    public PaymentDb Payment { get; }

    public DbFixture(CustomerTestClient customer, PaymentTestClient payment)
    {
        this.customer = customer;
        this.payment = payment;
        Payment = new PaymentDb(payment);
    }

    public async Task ResetAsync()
    {
        await payment.ResetAsync();
        await customer.ResetAsync();
    }
}
