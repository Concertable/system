using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Hooks;

public static class LoginCaptureHooks
{
    private static readonly Dictionary<Role, string> storageStateByRole = [];

    public static void Reset() => storageStateByRole.Clear();

    public static async Task<string> GetOrCaptureAsync(UiFixture fixture, Role role)
    {
        if (storageStateByRole.TryGetValue(role, out var state))
            return state;

        var seed = fixture.App.SeedState;
        var (email, password, spaUrl) = role switch
        {
            Role.VenueManager  => (seed.VenueManager1.Email,  SeedState.TestPassword, fixture.App.VenueSpaUrl),
            Role.ArtistManager => (seed.ArtistManager1.Email, SeedState.TestPassword, fixture.App.ArtistSpaUrl),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        await CaptureAsync(fixture, role, email, password, spaUrl);
        return storageStateByRole[role];
    }

    private static async Task CaptureAsync(UiFixture fixture, Role role, string email, string password, string spaUrl)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var login = new LoginPage(page, spaUrl);

        await login.GotoAsync();
        await login.SignInAsync(email, password);
        await page.WaitForURLAsync($"{spaUrl}/");

        storageStateByRole[role] = await context.StorageStateAsync();
    }
}
