using Microsoft.EntityFrameworkCore;
using Safar.Application.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Application.Packages;

public record PackageQuery(
    string? Search = null,
    PackageCategory? Category = null,
    string? Country = null,
    decimal? MaxPrice = null,
    DateOnly? From = null,
    string Sort = "popular",
    int Page = 1,
    int PageSize = 12);

public record PackageCardDto(
    int Id,
    string Slug,
    string Title,
    string Summary,
    string Destination,
    string Country,
    PackageCategory Category,
    int DurationDays,
    decimal FromPrice,
    string? ImageUrl,
    DateOnly? NextDeparture,
    int UpcomingDepartures);

public record DepartureDto(
    int Id,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal PricePerPerson,
    int SeatsAvailable,
    int Capacity,
    bool IsBookable,
    DepartureStatus Status);

public record PackageDetailDto(
    int Id,
    string Slug,
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
    IReadOnlyList<string> Inclusions,
    IReadOnlyList<DepartureDto> Departures);

public record CatalogFiltersDto(IReadOnlyList<string> Countries, IReadOnlyList<PackageCategory> Categories, decimal MaxPrice);

public interface IPackageService
{
    Task<PagedResult<PackageCardDto>> SearchAsync(PackageQuery query, CancellationToken ct = default);
    Task<PackageDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<CatalogFiltersDto> GetFiltersAsync(CancellationToken ct = default);
}

/// <summary>Public catalogue - only published packages are visible.</summary>
public class PackageService(IAppDbContext db, TimeProvider clock) : IPackageService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<PagedResult<PackageCardDto>> SearchAsync(PackageQuery query, CancellationToken ct = default)
    {
        var today = Today;
        var pageSize = Math.Clamp(query.PageSize, 1, 48);
        var q = db.TourPackages.AsNoTracking().Where(p => p.IsPublished);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(p => p.Title.ToLower().Contains(term)
                          || p.Destination.ToLower().Contains(term)
                          || p.Country.ToLower().Contains(term));
        }
        if (query.Category.HasValue) q = q.Where(p => p.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.Country)) q = q.Where(p => p.Country == query.Country);
        if (query.MaxPrice.HasValue) q = q.Where(p => p.BasePrice <= query.MaxPrice);
        if (query.From.HasValue)
            q = q.Where(p => p.Departures.Any(d => d.Status == DepartureStatus.Scheduled && d.StartDate >= query.From));

        // Project straight to the card shape so SQL only selects what the UI needs
        var projected = q.Select(p => new
        {
            Package = p,
            Upcoming = p.Departures.Where(d => d.Status == DepartureStatus.Scheduled && d.StartDate > today),
            Booked = p.Departures.Sum(d => d.SeatsBooked)
        });

        projected = query.Sort switch
        {
            "price-asc" => projected.OrderBy(x => x.Package.BasePrice),
            "price-desc" => projected.OrderByDescending(x => x.Package.BasePrice),
            "duration" => projected.OrderBy(x => x.Package.DurationDays),
            "newest" => projected.OrderByDescending(x => x.Package.CreatedAt),
            _ => projected.OrderByDescending(x => x.Booked).ThenBy(x => x.Package.Title)
        };

        var total = await q.CountAsync(ct);
        var items = await projected.Page(query.Page, pageSize)
            .Select(x => new PackageCardDto(
                x.Package.Id,
                x.Package.Slug,
                x.Package.Title,
                x.Package.Summary,
                x.Package.Destination,
                x.Package.Country,
                x.Package.Category,
                x.Package.DurationDays,
                x.Upcoming.Min(d => (decimal?)(d.PriceOverride ?? x.Package.BasePrice)) ?? x.Package.BasePrice,
                x.Package.ImageUrl,
                x.Upcoming.OrderBy(d => d.StartDate).Select(d => (DateOnly?)d.StartDate).FirstOrDefault(),
                x.Upcoming.Count()))
            .ToListAsync(ct);

        return new PagedResult<PackageCardDto>(items, total, query.Page, pageSize);
    }

    public async Task<PackageDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var today = Today;
        var p = await db.TourPackages.AsNoTracking()
                    .Include(x => x.Departures.Where(d => d.StartDate > today).OrderBy(d => d.StartDate))
                    .FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished, ct)
                ?? throw new NotFoundException($"Tour package '{slug}' was not found.");

        return ToDetail(p, today);
    }

    public async Task<CatalogFiltersDto> GetFiltersAsync(CancellationToken ct = default)
    {
        var published = db.TourPackages.AsNoTracking().Where(p => p.IsPublished);
        var countries = await published.Select(p => p.Country).Distinct().OrderBy(c => c).ToListAsync(ct);
        var categories = await published.Select(p => p.Category).Distinct().ToListAsync(ct);
        var maxPrice = await published.MaxAsync(p => (decimal?)p.BasePrice, ct) ?? 0;
        return new CatalogFiltersDto(countries, categories.Order().ToList(), maxPrice);
    }

    internal static PackageDetailDto ToDetail(TourPackage p, DateOnly today) => new(
        p.Id, p.Slug, p.Title, p.Summary, p.Description, p.Destination, p.Country, p.Category,
        p.DurationDays, p.BasePrice, p.ImageUrl, p.Highlights, p.Inclusions,
        p.Departures.Select(d => ToDto(d, p, today)).ToList());

    internal static DepartureDto ToDto(Departure d, TourPackage p, DateOnly today) => new(
        d.Id,
        d.StartDate,
        d.StartDate.AddDays(p.DurationDays - 1),
        d.PriceOverride ?? p.BasePrice,
        d.Capacity - d.SeatsBooked,
        d.Capacity,
        d.Status == DepartureStatus.Scheduled
            && d.StartDate.DayNumber - today.DayNumber >= Departure.BookingCutOffDays
            && d.Capacity > d.SeatsBooked,
        d.Status);
}
