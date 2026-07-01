using Concertable.Customer.E2ETests.Mobile.Support;

namespace Concertable.Customer.E2ETests.Mobile.PageObjects;

public sealed class HomeScreen
{
    private readonly MobileApp app;

    public HomeScreen(MobileApp app) => this.app = app;

    private AppiumElement FirstConcertCard => app.Driver.GetByTestId("concert-card", TimeSpan.FromSeconds(45));

    public void WaitUntilLoaded() => _ = FirstConcertCard;

    public void OpenFirstConcert() => FirstConcertCard.Click();
}
