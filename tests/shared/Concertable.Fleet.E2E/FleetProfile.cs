using System.Security.Cryptography;

namespace Concertable.Fleet.E2E;

public enum FleetSurface
{
    B2B,
    Customer,
}

public sealed record FleetEndpoints(
    string ServiceApi,
    string SearchApi,
    string Auth,
    string PaymentApi);

public sealed record FleetProfile(FleetSurface Surface, FleetEndpoints Endpoints)
{
    public static FleetProfile B2B(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(FleetSurface.B2B, new(serviceApi, searchApi, auth, paymentApi));

    public static FleetProfile Customer(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(FleetSurface.Customer, new(serviceApi, searchApi, auth, paymentApi));
}

public sealed record FleetRun(FleetProfile Profile, string AdminKey)
{
    public const string AuthServiceAuthSecret = "concertable-e2e-auth-service-secret";
    public const string B2BServiceAuthSecret = "concertable-e2e-b2b-service-secret";
    public const string CustomerServiceAuthSecret = "concertable-e2e-customer-service-secret";

    public static FleetRun Create(FleetProfile profile) =>
        new(profile, RandomNumberGenerator.GetHexString(32));

    public static IReadOnlyDictionary<string, string> AuthEnvironmentVariables() =>
        new Dictionary<string, string>
        {
            ["ServiceAuth__B2BClientSecret"] = B2BServiceAuthSecret,
            ["ServiceAuth__CustomerClientSecret"] = CustomerServiceAuthSecret,
            ["ServiceAuth__AuthClientSecret"] = AuthServiceAuthSecret,
        };
}
