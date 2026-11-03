using FluentValidation;
using Safar.Application.Bookings;
using Safar.Domain.Enums;

namespace Safar.Application.Admin;

// ---------- dashboard ----------

public record MonthlyRevenueDto(int Year, int Month, decimal Revenue, int Bookings);

public record TopPackageDto(int Id, string Title, string Destination, int Bookings, int Travellers, decimal Revenue);

public record DepartureOccupancyDto(
    int Id, int PackageId, string PackageTitle, DateOnly StartDate, int Capacity, int SeatsBooked, decimal OccupancyPercent);

public record DashboardDto(
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    int ConfirmedBookingsThisMonth,
    int PendingPayments,
    int TravellersNext30Days,
    decimal AverageOccupancy,
    int TotalCustomers,
    IReadOnlyList<MonthlyRevenueDto> RevenueByMonth,
    IReadOnlyList<TopPackageDto> TopPackages,
    IReadOnlyList<DepartureOccupancyDto> UpcomingDepartures,
    IReadOnlyList<BookingListItemDto> RecentBookings);

// ---------- catalogue ----------

public record AdminPackageListItemDto(
    int Id, string Slug, string Title, string Destination, string Country, PackageCategory Category,
    int DurationDays, decimal BasePrice, bool IsPublished, int UpcomingDepartures, int SeatsSold);

public record AdminDepartureDto(
    int Id, DateOnly StartDate, int Capacity, int SeatsBooked, decimal? PriceOverride, decimal PricePerPerson,
    DepartureStatus Status, int ConfirmedBookings, int PendingBookings);

public record AdminPackageDetailDto(
    int Id, string Slug, string Title, string Summary, string Description, string Destination, string Country,
    PackageCategory Category, int DurationDays, decimal BasePrice, string? ImageUrl,
    IReadOnlyList<string> Highlights, IReadOnlyList<string> Inclusions, bool IsPublished,
    IReadOnlyList<AdminDepartureDto> Departures);

public record UpsertPackageRequest(
    string Title,
    string Summary,
    string Description,
    string Destination,
    string Country,
    PackageCategory Category,
    int DurationDays,
    decimal BasePrice,
    string? ImageUrl,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Inclusions);

public record AddDepartureRequest(DateOnly StartDate, int Capacity, decimal? PriceOverride);

public record CancelDepartureRequest(string Reason);

public record ManifestTravellerDto(
    string BookingReference, string FullName, string Nationality, string? PassportNumber, DateOnly? DateOfBirth,
    string ContactPhone, string? SpecialRequests);

public record DepartureManifestDto(
    int DepartureId, string PackageTitle, DateOnly StartDate, int Capacity, int SeatsBooked,
    IReadOnlyList<ManifestTravellerDto> Travellers);

// ---------- bookings ----------

public record AdminBookingQuery(
    BookingStatus? Status = null,
    string? Search = null,
    DateOnly? DepartureFrom = null,
    DateOnly? DepartureTo = null,
    int Page = 1,
    int PageSize = 20);

// ---------- promo codes ----------

public record PromoCodeDto(
    int Id, string Code, string Description, int DiscountPercent, decimal? MaxDiscount,
    DateTime ValidUntil, int? MaxUses, int UsedCount, bool IsActive);

public record CreatePromoCodeRequest(
    string Code, string Description, int DiscountPercent, decimal? MaxDiscount, DateTime ValidUntil, int? MaxUses);

// ---------- validators ----------

public class UpsertPackageRequestValidator : AbstractValidator<UpsertPackageRequest>
{
    public UpsertPackageRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 120);
        RuleFor(x => x.Summary).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 30);
        RuleFor(x => x.BasePrice).GreaterThan(0).LessThan(100_000);
        RuleFor(x => x.ImageUrl).MaximumLength(500)
            .Must(u => u is null || Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("Image URL must be an https:// link.");
        RuleFor(x => x.Highlights).NotNull().Must(h => h.Count <= 10).WithMessage("Maximum 10 highlights.");
        RuleForEach(x => x.Highlights).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Inclusions).NotNull().Must(h => h.Count <= 15).WithMessage("Maximum 15 inclusions.");
        RuleForEach(x => x.Inclusions).NotEmpty().MaximumLength(120);
    }
}

public class AddDepartureRequestValidator : AbstractValidator<AddDepartureRequest>
{
    public AddDepartureRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.StartDate)
            .GreaterThan(_ => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("Departure date must be in the future.");
        RuleFor(x => x.Capacity).InclusiveBetween(1, 200);
        RuleFor(x => x.PriceOverride).GreaterThan(0).When(x => x.PriceOverride.HasValue);
    }
}

public class CreatePromoCodeRequestValidator : AbstractValidator<CreatePromoCodeRequest>
{
    public CreatePromoCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3, 30).Matches("^[A-Za-z0-9]+$")
            .WithMessage("Code can only contain letters and digits.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(1, 50);
        RuleFor(x => x.MaxDiscount).GreaterThan(0).When(x => x.MaxDiscount.HasValue);
        RuleFor(x => x.ValidUntil).GreaterThan(DateTime.UtcNow).WithMessage("Expiry must be in the future.");
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses.HasValue);
    }
}
