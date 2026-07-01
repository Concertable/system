namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class LoginPage
{
    private readonly IPage page;
    private readonly string spaBaseUrl;

    public LoginPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.spaBaseUrl = spaBaseUrl;
    }

    private ILocator EmailInput => page.GetByTestId("login-email");
    private ILocator PasswordInput => page.GetByTestId("login-password");
    private ILocator SubmitButton => page.GetByTestId("login-submit");
    private ILocator SignUpLink => page.GetByTestId("signup-link");

    public Task GotoAsync() => page.GotoAsync($"{spaBaseUrl}/login");

    public async Task SignInAsync(string email, string password)
    {
        await EmailInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
        await SubmitButton.ClickAsync();
    }

    public Task ClickSignUpAsync() => SignUpLink.ClickAsync();

    public Task WaitForUrlAsync(string url, float timeoutMs = 15000) =>
        page.WaitForURLAsync(url, new() { Timeout = timeoutMs });
}
