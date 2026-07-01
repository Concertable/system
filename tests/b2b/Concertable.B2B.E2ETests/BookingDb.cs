using System.Data;
using Dapper;

namespace Concertable.B2B.E2ETests;

public sealed class BookingDb
{
    private readonly IDbConnection connection;

    public BookingDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    public Task<int> GetIdByApplicationIdAsync(int applicationId) =>
        connection.QuerySingleAsync<int>(
            "SELECT Id FROM concert.Bookings WHERE ApplicationId = @applicationId",
            new { applicationId });
}
