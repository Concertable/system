using Dapper;
using Microsoft.Data.SqlClient;

namespace Concertable.Payment.E2ETests.Helpers;

/// <summary>
/// Reads how far Payment has provisioned its payout owners. A tenant that both receives and pays needs both
/// halves — the connect account it is paid into and the customer it is charged through — while a consumer who
/// only ever buys is provisioned the customer half alone.
/// </summary>
public sealed class PayoutAccountDb
{
    private readonly string connectionString;

    public PayoutAccountDb(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<IReadOnlyCollection<Guid>> GetPayableOwnerIdsAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        return (await connection.QueryAsync<Guid>(
            """
            SELECT OwnerId FROM payment.PayoutAccounts
            WHERE StripeAccountId IS NOT NULL AND StripeCustomerId IS NOT NULL
            """)).ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> GetChargeableOwnerIdsAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        return (await connection.QueryAsync<Guid>(
            """
            SELECT OwnerId FROM payment.PayoutAccounts
            WHERE StripeCustomerId IS NOT NULL
            """)).ToList();
    }
}
