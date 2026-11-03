namespace Safar.Domain.Enums;

public enum UserRole
{
    Customer = 1,
    Agent = 2,
    Admin = 3
}

public enum PackageCategory
{
    City = 1,
    Desert = 2,
    Beach = 3,
    Adventure = 4,
    Cultural = 5,
    Honeymoon = 6
}

public enum DepartureStatus
{
    Scheduled = 1,
    Cancelled = 2
}

/// <summary>
/// PendingPayment → Confirmed → (Cancelled)
/// PendingPayment → Expired   (payment hold timed out, seats released)
/// PendingPayment → Cancelled (customer abandoned)
/// </summary>
public enum BookingStatus
{
    PendingPayment = 1,
    Confirmed = 2,
    Cancelled = 3,
    Expired = 4
}

public enum PaymentStatus
{
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}
