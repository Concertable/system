using Concertable.Customer.E2ETests.Mobile.Support;
using OpenQA.Selenium.Support.UI;

namespace Concertable.Customer.E2ETests.Mobile.PageObjects;

public sealed class LoginScreen
{
    private readonly MobileApp app;

    public LoginScreen(MobileApp app) => this.app = app;

    private const string NativeContext = "NATIVE_APP";

    private AppiumElement SignInButton => app.Driver.GetByTestId("login-submit", TimeSpan.FromSeconds(30));

    public async Task SignInAsync(string email, string password)
    {
        SignInButton.Click();
        await SwitchToWebViewAsync();

        try
        {
            var emailInput = app.Driver.FindElement(By.CssSelector("[data-testid='login-email']"));
            emailInput.SendKeys(email);
            app.Driver.FindElement(By.CssSelector("[data-testid='login-password']")).SendKeys(password);
            app.Driver.FindElement(By.CssSelector("[data-testid='login-submit']")).Click();
        }
        finally
        {
            app.Driver.Context = NativeContext;
        }
    }

    private async Task SwitchToWebViewAsync()
    {
        var wait = new DefaultWait<AndroidDriver>(app.Driver)
        {
            Timeout = TimeSpan.FromSeconds(30),
            PollingInterval = TimeSpan.FromMilliseconds(500),
        };
        var webViewContext = wait.Until(d => d.Contexts.FirstOrDefault(c => c.StartsWith("WEBVIEW")));
        if (webViewContext is null)
            throw new InvalidOperationException("No WEBVIEW context available after tapping sign-in.");
        app.Driver.Context = webViewContext;
        await Task.CompletedTask;
    }
}
