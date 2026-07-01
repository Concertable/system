using System.Net;
using Concertable.Seed.Identity;
using Concertable.Testing;
using Xunit;

namespace Concertable.Customer.E2ETests.Payments;

[Collection("E2E")]
public sealed class TicketPurchaseTests(AppFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldCreateTicket_WhenPaymentSucceeds()
    {
        // Arrange
        var client = await fixture.CreateAuthenticatedClientAsync(SeedCustomers.CustomerEmail(1));
        var upcomingConcertId = fixture.Catalog.Concerts.First(c => c.Name == "Upcoming FlatFee Show").ConcertId;

        // Act
        var response = await client.PostAsync("/api/Ticket/purchase", new
        {
            ConcertId = upcomingConcertId,
            Quantity = 1,
            PaymentMethodId = AppFixture.TestPaymentMethodId
        });
        await response.ShouldBe(HttpStatusCode.OK);

        // Assert
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

    private sealed record UpcomingTicket(Guid Id, UpcomingConcert Concert);
    private sealed record UpcomingConcert(int Id, string Name);
}
