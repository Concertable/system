namespace Concertable.B2B.E2ETests.Ui.Support;

public static class LocatorExtensions
{
    /// Ticks a Radix checkbox (role=checkbox button) only if not already checked, so it is
    /// safe to call from overlapping entry points into the same page.
    public static async Task EnsureCheckedAsync(this ILocator checkbox)
    {
        await checkbox.WaitForAsync();
        if (await checkbox.GetAttributeAsync("aria-checked") != "true")
            await checkbox.ClickAsync();
    }
}
