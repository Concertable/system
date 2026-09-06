using Concertable.B2B.TestKit;
using Concertable.Payment.E2ETests.Helpers;
using Concertable.Payment.TestKit;

namespace Concertable.B2B.E2ETests;

public sealed class DbFixture
{
    private readonly B2BTestClient b2b;
    private readonly PaymentTestClient payment;

    public OpportunityDb Opportunity { get; }
    public ApplicationDb Application { get; }
    public BookingDb Booking { get; }
    public ConcertDb Concert { get; }
    public PaymentOperationsDb Payment { get; }

    public DbFixture(B2BTestClient b2b, PaymentTestClient payment)
    {
        this.b2b = b2b;
        this.payment = payment;
        Opportunity = new OpportunityDb(b2b);
        Application = new ApplicationDb(b2b);
        Booking = new BookingDb(b2b);
        Concert = new ConcertDb(b2b);
        Payment = new PaymentOperationsDb(new PaymentDb(payment));
    }

    public async Task ResetAsync()
    {
        await payment.ResetAsync();
        await b2b.ResetAsync();
    }
}
