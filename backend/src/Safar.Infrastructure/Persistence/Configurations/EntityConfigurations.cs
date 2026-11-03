using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Safar.Domain.Entities;

namespace Safar.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.Email).HasMaxLength(150);
        b.Property(u => u.FullName).HasMaxLength(100);
        b.Property(u => u.Phone).HasMaxLength(20);
        b.Property(u => u.PasswordHash).HasMaxLength(200);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.Property(t => t.TokenHash).HasMaxLength(64);
        b.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TourPackageConfiguration : IEntityTypeConfiguration<TourPackage>
{
    public void Configure(EntityTypeBuilder<TourPackage> b)
    {
        b.ToTable("tour_packages");
        b.HasIndex(p => p.Slug).IsUnique();
        b.HasIndex(p => new { p.IsPublished, p.Category });
        b.Property(p => p.Slug).HasMaxLength(100);
        b.Property(p => p.Title).HasMaxLength(120);
        b.Property(p => p.Summary).HasMaxLength(300);
        b.Property(p => p.Description).HasMaxLength(4000);
        b.Property(p => p.Destination).HasMaxLength(80);
        b.Property(p => p.Country).HasMaxLength(60);
        b.Property(p => p.ImageUrl).HasMaxLength(500);
        // List<string> maps to PostgreSQL text[] automatically with Npgsql
    }
}

public class DepartureConfiguration : IEntityTypeConfiguration<Departure>
{
    public void Configure(EntityTypeBuilder<Departure> b)
    {
        b.ToTable("departures", t =>
        {
            t.HasCheckConstraint("ck_departures_seats", "\"SeatsBooked\" >= 0 AND \"SeatsBooked\" <= \"Capacity\"");
        });
        b.HasIndex(d => new { d.TourPackageId, d.StartDate });
        b.HasOne(d => d.TourPackage).WithMany(p => p.Departures).HasForeignKey(d => d.TourPackageId).OnDelete(DeleteBehavior.Cascade);

        // Optimistic concurrency using PostgreSQL's built-in xmin row version
        b.Property(d => d.Version).IsRowVersion();

        b.Ignore(d => d.SeatsAvailable);
        b.Ignore(d => d.PricePerPerson);
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("bookings");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.HasIndex(x => x.UserId);
        b.Property(x => x.Reference).HasMaxLength(12);
        b.Property(x => x.PromoCode).HasMaxLength(30);
        b.Property(x => x.ContactEmail).HasMaxLength(150);
        b.Property(x => x.ContactPhone).HasMaxLength(20);
        b.Property(x => x.SpecialRequests).HasMaxLength(500);
        b.Property(x => x.CancellationReason).HasMaxLength(300);

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Departure).WithMany(d => d.Bookings).HasForeignKey(x => x.DepartureId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);

        // Travellers live inside the booking row as a JSONB document
        b.OwnsMany(x => x.Travellers, t => t.ToJson());
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasIndex(p => p.CreatedAt);
        b.Property(p => p.TransactionId).HasMaxLength(60);
        b.Property(p => p.Method).HasMaxLength(20);
    }
}

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> b)
    {
        b.ToTable("promo_codes");
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Code).HasMaxLength(30);
        b.Property(p => p.Description).HasMaxLength(150);
        b.Property<uint>("Version").IsRowVersion(); // shadow xmin - stops a limited code being over-used
    }
}
