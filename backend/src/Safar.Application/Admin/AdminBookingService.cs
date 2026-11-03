using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Safar.Application.Bookings;
using Safar.Application.Common;
using Safar.Domain.Common;
using Safar.Domain.Entities;

namespace Safar.Application.Admin;

public interface IAdminBookingService
{
    Task<PagedResult<BookingListItemDto>> SearchAsync(AdminBookingQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<PromoCodeDto>> GetPromoCodesAsync(CancellationToken ct = default);
    Task<PromoCodeDto> CreatePromoCodeAsync(CreatePromoCodeRequest request, CancellationToken ct = default);
    Task<PromoCodeDto> SetPromoActiveAsync(int id, bool active, CancellationToken ct = default);
}

public class AdminBookingService(IAppDbContext db, IValidator<CreatePromoCodeRequest> promoValidator) : IAdminBookingService
{
    public async Task<PagedResult<BookingListItemDto>> SearchAsync(AdminBookingQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = db.Bookings.AsNoTracking().AsQueryable();

        if (query.Status.HasValue) q = q.Where(b => b.Status == query.Status);
        if (query.DepartureFrom.HasValue) q = q.Where(b => b.Departure.StartDate >= query.DepartureFrom);
        if (query.DepartureTo.HasValue) q = q.Where(b => b.Departure.StartDate <= query.DepartureTo);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(b => b.Reference.ToLower().Contains(term)
                          || b.User.FullName.ToLower().Contains(term)
                          || b.ContactEmail.ToLower().Contains(term)
                          || b.Departure.TourPackage.Title.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(b => b.CreatedAt)
            .Page(query.Page, pageSize)
            .Select(BookingService.ToListItem)
            .ToListAsync(ct);

        return new PagedResult<BookingListItemDto>(items, total, query.Page, pageSize);
    }

    public async Task<IReadOnlyList<PromoCodeDto>> GetPromoCodesAsync(CancellationToken ct = default) =>
        await db.PromoCodes.AsNoTracking().OrderByDescending(p => p.ValidUntil).Select(p => ToDto(p)).ToListAsync(ct);

    public async Task<PromoCodeDto> CreatePromoCodeAsync(CreatePromoCodeRequest request, CancellationToken ct = default)
    {
        await promoValidator.ValidateAndThrowAsync(request, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.PromoCodes.AnyAsync(p => p.Code == code, ct))
            throw new ConflictException($"Promo code {code} already exists.");

        var promo = new PromoCode
        {
            Code = code,
            Description = request.Description.Trim(),
            DiscountPercent = request.DiscountPercent,
            MaxDiscount = request.MaxDiscount,
            ValidUntil = DateTime.SpecifyKind(request.ValidUntil, DateTimeKind.Utc),
            MaxUses = request.MaxUses
        };
        db.PromoCodes.Add(promo);
        await db.SaveChangesAsync(ct);
        return ToDto(promo);
    }

    public async Task<PromoCodeDto> SetPromoActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var promo = await db.PromoCodes.FindAsync([id], ct) ?? throw new NotFoundException("Promo code not found.");
        promo.IsActive = active;
        await db.SaveChangesAsync(ct);
        return ToDto(promo);
    }

    private static PromoCodeDto ToDto(PromoCode p) =>
        new(p.Id, p.Code, p.Description, p.DiscountPercent, p.MaxDiscount, p.ValidUntil, p.MaxUses, p.UsedCount, p.IsActive);
}
