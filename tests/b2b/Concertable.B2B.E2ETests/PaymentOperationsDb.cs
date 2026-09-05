using Concertable.B2B.TestKit;
using Concertable.Payment.E2ETests.Helpers;

namespace Concertable.B2B.E2ETests;

/// <summary>
/// Reads Payment's own state for the operations B2B named. Payment indexes by the reference, so this is the
/// one place the booking and concert ids the assertions speak in become that reference.
/// </summary>
public sealed class PaymentOperationsDb
{
    private readonly PaymentDb payment;

    public PaymentOperationsDb(PaymentDb payment)
    {
        this.payment = payment;
    }

    public Task<Guid?> GetEscrowPayeeIdAsync(int bookingId) =>
        payment.GetEscrowPayeeIdAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<string> GetEscrowPaymentIntentIdAsync(int bookingId) =>
        payment.GetEscrowPaymentIntentIdAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<int?> GetEscrowStatusAsync(int bookingId) =>
        payment.GetEscrowStatusAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<string?> GetEscrowRefundIdAsync(int bookingId) =>
        payment.GetEscrowRefundIdAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<int> GetEscrowLedgerTransactionCountAsync(int bookingId) =>
        payment.GetLedgerTransactionCountAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<long> GetEscrowLedgerSignedSumAsync(int bookingId) =>
        payment.GetLedgerSignedSumAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<long> GetEscrowLedgerPlatformRevenueAsync(int bookingId) =>
        payment.GetLedgerPlatformRevenueAsync(PaymentOperationReferences.EscrowType, Escrow(bookingId));

    public Task<string?> GetSettlementPaymentIntentIdAsync(int concertId) =>
        payment.GetLatestSettlementPaymentIntentIdAsync(
            PaymentOperationReferences.SettlementType,
            Settlement(concertId));

    public Task<int> GetSettlementLedgerTransactionCountAsync(int concertId) =>
        payment.GetLedgerTransactionCountAsync(PaymentOperationReferences.SettlementType, Settlement(concertId));

    public Task<long> GetSettlementLedgerSignedSumAsync(int concertId) =>
        payment.GetLedgerSignedSumAsync(PaymentOperationReferences.SettlementType, Settlement(concertId));

    public Task<long> GetSettlementLedgerPlatformRevenueAsync(int concertId) =>
        payment.GetLedgerPlatformRevenueAsync(PaymentOperationReferences.SettlementType, Settlement(concertId));

    private static string Escrow(int bookingId) =>
        PaymentOperationReferences.EscrowClientReference(bookingId);

    private static string Settlement(int concertId) =>
        PaymentOperationReferences.SettlementClientReference(concertId);
}
