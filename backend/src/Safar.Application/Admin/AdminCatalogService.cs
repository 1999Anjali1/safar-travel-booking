using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Safar.Application.Bookings;
using Safar.Application.Common;
using Safar.Domain.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Application.Admin;

public interface IAdminCatalogService
{
    Task<IReadOnlyList<AdminPackageListItemDto>> GetPackagesAsync(CancellationToken ct = default);
    Task<AdminPackageDetailDto> GetPackageAsync(int id, CancellationToken ct = default);
    Task<AdminPackageDetailDto> CreatePackageAsync(UpsertPackageRequest request, CancellationToken ct = default);
    Task<AdminPackageDetailDto> UpdatePackageAsync(int id, UpsertPackageRequest request, CancellationToken ct = default);
    Task<AdminPackageDetailDto> SetPublishedAsync(int id, bool published, CancellationToken ct = default);
    Task<AdminPackageDetailDto> AddDepartureAsync(int packageId, AddDepartureRequest request, CancellationToken ct = default);
    Task<AdminPackageDetailDto> CancelDepartureAsync(int departureId, CancelDepartureRequest request, CancellationToken ct = default);
    Task<DepartureManifestDto> GetManifestAsync(int departureId, CancellationToken ct = default);
}

public partial class AdminCatalogService(
    IAppDbContext db,
    INotificationService notifications,
    TimeProvider clock,
    IValidator<UpsertPackageRequest> packageValidator,
    IValidator<AddDepartureRequest> departureValidator) : IAdminCatalogService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now);

    public async Task<IReadOnlyList<AdminPackageListItemDto>> GetPackagesAsync(CancellationToken ct = default)
    {
        var today = Today;
        return await db.TourPackages.AsNoTracking()
            .OrderBy(p => p.Title)
            .Select(p => new AdminPackageListItemDto(
                p.Id, p.Slug, p.Title, p.Destination, p.Country, p.Category, p.DurationDays, p.BasePrice, p.IsPublished,
                p.Departures.Count(d => d.Status == DepartureStatus.Scheduled && d.StartDate >= today),
                p.Departures.Sum(d => d.SeatsBooked)))
            .ToListAsync(ct);
    }

    public async Task<AdminPackageDetailDto> GetPackageAsync(int id, CancellationToken ct = default)
    {
        var p = await db.TourPackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException($"Package {id} not found.");

        var since = Today.AddDays(-30);
        var departures = await db.Departures.AsNoTracking()
            .Where(d => d.TourPackageId == id && d.StartDate >= since)
            .OrderBy(d => d.StartDate)
            .Select(d => new AdminDepartureDto(
                d.Id, d.StartDate, d.Capacity, d.SeatsBooked, d.PriceOverride, d.PriceOverride ?? p.BasePrice, d.Status,
                d.Bookings.Count(b => b.Status == BookingStatus.Confirmed),
                d.Bookings.Count(b => b.Status == BookingStatus.PendingPayment)))
            .ToListAsync(ct);

        return new AdminPackageDetailDto(
            p.Id, p.Slug, p.Title, p.Summary, p.Description, p.Destination, p.Country, p.Category, p.DurationDays,
            p.BasePrice, p.ImageUrl, p.Highlights, p.Inclusions, p.IsPublished, departures);
    }

    public async Task<AdminPackageDetailDto> CreatePackageAsync(UpsertPackageRequest request, CancellationToken ct = default)
    {
        await packageValidator.ValidateAndThrowAsync(request, ct);

        var slug = Slugify(request.Title);
        var suffix = 2;
        var baseSlug = slug;
        while (await db.TourPackages.AnyAsync(p => p.Slug == slug, ct))
            slug = $"{baseSlug}-{suffix++}";

        var package = new TourPackage { Slug = slug, CreatedAt = Now, IsPublished = false };
        Apply(request, package);
        db.TourPackages.Add(package);
        await db.SaveChangesAsync(ct);
        return await GetPackageAsync(package.Id, ct);
    }

    public async Task<AdminPackageDetailDto> UpdatePackageAsync(int id, UpsertPackageRequest request, CancellationToken ct = default)
    {
        await packageValidator.ValidateAndThrowAsync(request, ct);
        var package = await db.TourPackages.FindAsync([id], ct) ?? throw new NotFoundException($"Package {id} not found.");
        Apply(request, package);
        await db.SaveChangesAsync(ct);
        return await GetPackageAsync(id, ct);
    }

    public async Task<AdminPackageDetailDto> SetPublishedAsync(int id, bool published, CancellationToken ct = default)
    {
        var package = await db.TourPackages.FindAsync([id], ct) ?? throw new NotFoundException($"Package {id} not found.");
        package.IsPublished = published;
        await db.SaveChangesAsync(ct);
        return await GetPackageAsync(id, ct);
    }

    public async Task<AdminPackageDetailDto> AddDepartureAsync(int packageId, AddDepartureRequest request, CancellationToken ct = default)
    {
        await departureValidator.ValidateAndThrowAsync(request, ct);

        if (!await db.TourPackages.AnyAsync(p => p.Id == packageId, ct))
            throw new NotFoundException($"Package {packageId} not found.");
        if (await db.Departures.AnyAsync(d => d.TourPackageId == packageId && d.StartDate == request.StartDate
                                             && d.Status == DepartureStatus.Scheduled, ct))
            throw new ConflictException($"A departure on {request.StartDate:dd MMM yyyy} already exists.");

        db.Departures.Add(new Departure
        {
            TourPackageId = packageId,
            StartDate = request.StartDate,
            Capacity = request.Capacity,
            PriceOverride = request.PriceOverride
        });
        await db.SaveChangesAsync(ct);
        return await GetPackageAsync(packageId, ct);
    }

    /// <summary>Operator cancels a trip (weather, low numbers…): every booking is cancelled with a full refund.</summary>
    public async Task<AdminPackageDetailDto> CancelDepartureAsync(int departureId, CancelDepartureRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("A reason is required to cancel a departure.");

        var departure = await db.Departures
                            .Include(d => d.TourPackage)
                            .Include(d => d.Bookings.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment))
                                .ThenInclude(b => b.User)
                            .Include(d => d.Bookings).ThenInclude(b => b.Payments)
                            .AsSplitQuery()
                            .FirstOrDefaultAsync(d => d.Id == departureId, ct)
                        ?? throw new NotFoundException($"Departure {departureId} not found.");

        if (departure.Status == DepartureStatus.Cancelled)
            throw new DomainException("Departure is already cancelled.");

        var affected = departure.Bookings
            .Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.PendingPayment)
            .ToList();

        foreach (var booking in affected)
            booking.Cancel($"Departure cancelled by operator: {request.Reason.Trim()}", Now, fullRefund: true);

        departure.Status = DepartureStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        foreach (var booking in affected)
            await notifications.BookingEventAsync(BookingService.ToNotification(booking, "BookingCancelled"), ct);

        return await GetPackageAsync(departure.TourPackageId, ct);
    }

    /// <summary>Passenger manifest for the tour guide / airline - confirmed travellers only.</summary>
    public async Task<DepartureManifestDto> GetManifestAsync(int departureId, CancellationToken ct = default)
    {
        var departure = await db.Departures.AsNoTracking()
                            .Include(d => d.TourPackage)
                            .Include(d => d.Bookings.Where(b => b.Status == BookingStatus.Confirmed))
                            .FirstOrDefaultAsync(d => d.Id == departureId, ct)
                        ?? throw new NotFoundException($"Departure {departureId} not found.");

        var travellers = departure.Bookings
            .OrderBy(b => b.Reference)
            .SelectMany(b => b.Travellers.Select(t => new ManifestTravellerDto(
                b.Reference, t.FullName, t.Nationality, t.PassportNumber, t.DateOfBirth, b.ContactPhone, b.SpecialRequests)))
            .ToList();

        return new DepartureManifestDto(departure.Id, departure.TourPackage.Title, departure.StartDate,
            departure.Capacity, departure.SeatsBooked, travellers);
    }

    private static void Apply(UpsertPackageRequest r, TourPackage p)
    {
        p.Title = r.Title.Trim();
        p.Summary = r.Summary.Trim();
        p.Description = r.Description.Trim();
        p.Destination = r.Destination.Trim();
        p.Country = r.Country.Trim();
        p.Category = r.Category;
        p.DurationDays = r.DurationDays;
        p.BasePrice = r.BasePrice;
        p.ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl.Trim();
        p.Highlights = r.Highlights.Select(h => h.Trim()).Where(h => h.Length > 0).ToList();
        p.Inclusions = r.Inclusions.Select(h => h.Trim()).Where(h => h.Length > 0).ToList();
    }

    internal static string Slugify(string text)
    {
        var slug = NonAlphanumeric().Replace(text.ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
