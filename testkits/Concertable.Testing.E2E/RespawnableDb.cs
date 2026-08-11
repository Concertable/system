using System.Data.Common;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;
using Respawn;

namespace Concertable.Testing.E2E;

public sealed class RespawnableDb
{
    private DbConnection connection = null!;
    private Respawner respawner = null!;

    public bool IsInitialized => connection is not null;
    public DbConnection Connection => connection;

    public async Task InitializeAsync(DistributedApplication app, string database, RespawnerOptions options)
    {
        var connectionString = await app.GetConnectionStringAsync(database);
        var builder = new SqlConnectionStringBuilder(connectionString) { MultipleActiveResultSets = true };
        connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        respawner = await Respawner.CreateAsync(connection, options);
    }

    public Task ResetAsync() => respawner.ResetAsync(connection);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
