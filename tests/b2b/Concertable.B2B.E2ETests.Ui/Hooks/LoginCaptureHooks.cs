using System.Text.Json;
using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Hooks;

public static class LoginCaptureHooks
{
    // A captured session must stay valid for the whole scenario that borrows it. The longest B2B
    // scenario runs a little over a minute, so anything expiring sooner than this is re-captured
    // rather than handed out to expire mid-scenario.
    private static readonly TimeSpan RemainingLifetimeRequired = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<SeededUser, string> storageStateByUser = [];

    public static void Reset() => storageStateByUser.Clear();

    public static async Task<string> GetOrCaptureAsync(UiFixture fixture, SeededUser user)
    {
        if (storageStateByUser.TryGetValue(user, out var state) && IsUsable(state))
            return state;

        var seed = fixture.App.SeedState;
        var (email, password, spaUrl) = user switch
        {
            SeededUser.VenueManager  => (seed.VenueManager1.Email,  SeedState.TestPassword, fixture.App.VenueSpaUrl),
            SeededUser.ArtistManager => (seed.ArtistManager1.Email, SeedState.TestPassword, fixture.App.ArtistSpaUrl),
            _ => throw new ArgumentOutOfRangeException(nameof(user))
        };

        await CaptureAsync(fixture, user, email, password, spaUrl);
        return storageStateByUser[user];
    }

    private static async Task CaptureAsync(UiFixture fixture, SeededUser user, string email, string password, string spaUrl)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var login = new LoginPage(page, spaUrl);

        await login.GotoAsync();
        await login.SignInAsync(email, password);
        await page.WaitForURLAsync($"{spaUrl}/");

        storageStateByUser[user] = await context.StorageStateAsync();
    }

    // The SPA keeps its oidc-client-ts session in localStorage, and silent renew fires once the access
    // token lapses. Replaying a lapsed session cannot renew it: the DB reset between scenarios drops the
    // persisted grant, and the one-time refresh token has already been spent, so the renew returns
    // invalid_grant and the app lands signed out. Read the session's own expiry rather than trusting it.
    private static bool IsUsable(string storageState)
    {
        using var document = JsonDocument.Parse(storageState);
        if (!document.RootElement.TryGetProperty("origins", out var origins))
            return false;

        var expiries = origins.EnumerateArray()
            .Where(origin => origin.TryGetProperty("localStorage", out _))
            .SelectMany(origin => origin.GetProperty("localStorage").EnumerateArray())
            .Where(entry => entry.GetProperty("name").GetString()?.StartsWith("oidc.user:", StringComparison.Ordinal) == true)
            .Select(entry => ReadExpiry(entry.GetProperty("value").GetString()))
            .OfType<DateTimeOffset>()
            .ToList();

        return expiries.Count > 0
            && expiries.TrueForAll(expiry => expiry - DateTimeOffset.UtcNow > RemainingLifetimeRequired);
    }

    private static DateTimeOffset? ReadExpiry(string? oidcUser)
    {
        if (string.IsNullOrEmpty(oidcUser))
            return null;

        using var document = JsonDocument.Parse(oidcUser);
        return document.RootElement.TryGetProperty("expires_at", out var expiresAt)
            && expiresAt.TryGetInt64(out var epochSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds)
                : null;
    }
}
