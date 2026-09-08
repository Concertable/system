using Concertable.B2B.TestKit;

namespace Concertable.B2B.E2ETests;

public sealed class BookingDb
{
    private readonly B2BTestClient client;

    public BookingDb(B2BTestClient client)
    {
        this.client = client;
    }

    public Task<int> GetIdByApplicationIdAsync(int applicationId) =>
        client.GetBookingIdAsync(applicationId);

    public async Task<BookingState> GetStateByApplicationIdAsync(int applicationId) =>
        (BookingState)await client.GetBookingStateByApplicationIdAsync(applicationId);
}
