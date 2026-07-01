using OpenQA.Selenium.Appium.Android;

namespace Concertable.Customer.E2ETests.Mobile.Support;

public sealed class StripePaymentSheet
{
    private readonly MobileApp app;

    public StripePaymentSheet(MobileApp app) => this.app = app;

    private AndroidDriver Driver => app.Driver;

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        var cardField = Driver.GetByTestId("CardNumberField", TimeSpan.FromSeconds(30));
        cardField.SendKeys(cardNumber);

        Driver.GetByTestId("CardExpiryField").SendKeys("12" + (DateTime.UtcNow.Year + 3).ToString().Substring(2));
        Driver.GetByTestId("CardCvcField").SendKeys("123");

        if (Driver.TryGetByTestId("CardPostalCodeField", TimeSpan.FromSeconds(2), out var postal))
            postal!.SendKeys("12345");

        Driver.GetByTestId("PayButton").Click();
        await Task.CompletedTask;
    }

    public Task PayWithSavedCardAsync()
    {
        Driver.GetByTestId("PayButton", TimeSpan.FromSeconds(30)).Click();
        return Task.CompletedTask;
    }
}
