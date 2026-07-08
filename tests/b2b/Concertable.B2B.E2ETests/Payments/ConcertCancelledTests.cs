using System.Net;
using Concertable.B2B.Concert.Application.DTOs;
using Concertable.B2B.Concert.Api.Responses;
using Concertable.B2B.Concert.Domain.Lifecycle;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using Concertable.Testing;
using Xunit;

namespace Concertable.B2B.E2ETests.Payments;

[Collection("E2E")]
public sealed class ConcertCancelledTests : IAsyncLifetime
{
    private readonly AppFixture fixture;

    public ConcertCancelledTests(AppFixture fixture)
    {
        this.fixture = fixture;
    }

    private HttpClient venueManagerClient = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        venueManagerClient = await fixture.CreateAuthenticatedClientAsync(fixture.SeedState.VenueManager1.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCancelBookingAndRefundEscrow_WhenFlatFeeConcertCancelled()
    {
        var appId = fixture.SeedState.FlatFeeApp.Id;

        var clientSecret = await PlaceAcceptHoldAsync(appId);
        await fixture.Stripe.ConfirmHoldAsync(clientSecret);
        await AcceptAsync(appId);

        await CancelAndAssertRefundedAsync(appId);
    }

    [Fact]
    public async Task ShouldCancelBookingAndRefundEscrow_WhenVenueHireConcertCancelled()
    {
        var appId = fixture.SeedState.VenueHireApp.Id;

        await AcceptAsync(appId);

        await CancelAndAssertRefundedAsync(appId);
    }

    private async Task CancelAndAssertRefundedAsync(int appId)
    {
        var bookingId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Booking.GetIdByApplicationIdAsync(appId),
            id => id > 0,
            timeout: TimeSpan.FromSeconds(15));

        // Escrow is created Pending on accept and only reaches Held once Stripe's charge
        // webhook confirms it; refund is valid only from Held, so wait for it.
        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetEscrowStatusAsync(bookingId),
            status => status == (int)EscrowStatus.Held,
            timeout: TimeSpan.FromSeconds(30));

        // The draft concert is created by B2B reacting to a cross-service booking event, which
        // lags the escrow hold — poll until it exists and offers the cancel action (Booked window).
        var concert = await fixture.Polling.UntilAsync(
            () => GetConcertByApplicationAsync(appId),
            c => c.Actions.Cancel is not null,
            timeout: TimeSpan.FromSeconds(30));

        var cancelResponse = await venueManagerClient.PostAsync($"/api/Concert/{concert.Id}/cancel");
        await cancelResponse.ShouldBe(HttpStatusCode.NoContent);

        await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Application.GetStateByIdAsync(appId),
            state => state == (int)LifecycleState.Cancelled,
            timeout: TimeSpan.FromSeconds(30));

        var refundId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetEscrowRefundIdAsync(bookingId),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(30));
        Assert.NotNull(refundId);

        var status = await fixture.DbFixture.Payment.GetEscrowStatusAsync(bookingId);
        Assert.Equal((int)EscrowStatus.Refunded, status);

        var refund = await fixture.Stripe.GetRefundAsync(refundId);
        Assert.Equal("succeeded", refund.Status);

        // Once cancelled, the cancel action is withdrawn from the concert response.
        await fixture.Polling.UntilAsync(
            () => GetConcertByApplicationAsync(appId),
            c => c.Actions.Cancel is null,
            timeout: TimeSpan.FromSeconds(30));
    }

    private async Task<ConcertDetailsResponse> GetConcertByApplicationAsync(int appId)
    {
        var response = await venueManagerClient.GetAsync($"/api/Concert/application/{appId}");
        await response.ShouldBe(HttpStatusCode.OK);
        var concert = await response.Content.ReadAsync<ConcertDetailsResponse>();
        Assert.NotNull(concert);
        return concert;
    }

    private async Task AcceptAsync(int appId)
    {
        var response = await venueManagerClient.PostAsync($"/api/Application/{appId}/accept", new { agreedToTerms = true });
        await response.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<string> PlaceAcceptHoldAsync(int applicationId)
    {
        var response = await venueManagerClient.PostAsync($"/api/Application/{applicationId}/checkout");
        await response.ShouldBe(HttpStatusCode.OK);
        var checkout = await response.Content.ReadAsync<CheckoutResult>();
        Assert.NotNull(checkout);
        return checkout.Session.ClientSecret;
    }

    private sealed record CheckoutResult(CheckoutSession Session);
}
