namespace Concertable.B2B.E2ETests.Ui.PageObjects;

public sealed class CreateArtistPage
{
    private readonly IPage page;
    private readonly string url;

    public CreateArtistPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = $"{spaBaseUrl}/create";
    }

    private ILocator NameInput => page.GetByTestId("hero-name");
    private ILocator AboutInput => page.GetByTestId("about");
    private ILocator BannerInput => page.GetByTestId("hero-banner");
    private ILocator AvatarInput => page.GetByTestId("hero-avatar");
    private ILocator SubmitButton => page.GetByTestId("submit");

    public Task WaitForLoadAsync() =>
        page.WaitForURLAsync(url, new() { Timeout = 15_000 });

    public async Task FillAsync(string name, string about, string bannerPath, string avatarPath)
    {
        await NameInput.FillAsync(name);
        await AboutInput.FillAsync(about);
        await BannerInput.SetInputFilesAsync(bannerPath);
        await AvatarInput.SetInputFilesAsync(avatarPath);
    }

    public Task SubmitAsync() => SubmitButton.ClickAsync();
}
