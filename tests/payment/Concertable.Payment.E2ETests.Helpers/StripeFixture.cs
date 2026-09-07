using Stripe;

namespace Concertable.Payment.E2ETests.Helpers;

public sealed class StripeFixture
{
    private readonly PaymentIntentService paymentIntents;
    private readonly SetupIntentService setupIntents;
    private readonly TransferService transfers;
    private readonly PaymentMethodService paymentMethods;
    private readonly RefundService refunds;

    public DateTime LastReset { get; private set; }

    public void Reset() => LastReset = DateTime.UtcNow;

    public StripeFixture(IStripeClient client)
    {
        paymentIntents = new PaymentIntentService(client);
        setupIntents = new SetupIntentService(client);
        transfers = new TransferService(client);
        paymentMethods = new PaymentMethodService(client);
        refunds = new RefundService(client);
    }

    public Task<Refund> GetRefundAsync(string refundId, CancellationToken ct = default) =>
        refunds.GetAsync(refundId, cancellationToken: ct);

    public async Task EnsureNoCardsAttachedAsync(string customerId, CancellationToken ct = default)
    {
        var list = await paymentMethods.ListAsync(
            new PaymentMethodListOptions { Customer = customerId, Type = "card", Limit = 100 },
            cancellationToken: ct);

        foreach (var paymentMethod in list.Data.Where(pm => pm.CustomerId == customerId))
            await EnsureDetachedFromCustomerAsync(paymentMethod.Id, customerId, ct);
    }

    private async Task EnsureDetachedFromCustomerAsync(
        string paymentMethodId,
        string customerId,
        CancellationToken ct)
    {
        try
        {
            await paymentMethods.DetachAsync(paymentMethodId, cancellationToken: ct);
        }
        catch (StripeException)
        {
            var current = await paymentMethods.GetAsync(paymentMethodId, cancellationToken: ct);

            if (current.CustomerId == customerId)
                throw;
        }
    }

    public async Task AttachTestCardAsync(string customerId, CancellationToken ct = default)
    {
        var attached = await paymentMethods.AttachAsync(
            "pm_card_visa",
            new PaymentMethodAttachOptions { Customer = customerId },
            cancellationToken: ct);

        // A customer session only re-offers a saved method marked `always`.
        await paymentMethods.UpdateAsync(
            attached.Id,
            new PaymentMethodUpdateOptions { AllowRedisplay = "always" },
            cancellationToken: ct);
    }

    public Task ConfirmHoldAsync(string clientSecret, string paymentMethodId = "pm_card_visa", CancellationToken ct = default) =>
        paymentIntents.ConfirmAsync(
            clientSecret.Split("_secret_")[0],
            new PaymentIntentConfirmOptions { PaymentMethod = paymentMethodId },
            cancellationToken: ct);

    public Task ConfirmPaymentMethodAsync(string clientSecret, string paymentMethodId = "pm_card_visa", CancellationToken ct = default) =>
        setupIntents.ConfirmAsync(
            clientSecret.Split("_secret_")[0],
            new SetupIntentConfirmOptions { PaymentMethod = paymentMethodId },
            cancellationToken: ct);

    public async Task<PaymentIntent?> GetCapturedHoldAsync(
        string paymentIntentId,
        decimal amount,
        CancellationToken ct = default)
    {
        var paymentIntent = await paymentIntents.GetAsync(paymentIntentId, cancellationToken: ct);

        return paymentIntent.Amount == ToMinorUnits(amount)
            && paymentIntent.Status == "succeeded"
                ? paymentIntent
                : null;
    }

    public async Task<Transfer?> FindTransferAsync(string stripeAccountId, decimal amount)
    {
        var results = await transfers.ListAsync(new TransferListOptions
        {
            Destination = stripeAccountId,
            Created = new DateRangeOptions { GreaterThanOrEqual = LastReset }
        });
        return results.Data.SingleOrDefault(t => t.Amount == ToMinorUnits(amount));
    }

    private static long ToMinorUnits(decimal amount) => decimal.ToInt64(amount * 100m);

}
