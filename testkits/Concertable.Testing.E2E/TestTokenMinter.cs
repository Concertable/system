using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Concertable.Testing.E2E;

public sealed class TestTokenMinter : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string authBaseUrl;

    public TestTokenMinter(IConfiguration configuration)
    {
        authBaseUrl = configuration["Endpoints:Auth"]
            ?? throw new InvalidOperationException("Endpoints:Auth is not configured.");

        httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
    }

    public async Task<string> MintAsync(string email, string password)
    {
        using var response = await RequestAsync(email, password);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    public Task WaitUntilMintableAsync(string email, string password, IPollingService polling) =>
        polling.UntilAsync(
            async () =>
            {
                using var response = await RequestAsync(email, password);
                return response.IsSuccessStatusCode;
            },
            timeout: TimeSpan.FromSeconds(30));

    private Task<HttpResponseMessage> RequestAsync(string email, string password) =>
        httpClient.PostAsync($"{authBaseUrl}/connect/token",
            new FormUrlEncodedContent([
                new("grant_type", "password"),
                new("client_id", "concertable-test"),
                new("username", email),
                new("password", password),
                new("scope", "concertable.b2b.api concertable.customer.api concertable.search.api"),
            ]));

    public void Dispose() => httpClient.Dispose();
}
