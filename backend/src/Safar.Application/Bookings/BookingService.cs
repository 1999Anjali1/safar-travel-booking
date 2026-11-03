using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Safar.Application.Common;
using Safar.Domain.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;
using Safar.Domain.Rules;

namespace Safar.Application.Bookings;

public interface IBookingService
{
    Task<QuoteDto> QuoteAsync(QuoteRequest request, CancellationToken ct = default);
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default);
    Task<BookingDto> PayAsync(string reference, PayRequest request, CancellationToken ct = default);
    Task<BookingDto> GetAsync(string reference, CancellationToken ct = default);
    Task<PagedResult<BookingListItemDto>> GetMineAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CancellationQuoteDto> QuoteCancellationAsync(string reference, CancellationToken ct = default);
    Task<BookingDto> CancelAsync(string reference, CancelBookingRequest request, CancellationToken ct = default);
}

public class BookingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPaymentGateway paymentGateway,
    INotificationService notifications,
    TimeProvider clock,
    IValidator<QuoteRequest> quoteValidator,
    IValidator<CreateBookingRequest> createValidator,
    IValidator<PayRequest> payValidator) : IBookingService
{
    private const int MaxConcurrencyRetries = 3;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException("Please sign in to book.");

    public async Task<QuoteDto> QuoteAsync(QuoteRequest request, CancellationToken ct = default)
    {
        await quoteValidator.ValidateAndThrowAsync(request, ct);

        var departure = await db.Departures.AsNoTracking().Include(d => d.TourPackage)
                            .FirstOrDefaultAsync(d => d.Id == request.DepartureId && d.TourPackage.IsPublished, ct)
                        ?? throw new NotFoundException("Departure not found.");

        PromoCode? promo = null;
        string? promoMessage = null;
        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            promo = await FindPromoAsync(request.PromoCode, ct);
            promoMessage = promo is null ? "Promo code not recognised." : promo.WhyInvalid(Now);
            if (promoMessage is not null) promo = null;
            else promoMessage = $"{promo!.DiscountPercent}% off applied - {promo.Description}";
        }

        var price = PriceCalculator.Calculate(departure.PricePerPerson, request.Seats, promo, Now);
        return new QuoteDto(price.UnitPrice, price.Seats, price.Subtotal, price.Discount, price.Total,
            price.PromoCode, promoMessage, departure.SeatsAvailable);
    }

    /// <summary>
    /// Holds seats for 15 minutes and creates a booking waiting for payment.
    /// Seat counts use optimistic concurrency: if another booking changed the departure
    /// at the same moment, we reload and try again instead of overselling.
    /// </summary>
    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var userId = UserId;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var departure = await db.Departures.Include(d => d.TourPackage)
                                    .FirstOrDefaultAsync(d => d.Id == request.DepartureId && d.TourPackage.IsPublished, ct)
                                ?? throw new NotFoundException("Departure not found.");

                PromoCode? promo = null;
                if (!string.IsNullOrWhiteSpace(request.PromoCode))
                    promo = await FindPromoAsync(request.PromoCode, ct)
                            ?? throw new DomainException("Promo code not recognised.");

                var travellers = request.Travellers.Select(t => new Traveller
                {
                    FullName = t.FullName.Trim(),
                    Nationality = t.Nationality.Trim(),
                    PassportNumber = t.PassportNumber?.Trim().ToUpperInvariant(),
                    DateOfBirth = t.DateOfBirth
                }).ToList();

                var booking = Booking.Create(userId, departure, travellers, promo,
                    request.ContactEmail.Trim(), request.ContactPhone.Trim(), request.SpecialRequests?.Trim(), Now);

                db.Bookings.Add(booking);
                await db.SaveChangesAsync(ct);

                return await GetAsync(booking.Reference, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                db.ResetTracking(); // someone else booked at the same time - reload fresh seat counts
            }
        }
    }

    public async Task<BookingDto> PayAsync(string reference, PayRequest request, CancellationToken ct = default)
    {
        await payValidator.ValidateAndThrowAsync(request, ct);
        var booking = await LoadOwnedAsync(reference, ct);

        if (booking.Status == BookingStatus.PendingPayment && Now > booking.ExpiresAt)
        {
            booking.Expire(Now);
            await db.SaveChangesAsync(ct);
            throw new DomainException("The 15-minute payment window has expired and the seats were released. Please book again.");
        }
        if (booking.Status != BookingStatus.PendingPayment)
            throw new DomainException($"Booking is {booking.Status} and cannot be paid.");

        var result = await paymentGateway.ChargeAsync(booking.Total, booking.Reference, request.Method, ct);
        if (!result.Succeeded)
        {
            booking.Payments.Add(new Payment
            {
                Amount = booking.Total, Status = PaymentStatus.Failed, TransactionId = result.TransactionId,
                Method = request.Method, CreatedAt = Now
            });
            await db.SaveChangesAsync(ct);
            throw new DomainException($"Payment declined: {result.FailureReason}");
        }

        booking.Confirm(new Payment
        {
            Amount = booking.Total, Status = PaymentStatus.Succeeded, TransactionId = result.TransactionId,
            Method = request.Method, CreatedAt = Now
        }, Now);
        await db.SaveChangesAsync(ct);

        await notifications.BookingEventAsync(ToNotification(booking, "BookingConfirmed"), ct);
        return ToDto(booking, Now);
    }

    public async Task<BookingDto> GetAsync(string reference, CancellationToken ct = default) =>
        ToDto(await LoadOwnedAsync(reference, ct, tracking: false), Now);

    public async Task<PagedResult<BookingListItemDto>> GetMineAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var userId = UserId;
        var q = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(b => b.CreatedAt)
            .Page(page, pageSize)
            .Select(ToListItem)
            .ToListAsync(ct);
        return new PagedResult<BookingListItemDto>(items, total, page, pageSize);
    }

    public async Task<CancellationQuoteDto> QuoteCancellationAsync(string reference, CancellationToken ct = default)
    {
        var booking = await LoadOwnedAsync(reference, ct, tracking: false);
        var today = DateOnly.FromDateTime(Now);
        var paid = booking.Status == BookingStatus.Confirmed ? booking.Total : 0;
        var quote = CancellationPolicy.RefundFor(paid, booking.Departure.StartDate, today);

        return new CancellationQuoteDto(paid, quote.RefundPercent, quote.Refund, quote.Fee, quote.DaysBeforeDeparture,
            CancellationPolicy.Tiers.Select(t => new CancellationTierDto(t.MinDays, t.Percent)).ToList());
    }

    public async Task<BookingDto> CancelAsync(string reference, CancelBookingRequest request, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var booking = await LoadOwnedAsync(reference, ct);
                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? (currentUser.IsStaff ? "Cancelled by agent" : "Cancelled by customer")
                    : request.Reason.Trim();

                booking.Cancel(reason, Now);
                await db.SaveChangesAsync(ct);

                await notifications.BookingEventAsync(ToNotification(booking, "BookingCancelled"), ct);
                return ToDto(booking, Now);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                db.ResetTracking();
            }
        }
    }

    // ---------- helpers ----------

    private Task<PromoCode?> FindPromoAsync(string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.PromoCodes.FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    /// <summary>Loads a booking the current user may see: their own, or any booking for staff.</summary>
    private async Task<Booking> LoadOwnedAsync(string reference, CancellationToken ct, bool tracking = true)
    {
        var q = db.Bookings
            .Include(b => b.Departure).ThenInclude(d => d.TourPackage)
            .Include(b => b.User)
            .Include(b => b.Payments)
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();

        var booking = await q.FirstOrDefaultAsync(b => b.Reference == reference.ToUpper(), ct)
                      ?? throw new NotFoundException($"Booking {reference} not found.");

        if (booking.UserId != UserId && !currentUser.IsStaff)
            throw new NotFoundException($"Booking {reference} not found."); // don't reveal other customers' bookings

        return booking;
    }

    internal static BookingNotification ToNotification(Booking b, string evt) => new(
        b.Reference, evt, b.User.FullName, b.Departure.TourPackage.Title, b.Departure.StartDate, b.Seats, b.Total);

    internal static readonly System.Linq.Expressions.Expression<Func<Booking, BookingListItemDto>> ToListItem = b =>
        new BookingListItemDto(
            b.Id, b.Reference, b.Status,
            b.Departure.TourPackage.Title, b.Departure.TourPackage.Slug, b.Departure.TourPackage.Destination,
            b.Departure.StartDate, b.Seats, b.Total, b.User.FullName, b.CreatedAt);

    internal static BookingDto ToDto(Booking b, DateTime utcNow)
    {
        var package = b.Departure.TourPackage;
        var today = DateOnly.FromDateTime(utcNow);
        return new BookingDto(
            b.Id,
            b.Reference,
            b.Status,
            new BookingPackageDto(package.Slug, package.Title, package.Destination, package.Country, package.DurationDays, package.Category),
            b.Departure.StartDate,
            b.Departure.StartDate.AddDays(package.DurationDays - 1),
            b.Seats,
            b.UnitPrice,
            b.Subtotal,
            b.Discount,
            b.Total,
            b.PromoCode,
            b.RefundAmount,
            b.User.FullName,
            b.ContactEmail,
            b.ContactPhone,
            b.SpecialRequests,
            b.Travellers.Select(t => new TravellerDto(t.FullName, t.Nationality, MaskPassport(t.PassportNumber), t.DateOfBirth)).ToList(),
            b.Payments.OrderBy(p => p.CreatedAt).Select(p => new PaymentDto(p.Amount, p.Status, p.TransactionId, p.Method, p.CreatedAt)).ToList(),
            b.CreatedAt,
            b.ExpiresAt,
            b.ConfirmedAt,
            b.CancelledAt,
            b.CancellationReason,
            CanPay: b.Status == BookingStatus.PendingPayment && utcNow <= b.ExpiresAt,
            CanCancel: b.Status is BookingStatus.PendingPayment or BookingStatus.Confirmed && b.Departure.StartDate > today);
    }

    /// <summary>Only show the last 3 characters of passport numbers in API responses.</summary>
    private static string? MaskPassport(string? passport) =>
        string.IsNullOrEmpty(passport) || passport.Length <= 3 ? passport : new string('•', passport.Length - 3) + passport[^3..];
}
