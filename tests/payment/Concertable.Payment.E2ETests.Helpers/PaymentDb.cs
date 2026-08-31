using Concertable.Payment.TestKit;

namespace Concertable.Payment.E2ETests.Helpers;

public sealed class PaymentDb
{
    private readonly PaymentTestClient client;

    public PaymentDb(PaymentTestClient client)
    {
        this.client = client;
    }

    public Task<string?> GetLatestSettlementPaymentIntentIdAsync(int bookingId) =>
        client.GetLatestSettlementPaymentIntentIdAsync(bookingId);

    public Task<Guid?> GetEscrowPayeeIdAsync(int bookingId) =>
        client.GetEscrowPayeeIdAsync(bookingId);

    public Task<string> GetEscrowPaymentIntentIdAsync(int bookingId) =>
        client.GetEscrowPaymentIntentIdAsync(bookingId);

    public Task<int?> GetEscrowStatusAsync(int bookingId) =>
        client.GetEscrowStatusAsync(bookingId);

    public Task<string?> GetEscrowRefundIdAsync(int bookingId) =>
        client.GetEscrowRefundIdAsync(bookingId);

    public Task<int> GetLedgerTransactionCountAsync(int bookingId) =>
        client.GetLedgerTransactionCountAsync(bookingId);

    public Task<long> GetLedgerSignedSumAsync(int bookingId) =>
        client.GetLedgerSignedSumAsync(bookingId);

    public Task<long> GetLedgerPlatformRevenueAsync(int bookingId) =>
        client.GetLedgerPlatformRevenueAsync(bookingId);
}
