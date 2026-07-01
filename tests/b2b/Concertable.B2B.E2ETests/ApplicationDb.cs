using System.Data;
using Dapper;

namespace Concertable.B2B.E2ETests;

public sealed class ApplicationDb
{
    private readonly IDbConnection connection;

    public ApplicationDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    public Task<int> GetStateByIdAsync(int applicationId) =>
        connection.QuerySingleAsync<int>(
            "SELECT State FROM concert.Applications WHERE Id = @applicationId",
            new { applicationId });
}
