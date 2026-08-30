namespace Concertable.Testing.E2E;

public static class FleetProjectProviders
{
    public static IFleetProjectProvider Source()
    {
        const string typeName =
            "Concertable.Fleet.E2E.Source.SourceFleetProjectProvider, Concertable.Fleet.E2E.Source";
        var providerType = Type.GetType(typeName, throwOnError: true)!;
        return (IFleetProjectProvider)Activator.CreateInstance(providerType)!;
    }
}
