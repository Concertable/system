using Microsoft.Playwright;

namespace Concertable.Testing.E2E.Ui;

public interface IPageAccessor
{
    IPage Page { get; }
}
