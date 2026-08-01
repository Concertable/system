namespace Concertable.B2B.E2ETests.Ui.Support;

// Which seeded operator a scenario logs in as; names match the @VenueManager/@ArtistManager
// scenario tags. Test-only login selector, distinct from tenant membership roles.
public enum LoginPersona
{
    VenueManager,
    ArtistManager,
}
