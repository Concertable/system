using System.Data;
using Dapper;

namespace Concertable.B2B.E2ETests;

public sealed class ConcertDb
{
    private readonly IDbConnection connection;

    public ConcertDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    /* Models the venue declaring the night's door take. Until the declare endpoint lands (Phase 2)
       the E2E arrange writes it straight to the column the endpoint will set. */
    public Task DeclareDoorRevenueAsync(int concertId, decimal doorRevenue) =>
        connection.ExecuteAsync(
            "UPDATE concert.Concerts SET DoorRevenue = @doorRevenue WHERE Id = @concertId",
            new { concertId, doorRevenue });
}
