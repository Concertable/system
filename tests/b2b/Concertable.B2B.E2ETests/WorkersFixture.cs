using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Concertable.B2B.E2ETests;

public sealed class WorkersFixture : IDisposable
{
    private readonly HttpClient client;
    private readonly IPollingService polling;

    public WorkersFixture(DistributedApplication app, IPollingService polling)
    {
        client = app.CreateHttpClient(AppHostConstants.ResourceNames.Workers);
        this.polling = polling;
    }

    /// <summary>
    /// Fires a timer function via the Functions host admin API; retried while the host warms up.
    /// Acceptance (202) is fire-and-forget — assert on the state the function produces.
    /// </summary>
    public async Task TriggerAsync(string functionName)
    {
        await polling.UntilAsync(
            async () =>
            {
                using var response = await client.PostAsJsonAsync(
                    $"/admin/functions/{functionName}", new { input = "" });
                return response.StatusCode == HttpStatusCode.Accepted;
            },
            timeout: TimeSpan.FromSeconds(60));
    }

    public void Dispose() => client.Dispose();
}
