using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class AcceptApplicationPage
{
    private readonly IPage page;

    public AcceptApplicationPage(IPage page) => this.page = page;

    private ILocator ConfirmButton => page.GetByTestId("confirm");
    private ILocator AgreeToTerms => page.GetByTestId("agree-to-terms");

    public Task ClickConfirmAsync() => ConfirmButton.ClickAsync();

    public async Task AgreeAndConfirmAsync()
    {
        await AgreeToTerms.EnsureCheckedAsync();
        await ConfirmButton.ClickAsync();
    }
}
