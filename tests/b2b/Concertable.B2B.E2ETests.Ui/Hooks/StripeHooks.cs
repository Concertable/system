using Concertable.B2B.E2ETests.Ui.Support;
using Concertable.Seed.Identity;

namespace Concertable.B2B.E2ETests.Ui.Hooks;

[Binding]
public sealed class StripeHooks(UiFixture fixture)
{
    [BeforeTestRun(Order = 2)]
    public static async Task DetachAllCardsBeforeTestRun()
    {
        await PlaywrightHooks.Fixture.App.ResetAsync();
        await DetachSeededCustomerCardsAsync(PlaywrightHooks.Fixture.App);
    }

    [BeforeScenario("ResetsStripe", Order = 2)]
    public async Task DetachSavedPaymentMethodsAsync() =>
        await DetachSeededCustomerCardsAsync(fixture.App);

    private static async Task DetachSeededCustomerCardsAsync(AppFixture app)
    {
        var seedData = app.SeedState;
        var customerIds = new[]
        {
            app.StripeCustomerResolver.Resolve(seedData.VenueManager1.Id),
            app.StripeCustomerResolver.Resolve(seedData.ArtistManager1.Id),
            app.StripeCustomerResolver.Resolve(SeedCustomers.CustomerId(1)),
        };

        foreach (var id in customerIds)
        {
            await app.Stripe.EnsureNoCardsAttachedAsync(id);
            await app.Stripe.AttachTestCardAsync(id);
        }
    }
}
