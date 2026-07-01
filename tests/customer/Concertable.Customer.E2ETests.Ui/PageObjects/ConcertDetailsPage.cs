namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class ConcertDetailsPage
{
    private readonly IPage page;

    public ConcertDetailsPage(IPage page) => this.page = page;

    private ILocator BuyTicketsButton => page.GetByTestId("buy-tickets");

    public Task WaitUntilLoadedAsync() =>
        BuyTicketsButton.WaitForAsync();

    public Task ClickBuyTicketsAsync() =>
        BuyTicketsButton.ClickAsync();
}
