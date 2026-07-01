using Aspire.Hosting;
using Respawn;
using Respawn.Graph;
using UserSchema = Concertable.B2B.User.Infrastructure.Schema;
using MessagingSchema = Concertable.Messaging.Infrastructure.Schema;

namespace Concertable.B2B.E2ETests;

public sealed class DbFixture
{
    private readonly DistributedApplication app;
    private readonly RespawnableDb b2b = new();
    private readonly PaymentDbFixture payment = new();

    public OpportunityDb Opportunity { get; private set; } = null!;
    public ApplicationDb Application { get; private set; } = null!;
    public BookingDb Booking { get; private set; } = null!;
    public PaymentDb Payment => payment.Payment;

    public DbFixture(DistributedApplication app) => this.app = app;

    public async Task InitializeAsync()
    {
        await b2b.InitializeAsync(app, AppHostConstants.Databases.B2B, new RespawnerOptions
        {
            TablesToIgnore =
            [
                "__EFMigrationsHistory",
                new Table(UserSchema.Name, UserSchema.Tables.Users),
                new Table(UserSchema.Name, UserSchema.Tables.ArtistManagerProfiles),
                new Table(UserSchema.Name, UserSchema.Tables.VenueManagerProfiles),
                new Table(UserSchema.Name, UserSchema.Tables.AdminProfiles),
                new Table(MessagingSchema.Name, MessagingSchema.Tables.Inbox),
            ],
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true
        });
        await payment.InitializeAsync(app);
        Opportunity = new OpportunityDb(b2b.Connection);
        Application = new ApplicationDb(b2b.Connection);
        Booking = new BookingDb(b2b.Connection);
    }

    public async Task ResetAsync()
    {
        await b2b.ResetAsync();
        await payment.ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await b2b.DisposeAsync();
        await payment.DisposeAsync();
    }

}
