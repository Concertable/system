using Concertable.B2B.TestKit;

namespace Concertable.B2B.E2ETests;

public sealed class ConcertDb
{
    private readonly B2BTestClient client;

    public ConcertDb(B2BTestClient client)
    {
        this.client = client;
    }

    /* Models the venue declaring the night's door take. Until the declare endpoint lands (Phase 2)
       the E2E arrange writes it straight to the column the endpoint will set. */
    public Task DeclareDoorRevenueAsync(int concertId, decimal doorRevenue) =>
        client.DeclareDoorRevenueAsync(concertId, doorRevenue);
}
