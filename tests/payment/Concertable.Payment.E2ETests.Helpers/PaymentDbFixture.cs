using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Concertable.Payment.Hosting;
using Concertable.Payment.TestKit;
using Concertable.Testing.E2E;

namespace Concertable.Payment.E2ETests.Helpers;

/// <summary>
/// Resets Payment through its E2E-only TestKit endpoint.
/// </summary>
public sealed class PaymentDbFixture
{
    private PaymentTestClient client = null!;
    public PaymentDb Payment { get; private set; } = null!;
    public bool IsInitialized { get; private set; }

    public Task InitializeAsync(DistributedApplication app, string adminKey)
    {
        client = new PaymentTestClient(
            app.CreateHttpClient(PaymentConstants.WebResource),
            adminKey);
        Payment = new PaymentDb(client);
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public Task ResetAsync() => client.ResetAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
