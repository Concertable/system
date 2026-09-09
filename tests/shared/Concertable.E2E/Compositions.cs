namespace Concertable.E2E;

public static class Compositions
{
    public static IComposition Source() =>
        throw new NotSupportedException(
            "This repository composes the fleet only from the images pinned in compatibility/local.yaml; "
            + "the project-substitution path was removed with Concertable.E2E.Source. The suites still "
            + "calling this reach a service's E2E-only admin surface, which a production image does not "
            + "carry, so they need a black-box replacement (seed through the b2b-seeding-simulator "
            + "container, reset through the AppHost's own connection strings) before they can run here. "
            + "Concertable.Qualification.E2ETests is the converted, image-backed suite.");
}
