using Aspire.Hosting.Testing;
using Concertable.Testing.Architecture;
using Xunit;

namespace Concertable.AppHost.StartupTests;

public sealed class ResourceGraphTests
{
    [Fact]
    public async Task ProductionGraph_IsValid()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        await using var app = await builder.BuildAsync();
    }

    [Fact]
    public async Task InvalidLifetimeGraph_IsRejected()
    {
        using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Concertable_AppHost>();
        builder.Services.AddInvalidLifetimeGraph();
        await Assert.ThrowsAnyAsync<Exception>(async () => await builder.BuildAsync());
    }
}
