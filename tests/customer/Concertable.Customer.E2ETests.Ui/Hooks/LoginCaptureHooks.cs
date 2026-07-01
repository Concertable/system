using Concertable.Customer.E2ETests.Ui.PageObjects;
using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Hooks;

public static class LoginCaptureHooks
{
    private static string? storageState;

    public static void Reset() => storageState = null;

    public static async Task<string> GetOrCaptureAsync(UiFixture fixture)
    {
        if (storageState is not null)
            return storageState;

        var seed = fixture.App.SeedState;
        var spaUrl = fixture.App.CustomerSpaUrl;

        await using var context = await fixture.Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        var login = new LoginPage(page, spaUrl);

        await login.GotoAsync();
        await login.SignInAsync(seed.Customer1.Email, SeedState.TestPassword);
        await page.WaitForURLAsync($"{spaUrl}/");

        storageState = await context.StorageStateAsync();
        return storageState;
    }
}
