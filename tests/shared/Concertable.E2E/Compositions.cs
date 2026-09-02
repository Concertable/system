namespace Concertable.E2E;

public static class Compositions
{
    public static IComposition Source()
    {
        const string typeName =
            "Concertable.E2E.Source.SourceComposition, Concertable.E2E.Source";
        var factoryType = Type.GetType(typeName, throwOnError: true)!;
        return (IComposition)Activator.CreateInstance(factoryType)!;
    }
}
