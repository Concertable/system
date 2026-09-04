using Concertable.B2B.Hosting;
using Concertable.Customer.Hosting;

namespace Concertable.AppHost;

public static class SystemLocalSpaSurfaces
{
    public static IReadOnlyList<SpaSurface> All { get; } =
        Array.AsReadOnly([.. CustomerLocalSpaSurfaces.All, .. B2BLocalSpaSurfaces.All]);
}
