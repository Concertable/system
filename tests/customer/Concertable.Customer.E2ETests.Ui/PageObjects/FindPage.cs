using System.Text.RegularExpressions;

namespace Concertable.Customer.E2ETests.Ui.PageObjects;

public sealed class FindPage
{
    private readonly IPage page;
    private readonly string url;

    public FindPage(IPage page, string spaBaseUrl)
    {
        this.page = page;
        this.url = $"{spaBaseUrl}/find";
    }

    private ILocator FilterOpenButton => page.GetByTestId("filter-open");
    private ILocator FilterPanel => page.GetByTestId("filter-panel");
    private ILocator HeaderTypeSelect => page.GetByTestId("filter-header-type");
    private ILocator GenreSelect => page.GetByTestId("filter-genre-select");
    private ILocator GenreAddButton => page.GetByTestId("filter-genre-add");
    private RadixSlider RadiusSlider => new(page, page.GetByTestId("filter-radius-slider"), min: 1, max: 200);
    private ILocator ApplyButton => page.GetByTestId("filter-apply");
    private ILocator SearchResults => page.GetByTestId("search-results");
    private ILocator HeaderCards => page.GetByTestId("header-card");
    private ILocator QueryInput => page.GetByTestId("search-query");

    public Task GotoAsync() => page.GotoSpaAsync(url);

    public async Task OpenFilterPanelAsync()
    {
        await FilterOpenButton.ClickAsync();
        await FilterPanel.WaitForAsync();
    }

    public async Task SelectHeaderTypeAsync(string type)
    {
        await HeaderTypeSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = type }).ClickAsync();
    }

    public async Task ApplyFiltersAsync()
    {
        await ApplyButton.ClickAsync();
        await FilterPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task AddGenreFilterAsync(string genre)
    {
        await GenreSelect.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = genre }).ClickAsync();
        await GenreAddButton.ClickAsync();
    }

    public Task SetRadiusAsync(int km) => RadiusSlider.SetValueAsync(km);

    public Task AssertUrlContainsAsync(string substring) =>
        Assertions.Expect(page).ToHaveURLAsync(new Regex(Regex.Escape(substring)));

    public Task AssertMinimumResultCardsAsync(int minimum) =>
        Assertions.Expect(HeaderCards.Nth(minimum - 1)).ToBeVisibleAsync();

    public Task WaitForResultsAsync() =>
        Assertions.Expect(SearchResults).ToBeVisibleAsync();

    public Task ClickFirstResultAsync() =>
        HeaderCards.First.ClickAsync();

    public Task TypeQueryAsync(string query) =>
        QueryInput.FillAsync(query);
}
