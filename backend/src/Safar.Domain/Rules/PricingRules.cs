using System.Security.Cryptography;
using Safar.Domain.Common;
using Safar.Domain.Entities;

namespace Safar.Domain.Rules;

public record PriceBreakdown(decimal UnitPrice, int Seats, decimal Subtotal, decimal Discount, decimal Total, string? PromoCode);

public static class PriceCalculator
{
    public static PriceBreakdown Calculate(decimal unitPrice, int seats, PromoCode? promo, DateTime utcNow)
    {
        var subtotal = unitPrice * seats;
        var discount = 0m;

        if (promo is not null)
        {
            var problem = promo.WhyInvalid(utcNow);
            if (problem is not null) throw new DomainException(problem);

            discount = Math.Round(subtotal * promo.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
            if (promo.MaxDiscount.HasValue) discount = Math.Min(discount, promo.MaxDiscount.Value);
        }

        return new PriceBreakdown(unitPrice, seats, subtotal, discount, subtotal - discount, promo?.Code);
    }
}

public record RefundQuote(decimal RefundPercent, decimal Refund, decimal Fee, int DaysBeforeDeparture);

/// <summary>
/// Standard tour-operator cancellation policy:
///   30+ days before departure → 90 % refund
///   15-29 days               → 50 %
///   7-14 days                → 25 %
///   under 7 days             → no refund
/// </summary>
public static class CancellationPolicy
{
    public static readonly (int MinDays, decimal Percent)[] Tiers =
    [
        (30, 90m),
        (15, 50m),
        (7, 25m),
        (0, 0m)
    ];

    public static RefundQuote RefundFor(decimal amountPaid, DateOnly departure, DateOnly today)
    {
        var days = departure.DayNumber - today.DayNumber;
        var percent = Tiers.First(t => days >= t.MinDays || t.MinDays == 0).Percent;
        var refund = Math.Round(amountPaid * percent / 100m, 2, MidpointRounding.AwayFromZero);
        return new RefundQuote(percent, refund, amountPaid - refund, days);
    }
}

public static class BookingReference
{
    // No 0/O/1/I to avoid confusion when read over the phone
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New() => "SFR-" + RandomNumberGenerator.GetString(Alphabet, 6);
}
