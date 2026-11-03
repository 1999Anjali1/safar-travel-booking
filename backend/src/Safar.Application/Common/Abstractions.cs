using Microsoft.EntityFrameworkCore;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Application.Common;

// Interfaces the Application layer needs. Implementations live in Infrastructure / Api,
// so business logic never depends on PostgreSQL, JWT libraries or SignalR directly.

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TourPackage> TourPackages { get; }
    DbSet<Departure> Departures { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PromoCode> PromoCodes { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Detach everything - used before retrying after a concurrency conflict.</summary>
    void ResetTracking();
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    bool IsStaff => Role is UserRole.Agent or UserRole.Admin;
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);

    /// <summary>Returns the raw token for the client and its hash for the database.</summary>
    (string Raw, string Hash) CreateRefreshToken();

    string Hash(string rawRefreshToken);

    TimeSpan RefreshTokenLifetime { get; }
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public record PaymentResult(bool Succeeded, string TransactionId, string? FailureReason);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(decimal amount, string reference, string method, CancellationToken ct = default);
}

public record BookingNotification(
    string Reference, string Event, string CustomerName, string PackageTitle, DateOnly DepartureDate, int Seats, decimal Total);

/// <summary>Pushes live events to staff dashboards (implemented with SignalR).</summary>
public interface INotificationService
{
    Task BookingEventAsync(BookingNotification notification, CancellationToken ct = default);
}
