using Safar.Domain.Common;
using Safar.Domain.Enums;
using Safar.Domain.Rules;

namespace Safar.Domain.Entities;

public class Booking
{
    public const int MaxSeatsPerBooking = 10;

    /// <summary>Seats are held for this long while the customer pays.</summary>
    public static readonly TimeSpan PaymentHold = TimeSpan.FromMinutes(15);

    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Customer-facing reference, e.g. SFR-7K3Q9X.</summary>
    public string Reference { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int DepartureId { get; set; }
    public Departure Departure { get; set; } = null!;

    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;

    public int Seats { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? PromoCode { get; set; }
    public decimal RefundAmount { get; set; }

    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }

    /// <summary>Stored as a JSONB column.</summary>
    public List<Traveller> Travellers { get; set; } = [];

    public List<Payment> Payments { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Creates a booking, reserves seats and prices it. The booking waits for payment.</summary>
    public static Booking Create(
        Guid userId,
        Departure departure,
        IReadOnlyList<Traveller> travellers,
        PromoCode? promo,
        string contactEmail,
        string contactPhone,
        string? specialRequests,
        DateTime utcNow)
    {
        if (travellers.Count is < 1 or > MaxSeatsPerBooking)
            throw new DomainException($"A booking must have between 1 and {MaxSeatsPerBooking} travellers.");

        var today = DateOnly.FromDateTime(utcNow);
        departure.ReserveSeats(travellers.Count, today);

        var price = PriceCalculator.Calculate(departure.PricePerPerson, travellers.Count, promo, utcNow);
        promo?.Redeem(utcNow);

        return new Booking
        {
            Reference = BookingReference.New(),
            UserId = userId,
            Departure = departure,
            DepartureId = departure.Id,
            Seats = travellers.Count,
            UnitPrice = price.UnitPrice,
            Subtotal = price.Subtotal,
            Discount = price.Discount,
            Total = price.Total,
            PromoCode = promo?.Code,
            Travellers = travellers.ToList(),
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            SpecialRequests = specialRequests,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(PaymentHold)
        };
    }

    public void Confirm(Payment payment, DateTime utcNow)
    {
        if (Status != BookingStatus.PendingPayment)
            throw new DomainException($"Booking is {Status} and cannot be paid.");
        if (utcNow > ExpiresAt)
            throw new DomainException("The payment window has expired. Please book again.");

        Payments.Add(payment);
        Status = BookingStatus.Confirmed;
        ConfirmedAt = utcNow;
    }

    /// <summary>Cancels the booking, releases seats and returns the refund amount.</summary>
    public decimal Cancel(string reason, DateTime utcNow, bool fullRefund = false)
    {
        if (Status is not (BookingStatus.PendingPayment or BookingStatus.Confirmed))
            throw new DomainException($"Booking is already {Status}.");

        var today = DateOnly.FromDateTime(utcNow);
        if (!fullRefund && Departure.StartDate <= today)
            throw new DomainException("A trip that has already started cannot be cancelled online.");

        RefundAmount = Status == BookingStatus.Confirmed
            ? fullRefund ? Total : CancellationPolicy.RefundFor(Total, Departure.StartDate, today).Refund
            : 0;

        if (RefundAmount > 0)
        {
            Payments.Add(new Payment
            {
                Amount = -RefundAmount,
                Status = PaymentStatus.Refunded,
                TransactionId = $"RFND-{Reference}",
                CreatedAt = utcNow
            });
        }

        Departure.ReleaseSeats(Seats);
        Status = BookingStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason;
        return RefundAmount;
    }

    /// <summary>Payment hold timed out - give the seats back.</summary>
    public void Expire(DateTime utcNow)
    {
        if (Status != BookingStatus.PendingPayment || utcNow <= ExpiresAt) return;
        Departure.ReleaseSeats(Seats);
        Status = BookingStatus.Expired;
    }
}

public class Traveller
{
    public string FullName { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}

public class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BookingId { get; set; }

    /// <summary>Positive for charges, negative for refunds.</summary>
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Method { get; set; } = "Card";
    public DateTime CreatedAt { get; set; }
}
