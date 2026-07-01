using System.Data;
using Dapper;

namespace Concertable.B2B.E2ETests;

public sealed class OpportunityDb
{
    private readonly IDbConnection connection;

    public OpportunityDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    public Task<int> GetNewestAsync(int venueId) =>
        connection.QuerySingleAsync<int>(
            "SELECT MAX(Id) FROM concert.Opportunities WHERE VenueId = @venueId",
            new { venueId });
}
