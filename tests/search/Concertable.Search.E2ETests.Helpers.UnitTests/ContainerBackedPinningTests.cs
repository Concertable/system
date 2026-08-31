using Aspire.Hosting;
using Concertable.Auth.Hosting;
using Concertable.Payment.Hosting;
using Concertable.Testing.E2E;

namespace Concertable.Search.E2ETests.Helpers.UnitTests;

public sealed class ContainerBackedPinningTests
{
    [Fact]
    public void GetRequiredResource_ImageBackedSystemServices_ReturnsEveryPinningTarget()
    {
        var builder = DistributedApplication.CreateBuilder();
        var auth = builder.AddContainer(AuthConstants.Resource, "test-image").Resource;
        var searchWeb = builder.AddContainer("search-web", "test-image").Resource;
        var paymentWeb = builder.AddContainer(PaymentConstants.WebResource, "test-image").Resource;
        var paymentWorkers = builder.AddContainer(PaymentConstants.WorkersResource, "test-image").Resource;

        Assert.Same(auth, builder.GetRequiredResource(AuthConstants.Resource));
        Assert.Same(searchWeb, builder.GetRequiredResource("search-web"));
        Assert.Same(paymentWeb, builder.GetRequiredResource(PaymentConstants.WebResource));
        Assert.Same(paymentWorkers, builder.GetRequiredResource(PaymentConstants.WorkersResource));
    }
}
