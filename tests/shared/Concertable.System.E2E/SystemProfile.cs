using System.Security.Cryptography;

namespace Concertable.SystemTesting.E2E;

public enum SystemSurface
{
    B2B,
    Customer,
}

public sealed record SystemEndpoints(
    string ServiceApi,
    string SearchApi,
    string Auth,
    string PaymentApi);

public sealed record SystemProfile(SystemSurface Surface, SystemEndpoints Endpoints)
{
    public static SystemProfile B2B(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(SystemSurface.B2B, new(serviceApi, searchApi, auth, paymentApi));

    public static SystemProfile Customer(
        string serviceApi,
        string searchApi,
        string auth,
        string paymentApi) =>
        new(SystemSurface.Customer, new(serviceApi, searchApi, auth, paymentApi));
}

public sealed record SystemRun(SystemProfile Profile, string AdminKey)
{
    public const string AuthServiceAuthSecret = "concertable-e2e-auth-service-secret";
    public const string B2BServiceAuthSecret = "concertable-e2e-b2b-service-secret";
    public const string CustomerServiceAuthSecret = "concertable-e2e-customer-service-secret";

    public static SystemRun Create(SystemProfile profile) =>
        new(profile, RandomNumberGenerator.GetHexString(32));

    public static IReadOnlyDictionary<string, string> AuthEnvironmentVariables() =>
        new Dictionary<string, string>
        {
            ["ServiceAuth__B2BClientSecret"] = B2BServiceAuthSecret,
            ["ServiceAuth__CustomerClientSecret"] = CustomerServiceAuthSecret,
            ["ServiceAuth__AuthClientSecret"] = AuthServiceAuthSecret,
        };
}
