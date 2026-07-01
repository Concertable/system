namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class BusinessGatewayPage
{
    private readonly IPage page;
    private readonly string url;

    public BusinessGatewayPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = spaBaseUrl;
    }

    private ILocator GetStartedVenue => page.GetByTestId("get-started-venue");
    private ILocator GetStartedArtist => page.GetByTestId("get-started-artist");

    public Task GotoAsync() => page.GotoAsync(url, new() { WaitUntil = WaitUntilState.Load });

    public Task ClickGetStartedVenueAsync() => GetStartedVenue.ClickAsync();
    public Task ClickGetStartedArtistAsync() => GetStartedArtist.ClickAsync();
}
