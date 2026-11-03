using Safar.Domain.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;
using Safar.Domain.Rules;

namespace Safar.UnitTests;

public class DepartureTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static Departure NewDeparture(int capacity = 10, int booked = 0, int daysAhead = 20) => new()
    {
        Id = 1,
        Capacity = capacity,
        SeatsBooked = booked,
        StartDate = Today.AddDays(daysAhead),
        TourPackage = new TourPackage { BasePrice = 500, DurationDays = 3 }
    };

    [Fact]
    public void Reserving_seats_reduces_availability()
    {
        var d = NewDeparture(capacity: 10, booked: 4);

        d.ReserveSeats(3, Today);

        Assert.Equal(7, d.SeatsBooked);
        Assert.Equal(3, d.SeatsAvailable);
    }

    [Fact]
    public void Cannot_book_more_seats_than_available()
    {
        var d = NewDeparture(capacity: 10, booked: 8);

        var ex = Assert.Throws<DomainException>(() => d.ReserveSeats(3, Today));
        Assert.Contains("Only 2 seat(s) left", ex.Message);
    }

    [Fact]
    public void Sold_out_departure_says_so()
    {
        var d = NewDeparture(capacity: 10, booked: 10);

        var ex = Assert.Throws<DomainException>(() => d.ReserveSeats(1, Today));
        Assert.Contains("sold out", ex.Message);
    }

    [Fact]
    public void Booking_closes_before_departure()
    {
        var d = NewDeparture(daysAhead: Departure.BookingCutOffDays - 1);

        Assert.Throws<DomainException>(() => d.ReserveSeats(1, Today));
        Assert.False(d.IsBookable(Today));
    }

    [Fact]
    public void Cancelled_departure_cannot_be_booked()
    {
        var d = NewDeparture();
        d.Status = DepartureStatus.Cancelled;

        Assert.Throws<DomainException>(() => d.ReserveSeats(1, Today));
    }

    [Fact]
    public void Seasonal_price_override_wins_over_base_price()
    {
        var d = NewDeparture();
        d.PriceOverride = 650;

        Assert.Equal(650, d.PricePerPerson);
    }
}

public class PricingTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Price_without_promo_is_unit_price_times_seats()
    {
        var price = PriceCalculator.Calculate(450, 3, null, Now);

        Assert.Equal(1350, price.Subtotal);
        Assert.Equal(0, price.Discount);
        Assert.Equal(1350, price.Total);
    }

    [Fact]
    public void Percentage_promo_is_applied()
    {
        var promo = new PromoCode { Code = "TEN", DiscountPercent = 10, ValidUntil = Now.AddDays(1) };

        var price = PriceCalculator.Calculate(500, 2, promo, Now);

        Assert.Equal(100, price.Discount);
        Assert.Equal(900, price.Total);
    }

    [Fact]
    public void Promo_discount_is_capped()
    {
        var promo = new PromoCode { Code = "CAP", DiscountPercent = 20, MaxDiscount = 250, ValidUntil = Now.AddDays(1) };

        var price = PriceCalculator.Calculate(5000, 2, promo, Now); // 20% would be 2000

        Assert.Equal(250, price.Discount);
        Assert.Equal(9750, price.Total);
    }

    [Fact]
    public void Expired_promo_is_rejected()
    {
        var promo = new PromoCode { Code = "OLD", DiscountPercent = 10, ValidUntil = Now.AddDays(-1) };

        var ex = Assert.Throws<DomainException>(() => PriceCalculator.Calculate(500, 1, promo, Now));
        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public void Promo_usage_limit_is_enforced()
    {
        var promo = new PromoCode { Code = "LIMIT", DiscountPercent = 10, ValidUntil = Now.AddDays(1), MaxUses = 1 };

        promo.Redeem(Now);

        Assert.Throws<DomainException>(() => promo.Redeem(Now));
    }
}

public class CancellationPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Theory]
    [InlineData(45, 90)]
    [InlineData(30, 90)]
    [InlineData(29, 50)]
    [InlineData(15, 50)]
    [InlineData(14, 25)]
    [InlineData(7, 25)]
    [InlineData(6, 0)]
    [InlineData(0, 0)]
    public void Refund_percentage_depends_on_days_before_departure(int daysBefore, decimal expectedPercent)
    {
        var quote = CancellationPolicy.RefundFor(1000, Today.AddDays(daysBefore), Today);

        Assert.Equal(expectedPercent, quote.RefundPercent);
        Assert.Equal(1000 * expectedPercent / 100, quote.Refund);
        Assert.Equal(1000 - quote.Refund, quote.Fee);
    }
}

public class BookingTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static Departure NewDeparture(int daysAhead = 40) => new()
    {
        Id = 7,
        Capacity = 10,
        StartDate = DateOnly.FromDateTime(Now).AddDays(daysAhead),
        TourPackage = new TourPackage { BasePrice = 1000, DurationDays = 4, Title = "Test trip" }
    };

    private static List<Traveller> Travellers(int n) =>
        Enumerable.Range(1, n).Select(i => new Traveller { FullName = $"Traveller {i}", Nationality = "Indian" }).ToList();

    private static Booking NewBooking(Departure d, int seats = 2) =>
        Booking.Create(Guid.NewGuid(), d, Travellers(seats), null, "a@b.com", "+97455555555", null, Now);

    private static Payment Paid(decimal amount) =>
        new() { Amount = amount, Status = PaymentStatus.Succeeded, TransactionId = "TXN-1", CreatedAt = Now };

    [Fact]
    public void New_booking_holds_seats_and_waits_for_payment()
    {
        var d = NewDeparture();

        var booking = NewBooking(d, seats: 3);

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(3, d.SeatsBooked);
        Assert.Equal(3000, booking.Total);
        Assert.Equal(Now + Booking.PaymentHold, booking.ExpiresAt);
        Assert.Matches("^SFR-[A-Z2-9]{6}$", booking.Reference);
    }

    [Fact]
    public void Paying_confirms_the_booking()
    {
        var booking = NewBooking(NewDeparture());

        booking.Confirm(Paid(booking.Total), Now.AddMinutes(5));

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Single(booking.Payments);
    }

    [Fact]
    public void Payment_after_hold_expires_is_rejected()
    {
        var booking = NewBooking(NewDeparture());

        Assert.Throws<DomainException>(() => booking.Confirm(Paid(booking.Total), Now.AddMinutes(20)));
    }

    [Fact]
    public void Expiring_an_unpaid_booking_releases_seats()
    {
        var d = NewDeparture();
        var booking = NewBooking(d, seats: 4);

        booking.Expire(Now.AddMinutes(16));

        Assert.Equal(BookingStatus.Expired, booking.Status);
        Assert.Equal(0, d.SeatsBooked);
    }

    [Fact]
    public void Expire_does_nothing_inside_the_hold_window()
    {
        var booking = NewBooking(NewDeparture());

        booking.Expire(Now.AddMinutes(10));

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
    }

    [Fact]
    public void Cancelling_a_paid_booking_refunds_by_policy_and_releases_seats()
    {
        var d = NewDeparture(daysAhead: 20); // 15-29 days → 50 %
        var booking = NewBooking(d, seats: 2);
        booking.Confirm(Paid(booking.Total), Now);

        var refund = booking.Cancel("Change of plans", Now);

        Assert.Equal(1000, refund); // 50 % of 2000
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(0, d.SeatsBooked);
        Assert.Contains(booking.Payments, p => p.Status == PaymentStatus.Refunded && p.Amount == -1000);
    }

    [Fact]
    public void Operator_cancellation_gives_full_refund()
    {
        var booking = NewBooking(NewDeparture(daysAhead: 3));
        booking.Confirm(Paid(booking.Total), Now);

        var refund = booking.Cancel("Weather", Now, fullRefund: true);

        Assert.Equal(booking.Total, refund);
    }

    [Fact]
    public void Cannot_cancel_twice()
    {
        var booking = NewBooking(NewDeparture());
        booking.Cancel("first", Now);

        Assert.Throws<DomainException>(() => booking.Cancel("second", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Traveller_count_must_be_between_1_and_10(int count)
    {
        Assert.Throws<DomainException>(() =>
            Booking.Create(Guid.NewGuid(), NewDeparture(), Travellers(count), null, "a@b.com", "+97455555555", null, Now));
    }
}
