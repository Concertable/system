using Concertable.Customer.E2ETests.Mobile.PageObjects;
using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.Steps;

[Binding]
public sealed class CustomerSteps
{
    private readonly MobileFixture fixture;
    private readonly MobileApp app;
    private readonly StripePaymentSheet paymentSheet;
    private LoginScreen loginScreen = null!;
    private HomeScreen homeScreen = null!;
    private ConcertDetailScreen concertDetailScreen = null!;
    private TicketCheckoutScreen checkoutScreen = null!;
    private CheckoutSuccessScreen successScreen = null!;
    private TicketsScreen ticketsScreen = null!;
    private TicketDetailScreen ticketDetailScreen = null!;

    public CustomerSteps(MobileFixture fixture, MobileApp app, StripePaymentSheet paymentSheet)
    {
        this.fixture = fixture;
        this.app = app;
        this.paymentSheet = paymentSheet;
    }

    [Given(@"the customer is signed in")]
    public async Task GivenSignedIn()
    {
        loginScreen = new LoginScreen(app);
        var seed = fixture.App.SeedState;
        await loginScreen.SignInAsync(seed.Customer1.Email, SeedState.TestPassword);
        homeScreen = new HomeScreen(app);
        homeScreen.WaitUntilLoaded();
    }

    [When(@"the customer opens the first concert")]
    public void OpensFirstConcert() =>
        homeScreen.OpenFirstConcert();

    [Then(@"the customer should be on the concert detail screen")]
    public void OnConcertDetailScreen()
    {
        concertDetailScreen = new ConcertDetailScreen(app);
        concertDetailScreen.WaitUntilLoaded();
    }

    [When(@"the customer taps buy tickets")]
    public void TapsBuyTickets() =>
        concertDetailScreen.ClickBuyTickets();

    [Then(@"the customer should be on the checkout screen")]
    public void OnCheckoutScreen()
    {
        checkoutScreen = new TicketCheckoutScreen(app, paymentSheet);
        checkoutScreen.WaitUntilLoaded();
    }

    [When(@"the customer pays with a test card")]
    public Task PaysWithTestCard() =>
        checkoutScreen.PayWithNewCardAsync(StripeCards.Success);

    [Then(@"the checkout success screen should be visible")]
    public void CheckoutSuccessVisible()
    {
        successScreen = new CheckoutSuccessScreen(app);
        successScreen.WaitUntilVisible();
    }

    [When(@"the customer taps view tickets")]
    public void TapsViewTickets() =>
        successScreen.ClickViewTickets();

    [Then(@"the customer should be on the tickets screen")]
    public void OnTicketsScreen()
    {
        ticketsScreen = new TicketsScreen(app);
        ticketsScreen.WaitUntilLoaded();
    }

    [Then(@"a ticket card should be listed")]
    public void TicketCardListed() =>
        ticketsScreen.WaitUntilLoaded();

    [When(@"the customer opens the first ticket")]
    public void OpensFirstTicket() =>
        ticketsScreen.OpenFirstTicket();

    [Then(@"the QR code should be visible")]
    public void QrCodeVisible()
    {
        ticketDetailScreen = new TicketDetailScreen(app);
        ticketDetailScreen.WaitForQrCode();
    }
}
