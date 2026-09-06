using Concertable.Payment.TestKit;

namespace Concertable.Payment.E2ETests.Helpers;

public sealed class PaymentDb
{
    private readonly PaymentTestClient client;

    public PaymentDb(PaymentTestClient client)
    {
        this.client = client;
    }

    public Task<string?> GetLatestSettlementPaymentIntentIdAsync(string operationType, string clientReference) =>
        client.GetLatestSettlementPaymentIntentIdAsync(operationType, clientReference);

    public Task<Guid?> GetEscrowPayeeIdAsync(string operationType, string clientReference) =>
        client.GetEscrowPayeeIdAsync(operationType, clientReference);

    public Task<string> GetEscrowPaymentIntentIdAsync(string operationType, string clientReference) =>
        client.GetEscrowPaymentIntentIdAsync(operationType, clientReference);

    public Task<int?> GetEscrowStatusAsync(string operationType, string clientReference) =>
        client.GetEscrowStatusAsync(operationType, clientReference);

    public Task<string?> GetEscrowRefundIdAsync(string operationType, string clientReference) =>
        client.GetEscrowRefundIdAsync(operationType, clientReference);

    public Task<int> GetLedgerTransactionCountAsync(string operationType, string clientReference) =>
        client.GetLedgerTransactionCountAsync(operationType, clientReference);

    public Task<long> GetLedgerSignedSumAsync(string operationType, string clientReference) =>
        client.GetLedgerSignedSumAsync(operationType, clientReference);

    public Task<long> GetLedgerPlatformRevenueAsync(string operationType, string clientReference) =>
        client.GetLedgerPlatformRevenueAsync(operationType, clientReference);
}
