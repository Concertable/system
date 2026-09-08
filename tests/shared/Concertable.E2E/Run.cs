using System.Globalization;
using System.Security.Cryptography;

namespace Concertable.E2E;

public sealed record Run(Profile Profile, string AdminKey)
{
    public const string AuthServiceAuthSecret = "concertable-e2e-auth-service-secret";
    public const string B2BServiceAuthSecret = "concertable-e2e-b2b-service-secret";
    public const string CustomerServiceAuthSecret = "concertable-e2e-customer-service-secret";

    public static Run Create(Profile profile) =>
        new(profile, RandomNumberGenerator.GetHexString(32));

    public static IReadOnlyDictionary<string, string> AuthEnvironmentVariables() =>
        new Dictionary<string, string>
        {
            ["ServiceAuth__B2BClientSecret"] = B2BServiceAuthSecret,
            ["ServiceAuth__CustomerClientSecret"] = CustomerServiceAuthSecret,
            ["ServiceAuth__AuthClientSecret"] = AuthServiceAuthSecret,
            // The browser suites reset their database and login state between scenarios while Auth stays
            // alive. Keep the production credential policy enabled, but remove its cross-scenario budget
            // from E2E just as the in-process integration fixtures do.
            ["RateLimiting__credential__PermitLimit"] = int.MaxValue.ToString(CultureInfo.InvariantCulture),
        };
}
