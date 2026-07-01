namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class AcceptApplicationPage
{
    private readonly IPage page;

    public AcceptApplicationPage(IPage page) => this.page = page;

    private ILocator ConfirmButton => page.GetByTestId("confirm");

    public Task ClickConfirmAsync() => ConfirmButton.ClickAsync();
}
