using Microsoft.Playwright;

namespace Concertable.Testing.E2E.Ui;

public sealed class RadixSlider(IPage page, ILocator root, int min, int max)
{
    private ILocator Thumb => root.GetByRole(AriaRole.Slider);

    public async Task SetValueAsync(int value)
    {
        var box = await root.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Radix slider has no bounding box");

        var x = box.X + (value - min) / (float)(max - min) * box.Width;
        await page.Mouse.ClickAsync(x, box.Y + box.Height / 2);

        await Thumb.FocusAsync();
        var current = int.Parse(await Thumb.GetAttributeAsync("aria-valuenow") ?? "0");
        var key = current < value ? "ArrowRight" : "ArrowLeft";
        for (var i = 0; i < Math.Abs(value - current); i++)
            await page.Keyboard.PressAsync(key);

        await Assertions.Expect(Thumb).ToHaveAttributeAsync("aria-valuenow", value.ToString());
    }
}
