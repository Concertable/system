using System.Net;
using System.Text.Json;
using Xunit;

namespace Concertable.Qualification.E2ETests;

[Collection(SystemCollection.Name)]
public sealed class CompositionQualificationTests(SystemFixture system)
{
    [Fact]
    public void EveryPinnedService_ExposesAnEndpoint()
    {
        Assert.Equal(
            system.Manifest.ServiceNames.Order(),
            system.HttpServices.Keys.Concat(WorkerServices).Order());
    }

    [Theory]
    [MemberData(nameof(HttpServiceNames))]
    public async Task HttpService_ServesLivenessAndReadiness(string service)
    {
        var origin = system.HttpServices[service];

        foreach (var probe in new[] { "health", "alive" })
        {
            using var response = await system.Client.GetAsync(new Uri(origin, probe));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Auth_ServesOpenIdConfigurationForTheFleet()
    {
        var origin = system.HttpServices["auth"];

        using var response = await system.Client.GetAsync(
            new Uri(origin, ".well-known/openid-configuration"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            origin.GetLeftPart(UriPartial.Authority),
            new Uri(document.RootElement.GetProperty("issuer").GetString()!).GetLeftPart(UriPartial.Authority));
        Assert.NotEmpty(document.RootElement.GetProperty("token_endpoint").GetString()!);
    }

    [Fact]
    public async Task ProtectedApi_RefusesAnAnonymousCaller()
    {
        using var response = await system.Client.GetAsync(new Uri(system.HttpServices["b2b-web"], "api/Venue"));

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    public static TheoryData<string> HttpServiceNames() =>
        new("auth", "b2b-web", "customer-web", "payment-web", "search-web");

    private static readonly string[] WorkerServices =
        ["b2b-workers", "payment-workers", "search-workers"];
}
