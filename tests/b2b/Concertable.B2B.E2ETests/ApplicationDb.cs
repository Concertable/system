using Concertable.B2B.TestKit;

namespace Concertable.B2B.E2ETests;

public sealed class ApplicationDb
{
    private readonly B2BTestClient client;

    public ApplicationDb(B2BTestClient client)
    {
        this.client = client;
    }

    public Task<int> GetStateByIdAsync(int applicationId) =>
        client.GetApplicationStateAsync(applicationId);
}
