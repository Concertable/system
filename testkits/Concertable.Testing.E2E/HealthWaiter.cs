using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Concertable.Testing.E2E;

public sealed class HealthWaiter : IDisposable
{
    private readonly ILogger<HealthWaiter> logger;
    private readonly HttpClient httpClient;

    public HealthWaiter(ILogger<HealthWaiter> logger)
    {
        this.logger = logger;
        this.httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, _, _, _) =>
                message.RequestUri?.IsLoopback == true
        });
    }

    public async Task WaitForAllHealthyAsync(IEnumerable<string> baseUrls, TimeSpan timeout, CancellationToken ct = default)
    {
        logger.WaitingForAppToBeHealthy(string.Join(", ", baseUrls));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        await Task.WhenAll(baseUrls.Select(url => PollUntilSuccessAsync($"{url}/health", cts.Token)));

        logger.AppIsHealthy();
    }

    public async Task WaitForAllServingAsync(IEnumerable<string> spaUrls, TimeSpan timeout, CancellationToken ct = default)
    {
        logger.WaitingForSpasToServe(string.Join(", ", spaUrls));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        await Task.WhenAll(spaUrls.Select(url => PollUntilSuccessAsync(url, cts.Token)));

        logger.SpasAreServing();
    }

    public async Task WaitForPayoutAccountsAsync(string paymentConnectionString, int expectedCount, TimeSpan timeout)
    {
        await using var connection = new SqlConnection(paymentConnectionString);
        await connection.OpenAsync();

        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            var count = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM payment.PayoutAccounts WHERE StripeAccountId IS NOT NULL");

            if (count >= expectedCount)
                return;

            try { await Task.Delay(TimeSpan.FromSeconds(2), cts.Token); }
            catch (OperationCanceledException) { break; }
        }

        throw new TimeoutException("Timed out waiting for PayoutAccounts to be provisioned.");
    }

    private async Task PollUntilSuccessAsync(string url, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode) return;
                logger.HealthCheckError(url, $"HTTP {(int)response.StatusCode}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.HealthCheckError(url, ex.Message);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException($"Readiness check timed out for {url}");
    }

    public void Dispose() => httpClient.Dispose();
}
