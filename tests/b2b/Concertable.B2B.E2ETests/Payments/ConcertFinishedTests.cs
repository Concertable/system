using Concertable.B2B.Concert.Domain.Lifecycle;
using Concertable.B2B.TestKit;
using Concertable.Payment.TestKit;
using Concertable.Testing;
using Xunit;

namespace Concertable.B2B.E2ETests.Payments;

[Collection("E2E")]
public sealed class ConcertFinishedTests(AppFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCompleteConcert_WhenFlatFeeConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Concert.GetStateByApplicationIdAsync(fixture.SeedState.PastFlatFeeApp.Id),
            state => state == ConcertState.Complete,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ShouldCompleteConcert_WhenVenueHireConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Concert.GetStateByApplicationIdAsync(fixture.SeedState.PastVenueHireApp.Id),
            state => state == ConcertState.Complete,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ShouldCompleteBookingAndPayArtist_WhenDoorSplitConcertFinishes()
    {
        // PastDoorSplit: DoorSplit 70% — 10 tickets sold on Concertable at £20 (£200) + venue declares
        // £100 external door take → total £300 → artist share = £210 (21000 pence). Proves the split
        // settles on both channels summed, not either alone.

        // Arrange — the venue declares the external door take on top of Concertable's own sales
        await fixture.DbFixture.Concert.DeclareDoorRevenueAsync(
            fixture.SeedState.PastDoorSplitBooking.Concert.Id,
            100m);

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastDoorSplitBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeTestAccounts.BySeedUserId[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
        Assert.Equal(22000L, intent.Amount);
        Assert.Equal(21000L, intent.TransferData.Amount);

        await AssertSettlementLedgerReconcilesAsync(
            fixture.SeedState.PastDoorSplitBooking.Id, stripeCharge: intent.Amount, stripeTransfer: intent.TransferData.Amount);
    }

    [Fact]
    public async Task ShouldCompleteBookingAndPayArtist_WhenVersusConcertFinishes()
    {
        // PastVersus: Versus £100 + 70% door — 1 ticket sold on Concertable at £20, venue declares £0
        // extra door take → total £20 → artist share = £100 + £14 = £114 (11400 pence).

        // Arrange — the venue declares the external door take (£0 here; all sales came through us)
        await fixture.DbFixture.Concert.DeclareDoorRevenueAsync(
            fixture.SeedState.PastVersusBooking.Concert.Id,
            0m);

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastVersusBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeTestAccounts.BySeedUserId[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
        Assert.Equal(12400L, intent.Amount);
        Assert.Equal(11400L, intent.TransferData.Amount);

        await AssertSettlementLedgerReconcilesAsync(
            fixture.SeedState.PastVersusBooking.Id, stripeCharge: intent.Amount, stripeTransfer: intent.TransferData.Amount);
    }

    private async Task AssertSettlementLedgerReconcilesAsync(int bookingId, long stripeCharge, long stripeTransfer)
    {
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLedgerTransactionCountAsync(bookingId),
            count => count == 1,
            timeout: TimeSpan.FromSeconds(30));

        Assert.Equal(0L, await fixture.DbFixture.Payment.GetLedgerSignedSumAsync(bookingId));
        Assert.Equal(stripeCharge - stripeTransfer, await fixture.DbFixture.Payment.GetLedgerPlatformRevenueAsync(bookingId));
        Assert.Equal(1, await fixture.DbFixture.Payment.GetLedgerTransactionCountAsync(bookingId));
    }

    private Task TriggerConcertFinishedFunctionAsync() =>
        fixture.Workers.TriggerAsync(B2BTestFunctions.ConcertFinished);
}
