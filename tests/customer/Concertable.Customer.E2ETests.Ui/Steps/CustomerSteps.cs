using Concertable.Customer.E2ETests.Ui.PageObjects;
using Concertable.Customer.E2ETests.Ui.Support;

namespace Concertable.Customer.E2ETests.Ui.Steps;

[Binding]
public sealed class CustomerSteps
{
    private readonly UiFixture fixture;
    private readonly Browser browser;
    private readonly IStripePayment payment;
    private FindPage findPage = null!;
    private ConcertDetailsPage concertDetailsPage = null!;
    private TicketCheckoutPage checkoutPage = null!;
    private CustomerUpcomingTicketsPage upcomingTicketsPage = null!;

    public CustomerSteps(
        UiFixture fixture,
        Browser browser,
        IStripePayment payment)
    {
        this.fixture = fixture;
        this.browser = browser;
        this.payment = payment;
    }

    [Given(@"the customer is on a concert detail page")]
    public async Task GivenOnConcertDetailPage()
    {
        var concertId = fixture.App.SeedState.UpcomingFlatFeeConcert.Id;
        await browser.Page.GotoSpaAsync($"{fixture.App.CustomerSpaUrl}/find/concert/{concertId}");
        concertDetailsPage = new ConcertDetailsPage(browser.Page);
        await concertDetailsPage.WaitUntilLoadedAsync();
    }

    [When(@"the customer opens the filter panel on the find page")]
    public async Task OpensFilterPanelOnFindPage()
    {
        findPage = new FindPage(browser.Page, fixture.App.CustomerSpaUrl);
        await findPage.GotoAsync();
        await findPage.OpenFilterPanelAsync();
    }

    [When(@"the customer selects the header type ""(.*)""")]
    public Task SelectsHeaderType(string type) =>
        findPage.SelectHeaderTypeAsync(type);

    [When(@"the customer adds the genre filter ""(.*)""")]
    public Task AddsGenreFilter(string genre) =>
        findPage.AddGenreFilterAsync(genre);

    [When(@"the customer sets the radius to (\d+) km")]
    public Task SetsRadius(int km) =>
        findPage.SetRadiusAsync(km);

    [When(@"the customer applies the filters")]
    public Task AppliesFilters() =>
        findPage.ApplyFiltersAsync();

    [Then(@"concert results should be visible")]
    public Task ConcertResultsVisible() =>
        findPage.WaitForResultsAsync();

    [Then(@"the find URL should contain ""(.*)""")]
    public Task FindUrlContains(string substring) =>
        findPage.AssertUrlContainsAsync(substring);

    [Then(@"at least (\d+) concert result cards should be visible")]
    public Task AtLeastNConcertResultCardsVisible(int n) =>
        findPage.AssertMinimumResultCardsAsync(n);

    [When(@"the customer clicks the first concert result")]
    public Task ClicksFirstResult() =>
        findPage.ClickFirstResultAsync();

    [Then(@"the customer should be on the concert detail page")]
    public async Task OnConcertDetailPage()
    {
        await browser.Page.WaitForURLAsync("**/find/concert/**");
        concertDetailsPage = new ConcertDetailsPage(browser.Page);
        await concertDetailsPage.WaitUntilLoadedAsync();
    }

    [When(@"the customer clicks buy tickets")]
    public Task ClicksBuyTickets() =>
        concertDetailsPage.ClickBuyTicketsAsync();

    [Then(@"the customer should be on the checkout page")]
    public async Task OnCheckoutPage()
    {
        await browser.Page.WaitForURLAsync("**/concert/checkout/**");
        checkoutPage = new TicketCheckoutPage(browser.Page, payment);
    }

    [When(@"the customer pays with a test card and confirms")]
    public Task PaysWithTestCard() =>
        checkoutPage.PayWithTestCardAsync();

    [When(@"the customer pays with a new card and confirms")]
    public Task PaysWithNewCard() =>
        checkoutPage.PayWithNewCardAsync(StripeCards.Success);

    [When(@"the customer pays with a declined card")]
    public Task PaysWithDeclinedCard() =>
        checkoutPage.PayWithNewCardAsync(StripeCards.Decline);

    [When(@"the customer pays with a 3DS card")]
    public async Task PaysWith3dsCard()
    {
        await checkoutPage.PayWithNewCardAsync(StripeCards.Requires3ds);
        await payment.CompleteChallengeAsync();
    }

    [When(@"the customer pays with a 3DS-failing card")]
    public async Task PaysWith3dsFailingCard()
    {
        await checkoutPage.PayWithNewCardAsync(StripeCards.Insufficient3ds);
        await payment.CompleteChallengeAsync();
    }

    [Then(@"the checkout success screen should be visible")]
    public Task CheckoutSuccessVisible() =>
        checkoutPage.WaitForSuccessScreenAsync();

    [Then(@"a ticket purchased toast should appear")]
    public Task TicketPurchasedToastAppears() =>
        checkoutPage.WaitForTicketPurchasedToastAsync();

    [When(@"the customer clicks view tickets")]
    public Task ClicksViewTickets() =>
        checkoutPage.ClickViewTicketsAsync();

    [Then(@"the customer should be on the upcoming tickets page")]
    public async Task OnUpcomingTicketsPage()
    {
        await browser.Page.WaitForURLAsync("**/profile/tickets/upcoming");
        upcomingTicketsPage = new CustomerUpcomingTicketsPage(browser.Page);
        await upcomingTicketsPage.WaitForTicketListAsync();
    }

    [Then(@"a ticket card should be listed")]
    public Task TicketCardListed() =>
        upcomingTicketsPage.WaitForTicketCardsAsync();

    [When(@"the customer opens the QR code")]
    public Task OpensQrCode() =>
        upcomingTicketsPage.OpenQrCodeAsync();

    [Then(@"the QR dialog should be visible")]
    public Task QrDialogVisible() =>
        upcomingTicketsPage.WaitForQrDialogAsync();

    [Then(@"the QR image should be present")]
    public Task QrImagePresent() =>
        upcomingTicketsPage.WaitForQrImageAsync();
}
