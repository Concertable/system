namespace Concertable.System.E2E;

public static class SystemAppHostFactories
{
    public static ISystemAppHostFactory Source()
    {
        const string typeName =
            "Concertable.System.E2E.Source.SourceSystemAppHostFactory, Concertable.System.E2E.Source";
        var factoryType = Type.GetType(typeName, throwOnError: true)!;
        return (ISystemAppHostFactory)Activator.CreateInstance(factoryType)!;
    }
}
