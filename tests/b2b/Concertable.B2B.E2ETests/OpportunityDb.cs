using Concertable.B2B.TestKit;

namespace Concertable.B2B.E2ETests;

public sealed class OpportunityDb
{
    private readonly B2BTestClient client;

    public OpportunityDb(B2BTestClient client)
    {
        this.client = client;
    }

    public Task<int> GetNewestAsync(int venueId) =>
        client.GetNewestOpportunityIdAsync(venueId);
}
