using System.Net;
using Concertable.B2B.Concert.Api.Responses;
using Concertable.B2B.Workers.Functions;
using Concertable.Testing;
using Xunit;

namespace Concertable.B2B.E2ETests.Payments;

[Collection("E2E")]
public sealed class ConcertFinishedTests(AppFixture fixture) : IAsyncLifetime
{
    private HttpClient venueManagerClient = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        venueManagerClient = await fixture.CreateAuthenticatedClientAsync(fixture.SeedState.VenueManager1.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCompleteConcert_WhenFlatFeeConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => GetConcertByApplicationAsync(fixture.SeedState.PastFlatFeeApp.Id),
            concert => concert.Actions.Invoice is not null,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ShouldCompleteConcert_WhenVenueHireConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => GetConcertByApplicationAsync(fixture.SeedState.PastVenueHireApp.Id),
            concert => concert.Actions.Invoice is not null,
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
            fixture.SeedState.ConcertFor(fixture.SeedState.PastDoorSplitBooking).Id,
            100m);

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastDoorSplitBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeAccountResolver.AccountIds[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
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
            fixture.SeedState.ConcertFor(fixture.SeedState.PastVersusBooking).Id,
            0m);

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastVersusBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeAccountResolver.AccountIds[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
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
        fixture.Workers.TriggerAsync(nameof(ConcertFinishedFunction));

    private async Task<MyDetailsResponse> GetConcertByApplicationAsync(int applicationId)
    {
        var response = await venueManagerClient.GetAsync($"/api/concert/application/{applicationId}");
        await response.ShouldBe(HttpStatusCode.OK);
        var concert = await response.Content.ReadAsync<MyDetailsResponse>();
        Assert.NotNull(concert);
        return concert;
    }
}
