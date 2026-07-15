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
    private MyConcertPage myConcertPage = null!;
    private string contractPdfText = null!;

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
        await acceptPage.AgreeAndConfirmAsync();
    }

    [Given(@"a flat fee opportunity has been applied to")]
    public async Task AFlatFeeOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.FlatFeeApp.Id;
        await GotoCheckoutAndAgreeAsync();
    }

    [Given(@"a door split opportunity has been applied to")]
    public async Task ADoorSplitOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.DoorSplitApp.Id;
        await GotoCheckoutAndAgreeAsync();
    }

    [Given(@"a versus opportunity has been applied to")]
    public async Task AVersusOpportunityHasBeenAppliedTo()
    {
        state.ApplicationId = fixture.App.SeedState.VersusApp.Id;
        await GotoCheckoutAndAgreeAsync();
    }

    private async Task GotoCheckoutAndAgreeAsync()
    {
        await browser.Page.GotoSpaAsync($"{fixture.App.VenueSpaUrl}/applications/{state.ApplicationId}/checkout");
        await browser.Page.GetByTestId("e-sign").FillAsync("Vera Venue");
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

    [When(@"a draft concert is created")]
    [Then(@"a draft concert is created")]
    public Task DraftConcertCreated() =>
        browser.Page.WaitForURLAsync("**/my/concerts/concert/**", new() { Timeout = 60_000 });

    [When(@"the venue manager downloads the booking contract")]
    [Then(@"the venue manager downloads the booking contract")]
    public async Task DownloadsBookingContract() =>
        contractPdfText = await new MyConcertPage(browser.Page).DownloadContractAsync();

    [Then(@"the contract PDF is signed by ""(.+)"" and ""(.+)""")]
    public void ContractPdfIsSignedBy(string partyA, string partyB)
    {
        Assert.Contains("Signatures", contractPdfText);
        Assert.Contains($"Signed by {partyA}", contractPdfText);
        Assert.Contains($"Signed by {partyB}", contractPdfText);
    }

    [When(@"the venue manager cancels the booking")]
    public Task CancelsBooking() =>
        new MyConcertPage(browser.Page).CancelBookingAsync();

    [Then(@"the booking is cancelled and the payment refunded")]
    public Task BookingCancelledAndRefunded() =>
        new MyConcertPage(browser.Page).WaitUntilCancelledAsync();

    [Given(@"an ended door split concert with (\d+) tickets sold through Concertable")]
    public Task AnEndedDoorSplitConcertWithConcertableSales(int ticketsSold)
    {
        var concert = fixture.App.SeedState.PastDoorSplitBooking.Concert!;
        Assert.Equal(ticketsSold, concert.TicketsSold); // ties the scenario's figure to the seed
        state.ConcertId = concert.Id;
        return Task.CompletedTask;
    }

    [When(@"the venue manager enters £(\d+) of external door takings")]
    public async Task EntersExternalDoorTakings(decimal externalTake)
    {
        await browser.UseRoleAsync(Role.VenueManager);
        myConcertPage = new MyConcertPage(browser.Page, fixture.App.VenueSpaUrl);
        await myConcertPage.GotoAsync(state.ConcertId!.Value);
        await myConcertPage.EnterDoorTakingsAsync(externalTake);
    }

    [Then(@"the takings breakdown shows £([\d.]+) from Concertable and £([\d.]+) in total")]
    public Task TakingsBreakdownShows(decimal concertable, decimal total) =>
        myConcertPage.ExpectBreakdownAsync(concertable, total);

    [When(@"the venue manager confirms the door takings")]
    public Task ConfirmsDoorTakings() => myConcertPage.ConfirmDoorTakingsAsync();

    [Then(@"the door takings are recorded")]
    public Task DoorTakingsRecorded() => myConcertPage.WaitUntilDoorTakingsRecordedAsync();

    private Task<int> FetchNewestOpportunityIdAsync(int venueId) =>
        fixture.App.DbFixture.Opportunity.GetNewestAsync(venueId);
}
