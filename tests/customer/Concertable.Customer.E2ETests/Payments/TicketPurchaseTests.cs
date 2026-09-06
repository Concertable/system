using System.Net;
using Concertable.Seed.Identity;
using Concertable.Testing;
using Concertable.Payment.Contracts;
using Xunit;

namespace Concertable.Customer.E2ETests.Payments;

[Collection("E2E")]
public sealed class TicketPurchaseTests(AppFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Purchase_PaymentSucceeds_CreatesTicket()
    {
        var client = await fixture.CreateAuthenticatedClientAsync(fixture.SeedState.Customer1.Email);
        var upcomingConcertId = fixture.SeedState.UpcomingFlatFeeConcert.Id;

        var response = await client.PostAsync("/api/Ticket/checkout", new
        {
            ConcertId = upcomingConcertId,
            Quantity = 1
        });
        await response.ShouldBe(HttpStatusCode.OK);
        var checkout = await response.Content.ReadAsync<TicketCheckout>();
        Assert.NotNull(checkout);

        await fixture.ConfirmPaymentAsync(checkout.Session.ClientSecret);

        await fixture.Polling.UntilAsync(
            async () =>
            {
                var ticketsResponse = await client.GetAsync("/api/Ticket/upcoming/user");
                await ticketsResponse.ShouldBe(HttpStatusCode.OK);
                return await ticketsResponse.Content.ReadAsync<IEnumerable<UpcomingTicket>>();
            },
            tickets => tickets is not null && tickets.Any(t => t.Concert.Id == upcomingConcertId),
            timeout: TimeSpan.FromSeconds(30));
    }

    private sealed record TicketCheckout(
        PaymentOperationReference Reference,
        CheckoutSession Session);
    private sealed record CheckoutSession(string ClientSecret);
    private sealed record UpcomingTicket(Guid Id, UpcomingConcert Concert);
    private sealed record UpcomingConcert(int Id, string Name);
}
