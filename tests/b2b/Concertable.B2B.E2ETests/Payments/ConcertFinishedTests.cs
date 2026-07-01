using Concertable.B2B.Concert.Api.Responses;
using Concertable.B2B.Workers.Functions;
using Concertable.B2B.Concert.Domain.Lifecycle;
using Concertable.Testing;
using Xunit;

namespace Concertable.B2B.E2ETests.Payments;

[Collection("E2E")]
public sealed class ConcertFinishedTests(AppFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCompleteBooking_WhenFlatFeeConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Application.GetStateByIdAsync(fixture.SeedState.PastFlatFeeApp.Id),
            state => state == (int)LifecycleState.Complete,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ShouldCompleteBooking_WhenVenueHireConcertFinishes()
    {
        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Application.GetStateByIdAsync(fixture.SeedState.PastVenueHireApp.Id),
            state => state == (int)LifecycleState.Complete,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ShouldCompleteBookingAndPayArtist_WhenDoorSplitConcertFinishes()
    {
        // PastDoorSplit: DoorSplit 70% — 1 ticket pre-seeded at £20 → artist share = £14 (1400 pence)

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastDoorSplitBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeE2EAccountResolver.AccountIds[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
        Assert.Equal(1400L, intent.Amount);
    }

    [Fact]
    public async Task ShouldCompleteBookingAndPayArtist_WhenVersusConcertFinishes()
    {
        // PastVersus: Versus £100 + 70% door — 1 ticket pre-seeded at £20 → artist share = £114 (11400 pence)

        // Act
        await TriggerConcertFinishedFunctionAsync();

        // Assert
        var paymentIntentId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetLatestSettlementPaymentIntentIdAsync(fixture.SeedState.PastVersusBooking.Id),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));

        var intent = await fixture.StripePaymentIntents.GetAsync(paymentIntentId);
        Assert.Equal(StripeE2EAccountResolver.AccountIds[fixture.SeedState.ArtistManager1.Id], intent.TransferData.DestinationId);
        Assert.Equal(11400L, intent.Amount);
    }

    private Task TriggerConcertFinishedFunctionAsync() =>
        fixture.Workers.TriggerAsync(nameof(ConcertFinishedFunction));
}
