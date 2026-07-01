using Concertable.B2B.E2ETests.Ui.PageObjects;
using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.Steps;

[Binding]
public sealed class VenueManagerSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly WorkflowState state;
    private readonly IStripePayment payment;
    private MyVenuePage myVenuePage = null!;

    public VenueManagerSteps(
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

    [When(@"the venue manager posts a flat fee opportunity for £(\d+)")]
    public async Task PostsFlatFeeOpportunity(decimal fee)
    {
        state.VenueId = fixture.App.SeedState.Venue.Id;

        myVenuePage = new MyVenuePage(browser.Page, fixture.App.VenueSpaUrl);
        await myVenuePage.GotoAsync();
        await myVenuePage.PostFlatFeeOpportunityAsync(fee);
        await myVenuePage.WaitUntilSavedAsync();

        state.OpportunityId = await FetchNewestOpportunityIdAsync(state.VenueId);
    }

    [When(@"the venue manager accepts and pays with a valid card")]
    public Task AcceptsAndPaysWithValidCard() => AcceptWithSavedCardAsync(verify: false);

    [When(@"the venue manager accepts and registers a valid card")]
    public Task AcceptsAndRegistersValidCard() => AcceptWithSavedCardAsync(verify: true);

    private async Task AcceptWithSavedCardAsync(bool verify)
    {
        await browser.UseRoleAsync(Role.VenueManager);

        var applicationsPage = new ApplicationsPage(browser.Page, fixture.App.VenueSpaUrl);
        await applicationsPage.GotoAsync(state.OpportunityId);
        await applicationsPage.ClickAcceptAsync(state.ApplicationId);

        var acceptPage = new AcceptApplicationPage(browser.Page);
        await acceptPage.ClickConfirmAsync();

        await browser.Page.WaitForURLAsync("**/applications/*/checkout", new() { Timeout = 15_000 });

        var checkoutPage = new ApplicationCheckoutPage(browser.Page, payment);
        if (verify)
            await checkoutPage.SubmitWithSavedCardAndVerifyAsync();
        else
            await checkoutPage.SubmitWithSavedCardAsync();
    }

    [When(@"the venue manager posts a venue hire opportunity for £(\d+)")]
    public async Task PostsVenueHireOpportunity(decimal fee)
    {
        state.VenueId = fixture.App.SeedState.Venue.Id;

        myVenuePage = new MyVenuePage(browser.Page, fixture.App.VenueSpaUrl);
        await myVenuePage.GotoAsync();
        await myVenuePage.PostVenueHireOpportunityAsync(fee);
        await myVenuePage.WaitUntilSavedAsync();

        state.OpportunityId = await FetchNewestOpportunityIdAsync(state.VenueId);
    }

    [When(@"the venue manager posts a door split opportunity for (\d+)% door")]
    public async Task PostsDoorSplitOpportunity(int doorPercent)
    {
        state.VenueId = fixture.App.SeedState.Venue.Id;

        myVenuePage = new MyVenuePage(browser.Page, fixture.App.VenueSpaUrl);
        await myVenuePage.GotoAsync();
        await myVenuePage.PostDoorSplitOpportunityAsync(doorPercent);
        await myVenuePage.WaitUntilSavedAsync();

        state.OpportunityId = await FetchNewestOpportunityIdAsync(state.VenueId);
    }

    [When(@"the venue manager posts a versus opportunity for £(\d+) guarantee and (\d+)% door")]
    public async Task PostsVersusOpportunity(int guarantee, int doorPercent)
    {
        state.VenueId = fixture.App.SeedState.Venue.Id;

        myVenuePage = new MyVenuePage(browser.Page, fixture.App.VenueSpaUrl);
        await myVenuePage.GotoAsync();
        await myVenuePage.PostVersusOpportunityAsync(guarantee, doorPercent);
        await myVenuePage.WaitUntilSavedAsync();

        state.OpportunityId = await FetchNewestOpportunityIdAsync(state.VenueId);
    }

    [When(@"the venue manager accepts the application")]
    public async Task AcceptsApplication()
    {
        await browser.UseRoleAsync(Role.VenueManager);

        var applicationsPage = new ApplicationsPage(browser.Page, fixture.App.VenueSpaUrl);
        await applicationsPage.GotoAsync(state.OpportunityId);
        await applicationsPage.ClickAcceptAsync(state.ApplicationId);

        var acceptPage = new AcceptApplicationPage(browser.Page);
        await acceptPage.ClickConfirmAsync();
    }

    [Given(@"a flat fee opportunity has been applied to")]
    public async Task AFlatFeeOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.FlatFeeApp.Id;
        await browser.Page.GotoSpaAsync($"{fixture.App.VenueSpaUrl}/applications/{state.ApplicationId}/checkout");
    }

    [Given(@"a door split opportunity has been applied to")]
    public async Task ADoorSplitOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.DoorSplitApp.Id;
        await browser.Page.GotoSpaAsync($"{fixture.App.VenueSpaUrl}/applications/{state.ApplicationId}/checkout");
    }

    [Given(@"a versus opportunity has been applied to")]
    public async Task AVersusOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.VersusApp.Id;
        await browser.Page.GotoSpaAsync($"{fixture.App.VenueSpaUrl}/applications/{state.ApplicationId}/checkout");
    }

    [When(@"the venue manager pays the flat fee with a new card")]
    public Task PaysWithNewCard() =>
        new ApplicationCheckoutPage(browser.Page, payment).SubmitWithNewCardAsync(StripeCards.Success);

    [When(@"the venue manager pays the flat fee with a declined card")]
    public Task PaysFlatFeeWithDeclinedCard() =>
        payment.PayWithNewCardAsync(StripeCards.Decline);

    [When(@"the venue manager pays the flat fee with a 3DS card")]
    public async Task PaysFlatFeeWith3dsCard()
    {
        await payment.PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.CompleteChallengeAsync();
    }

    [When(@"the venue manager pays the flat fee with a 3DS-failing card")]
    public async Task PaysFlatFeeWith3dsFailingCard()
    {
        await payment.PayWithNewCardAsync(StripeCards.Insufficient3ds);
        await payment.CompleteChallengeAsync();
    }

    [When(@"the venue manager registers a card with a new card")]
    public Task RegistersCardWithNewCard() =>
        new ApplicationCheckoutPage(browser.Page, payment).SubmitWithNewCardAsync(StripeCards.Success);

    [When(@"the venue manager registers a card with a declined card")]
    public Task RegistersCardWithDeclinedCard() =>
        payment.PayWithNewCardAsync(StripeCards.Decline);

    [When(@"the venue manager registers a card with a 3DS card")]
    public async Task RegistersCardWith3dsCard()
    {
        await payment.PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.CompleteChallengeAsync();
    }

    [When(@"the venue manager registers a card with a 3DS-failing card")]
    public async Task RegistersCardWith3dsFailingCard()
    {
        await payment.PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.FailChallengeAsync();
    }

    [Then(@"a draft concert is created")]
    public Task DraftConcertCreated() =>
        browser.Page.WaitForURLAsync("**/my/concerts/concert/**", new() { Timeout = 60_000 });

    private Task<int> FetchNewestOpportunityIdAsync(int venueId) =>
        fixture.App.DbFixture.Opportunity.GetNewestAsync(venueId);
}
