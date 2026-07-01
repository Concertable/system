using Aspire.Hosting;
using Respawn;
using Respawn.Graph;
using ConcertSchema = Concertable.Customer.Concert.Infrastructure.Schema;
using ArtistSchema = Concertable.Customer.Artist.Infrastructure.Schema;
using VenueSchema = Concertable.Customer.Venue.Infrastructure.Schema;
using UserSchema = Concertable.Customer.User.Infrastructure.Schema;
using MessagingSchema = Concertable.Messaging.Infrastructure.Schema;

namespace Concertable.Customer.E2ETests;

public sealed class DbFixture
{
    private readonly DistributedApplication app;
    private readonly RespawnableDb customer = new();
    private readonly PaymentDbFixture payment = new();

    public PaymentDb Payment => payment.Payment;

    public DbFixture(DistributedApplication app) => this.app = app;

    public async Task InitializeAsync()
    {
        await customer.InitializeAsync(app, AppHostConstants.Databases.Customer, new RespawnerOptions
        {
            TablesToIgnore = [
                "__EFMigrationsHistory",
                new Table(ConcertSchema.Name, ConcertSchema.Tables.Concerts),
                new Table(ConcertSchema.Name, ConcertSchema.Tables.ConcertGenres),
                new Table(ConcertSchema.Name, ConcertSchema.Tables.VenueReadModels),
                new Table(ConcertSchema.Name, ConcertSchema.Tables.ArtistReadModels),
                new Table(ConcertSchema.Name, ConcertSchema.Tables.ArtistReadModelGenres),
                new Table(ArtistSchema.Name, ArtistSchema.Tables.Artists),
                new Table(ArtistSchema.Name, ArtistSchema.Tables.ArtistGenres),
                new Table(VenueSchema.Name, VenueSchema.Tables.Venues),
                new Table(UserSchema.Name, UserSchema.Tables.Users),
                new Table(MessagingSchema.Name, MessagingSchema.Tables.Inbox),
            ],
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true
        });
        await payment.InitializeAsync(app);
    }

    public async Task ResetAsync()
    {
        await customer.ResetAsync();
        await payment.ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await customer.DisposeAsync();
        await payment.DisposeAsync();
    }
}
