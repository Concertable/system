using System.Net;
using Concertable.B2B.Concert.Application.DTOs;
using Concertable.B2B.Concert.Api.Responses;
using Concertable.Payment.Client;
using Concertable.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Concertable.B2B.E2ETests.Payments;

[Collection("E2E")]
public sealed class ConcertDraftTests : IAsyncLifetime
{
    private readonly AppFixture fixture;
    private readonly ITestOutputHelper output;

    public ConcertDraftTests(AppFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.output = output;
    }

    private HttpClient venueManagerClient = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        venueManagerClient = await fixture.CreateAuthenticatedClientAsync(fixture.SeedState.VenueManager1.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCreateDraftAndPayArtist_WhenFlatFeeApplicationAccepted()
    {
        var clientSecret = await PlaceAcceptHoldAsync(fixture.SeedState.FlatFeeApp.Id);
        await fixture.Stripe.ConfirmHoldAsync(clientSecret);

        var acceptResponse = await venueManagerClient.PostAsync(
            $"/api/Application/{fixture.SeedState.FlatFeeApp.Id}/accept");
        await acceptResponse.ShouldBe(HttpStatusCode.NoContent);

        var bookingId = await fixture.DbFixture.Booking.GetIdByApplicationIdAsync(fixture.SeedState.FlatFeeApp.Id);
        var escrowPayeeId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetEscrowPayeeIdAsync(bookingId),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(15));
        var artistTenantId = fixture.SeedState.Tenants.Single(t => t.CreatedByUserId == fixture.SeedState.ArtistManager1.Id).Id;
        Assert.Equal(artistTenantId, escrowPayeeId);

        await fixture.Polling.UntilAsync(
            async () =>
            {
                var response = await venueManagerClient.GetAsync($"/api/Application/{fixture.SeedState.FlatFeeApp.Id}");
                await response.ShouldBe(HttpStatusCode.OK);
                return await response.Content.ReadAsync<ApplicationResponse>();
            },
            app => app?.Status == ApplicationStatus.Accepted,
            timeout: TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task ShouldCreateDraftAndPayVenue_WhenVenueHireApplicationAccepted()
    {
        var response = await venueManagerClient.PostAsync(
            $"/api/Application/{fixture.SeedState.VenueHireApp.Id}/accept");
        await response.ShouldBe(HttpStatusCode.NoContent);

        var bookingId = await fixture.DbFixture.Booking.GetIdByApplicationIdAsync(fixture.SeedState.VenueHireApp.Id);
        var escrowPayeeId = await fixture.Polling.UntilAsync(
            () => fixture.DbFixture.Payment.GetEscrowPayeeIdAsync(bookingId),
            id => id is not null,
            timeout: TimeSpan.FromSeconds(15));
        var venueTenantId = fixture.SeedState.Tenants.Single(t => t.CreatedByUserId == fixture.SeedState.VenueManager1.Id).Id;
        Assert.Equal(venueTenantId, escrowPayeeId);

        await fixture.Polling.UntilAsync(
            async () =>
            {
                var response = await venueManagerClient.GetAsync($"/api/Application/{fixture.SeedState.VenueHireApp.Id}");
                await response.ShouldBe(HttpStatusCode.OK);
                return await response.Content.ReadAsync<ApplicationResponse>();
            },
            app => app?.Status == ApplicationStatus.Accepted,
            timeout: TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task ShouldCreateDraft_WhenDoorSplitApplicationAccepted()
    {
        var acceptResponse = await venueManagerClient.PostAsync(
            $"/api/Application/{fixture.SeedState.DoorSplitApp.Id}/accept",
            new { PaymentMethodId = AppFixture.TestPaymentMethodId });
        await acceptResponse.ShouldBe(HttpStatusCode.NoContent);

        var applicationResponse = await venueManagerClient.GetAsync($"/api/Application/{fixture.SeedState.DoorSplitApp.Id}");
        await applicationResponse.ShouldBe(HttpStatusCode.OK);
        var application = await applicationResponse.Content.ReadAsync<ApplicationResponse>();
        Assert.Equal(ApplicationStatus.Accepted, application!.Status);
    }

    [Fact]
    public async Task ShouldCreateDraft_WhenVersusApplicationAccepted()
    {
        var acceptResponse = await venueManagerClient.PostAsync(
            $"/api/Application/{fixture.SeedState.VersusApp.Id}/accept",
            new { PaymentMethodId = AppFixture.TestPaymentMethodId });
        await acceptResponse.ShouldBe(HttpStatusCode.NoContent);

        var applicationResponse = await venueManagerClient.GetAsync($"/api/Application/{fixture.SeedState.VersusApp.Id}");
        await applicationResponse.ShouldBe(HttpStatusCode.OK);
        var application = await applicationResponse.Content.ReadAsync<ApplicationResponse>();
        Assert.Equal(ApplicationStatus.Accepted, application!.Status);
    }

    private async Task<string> PlaceAcceptHoldAsync(int applicationId)
    {
        var response = await venueManagerClient.PostAsync($"/api/Application/{applicationId}/checkout");
        await response.ShouldBe(HttpStatusCode.OK);
        var checkout = await response.Content.ReadAsync<CheckoutResult>();
        return checkout!.Session.ClientSecret;
    }

    private sealed record CheckoutResult(CheckoutSession Session);
}
