using Concertable.B2B.E2ETests.Ui.Support;

namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class AcceptApplicationPage
{
    private readonly IPage page;

    public AcceptApplicationPage(IPage page) => this.page = page;

    private ILocator ConfirmButton => page.GetByTestId("confirm");
    private ILocator SignatureName => page.GetByTestId("e-sign");

    public Task ClickConfirmAsync() => ConfirmButton.ClickAsync();

    public async Task AgreeAndConfirmAsync()
    {
        await SignatureName.FillAsync("Vera Venue");
        await ConfirmButton.ClickAsync();
    }
}
