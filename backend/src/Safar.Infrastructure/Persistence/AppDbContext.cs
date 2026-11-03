using Microsoft.EntityFrameworkCore;
using Safar.Application.Common;
using Safar.Domain.Entities;

namespace Safar.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TourPackage> TourPackages => Set<TourPackage>();
    public DbSet<Departure> Departures => Set<Departure>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public void ResetTracking() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Guid keys are created in C# (Guid.CreateVersion7), not by the database. Telling EF this means
        // a new Payment added to booking.Payments is INSERTed rather than treated as an existing row.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var key = entity.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                key.Properties[0].ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is always numeric(12,2); enums are stored as readable text
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }
}
