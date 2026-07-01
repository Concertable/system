using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;
using Concertable.E2ETests.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class ArtistSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly WorkflowState state;
    private readonly IStripePayment payment;
    private VenueDetailsPage venuePage = null!;

    public ArtistSteps(
        UiFixture fixture,
        Browser browser,
        WorkflowState state,
        IStripePayment payment)
    {
        this.fixture = fixture;
        this.browser = browser;
        this.state = state;
        this.payment = payment;
    }

    [When(@"the artist applies to the opportunity")]
    public async Task ArtistApplies()
    {
        await browser.UseRoleAsync(Role.ArtistManager);

        venuePage = new VenueDetailsPage(browser.Page, fixture.App.ArtistSpaUrl);
        await venuePage.GotoAsync(state.VenueId);

        var applied = browser.Page.WaitForResponseAsync($"**/api/application/{state.OpportunityId}");
        await venuePage.ApplyAsync(state.OpportunityId);
        state.ApplicationId = await ReadApplicationIdAsync(await applied);

        await venuePage.WaitUntilAppliedAsync(state.OpportunityId);
    }

    [When(@"the artist applies to the venue hire opportunity with a valid card")]
    public async Task ArtistAppliesToVenueHireWithValidCard()
    {
        await browser.UseRoleAsync(Role.ArtistManager);

        venuePage = new VenueDetailsPage(browser.Page, fixture.App.ArtistSpaUrl);
        await venuePage.GotoAsync(state.VenueId);
        await venuePage.ApplyAsync(state.OpportunityId);

        var applyCheckoutPage = new ApplyCheckoutPage(browser.Page, payment);
        var applied = browser.Page.WaitForResponseAsync($"**/api/application/{state.OpportunityId}");
        await applyCheckoutPage.PayWithSavedCardAsync();
        state.ApplicationId = await ReadApplicationIdAsync(await applied);
    }

    [Given(@"a venue hire opportunity is open for application")]
    public async Task AVenueHireOpportunityIsOpen()
    {
        var opp = fixture.App.SeedState.FreshVenueHireOpportunity;
        state.VenueId = opp.VenueId;
        state.OpportunityId = opp.Id;

        await browser.Page.GotoSpaAsync($"{fixture.App.ArtistSpaUrl}/opportunity/checkout/{state.OpportunityId}");
    }

    [When(@"the artist pays the venue hire fee with a new card")]
    public async Task PaysVenueHireFeeWithNewCard()
    {
        var applyCheckoutPage = new ApplyCheckoutPage(browser.Page, payment);
        var applied = browser.Page.WaitForResponseAsync($"**/api/application/{state.OpportunityId}");
        await applyCheckoutPage.PayWithNewCardAsync(StripeCards.Success);
        state.ApplicationId = await ReadApplicationIdAsync(await applied);
    }

    [When(@"the artist pays the venue hire fee with a declined card")]
    public Task PaysVenueHireFeeWithDeclinedCard() =>
        new ApplyCheckoutPage(browser.Page, payment).PayWithNewCardAsync(StripeCards.Decline);

    [When(@"the artist pays the venue hire fee with a 3DS card")]
    public async Task PaysVenueHireFeeWith3dsCard()
    {
        var applied = browser.Page.WaitForResponseAsync($"**/api/application/{state.OpportunityId}");
        await new ApplyCheckoutPage(browser.Page, payment).PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.CompleteChallengeAsync();
        state.ApplicationId = await ReadApplicationIdAsync(await applied);
    }

    [When(@"the artist pays the venue hire fee with a 3DS-failing card")]
    public async Task PaysVenueHireFeeWith3dsFailingCard()
    {
        await new ApplyCheckoutPage(browser.Page, payment).PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.FailChallengeAsync();
    }

    [Then(@"the application is created")]
    public Task ApplicationIsCreated() => venuePage.WaitUntilAppliedAsync(state.OpportunityId);

    private static async Task<int> ReadApplicationIdAsync(IResponse response)
    {
        var json = await response.JsonAsync();
        return json!.Value.GetProperty("id").GetInt32();
    }
}
