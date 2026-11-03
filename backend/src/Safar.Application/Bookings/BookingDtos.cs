using FluentValidation;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Application.Bookings;

public record TravellerDto(string FullName, string Nationality, string? PassportNumber, DateOnly? DateOfBirth);

public record QuoteRequest(int DepartureId, int Seats, string? PromoCode);

public record QuoteDto(
    decimal UnitPrice, int Seats, decimal Subtotal, decimal Discount, decimal Total,
    string? PromoCode, string? PromoMessage, int SeatsAvailable);

public record CreateBookingRequest(
    int DepartureId,
    IReadOnlyList<TravellerDto> Travellers,
    string ContactEmail,
    string ContactPhone,
    string? PromoCode,
    string? SpecialRequests);

public record PayRequest(string Method);

public record CancelBookingRequest(string? Reason);

public record PaymentDto(decimal Amount, PaymentStatus Status, string TransactionId, string Method, DateTime CreatedAt);

public record BookingPackageDto(string Slug, string Title, string Destination, string Country, int DurationDays, PackageCategory Category);

public record BookingDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    BookingPackageDto Package,
    DateOnly DepartureDate,
    DateOnly ReturnDate,
    int Seats,
    decimal UnitPrice,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string? PromoCode,
    decimal RefundAmount,
    string CustomerName,
    string ContactEmail,
    string ContactPhone,
    string? SpecialRequests,
    IReadOnlyList<TravellerDto> Travellers,
    IReadOnlyList<PaymentDto> Payments,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    bool CanPay,
    bool CanCancel);

public record BookingListItemDto(
    Guid Id,
    string Reference,
    BookingStatus Status,
    string PackageTitle,
    string PackageSlug,
    string Destination,
    DateOnly DepartureDate,
    int Seats,
    decimal Total,
    string CustomerName,
    DateTime CreatedAt);

public record CancellationTierDto(int MinDaysBefore, decimal RefundPercent);

public record CancellationQuoteDto(
    decimal AmountPaid, decimal RefundPercent, decimal Refund, decimal Fee, int DaysBeforeDeparture,
    IReadOnlyList<CancellationTierDto> Policy);

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.DepartureId).GreaterThan(0);
        RuleFor(x => x.Travellers).NotEmpty()
            .Must(t => t.Count <= Booking.MaxSeatsPerBooking)
            .WithMessage($"Maximum {Booking.MaxSeatsPerBooking} travellers per booking.");
        RuleForEach(x => x.Travellers).ChildRules(t =>
        {
            t.RuleFor(x => x.FullName).NotEmpty().Length(2, 100);
            t.RuleFor(x => x.Nationality).NotEmpty().MaximumLength(60);
            t.RuleFor(x => x.PassportNumber).MaximumLength(20)
                .Matches("^[A-Za-z0-9]*$").WithMessage("Passport number can only contain letters and digits.");
            t.RuleFor(x => x.DateOfBirth)
                .Must(d => d is null || d < DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Date of birth must be in the past.");
        });
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^\+?[0-9 ]{7,20}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.PromoCode).MaximumLength(30);
        RuleFor(x => x.SpecialRequests).MaximumLength(500);
    }
}

public class QuoteRequestValidator : AbstractValidator<QuoteRequest>
{
    public QuoteRequestValidator()
    {
        RuleFor(x => x.DepartureId).GreaterThan(0);
        RuleFor(x => x.Seats).InclusiveBetween(1, Booking.MaxSeatsPerBooking);
    }
}

public class PayRequestValidator : AbstractValidator<PayRequest>
{
    public static readonly string[] Methods = ["Card", "ApplePay", "GooglePay"];

    public PayRequestValidator()
    {
        RuleFor(x => x.Method).Must(m => Methods.Contains(m))
            .WithMessage($"Payment method must be one of: {string.Join(", ", Methods)}.");
    }
}
