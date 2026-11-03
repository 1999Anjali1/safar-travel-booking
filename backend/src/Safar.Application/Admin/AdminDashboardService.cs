using Microsoft.EntityFrameworkCore;
using Safar.Application.Bookings;
using Safar.Application.Common;
using Safar.Domain.Enums;

namespace Safar.Application.Admin;

public interface IAdminDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class AdminDashboardService(IAppDbContext db, TimeProvider clock) : IAdminDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);
        var sixMonthsAgo = monthStart.AddMonths(-5);

        // Net revenue = successful charges minus refunds (refunds are stored as negative amounts)
        var payments = db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Refunded);

        var revenueThisMonth = await payments.Where(p => p.CreatedAt >= monthStart).SumAsync(p => p.Amount, ct);
        var revenueLastMonth = await payments
            .Where(p => p.CreatedAt >= lastMonthStart && p.CreatedAt < monthStart).SumAsync(p => p.Amount, ct);

        var revenueRows = await payments
            .Where(p => p.CreatedAt >= sixMonthsAgo)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(p => p.Amount),
                Bookings = g.Count(p => p.Status == PaymentStatus.Succeeded)
            })
            .ToListAsync(ct);

        // Always return 6 months, filling gaps with zero
        var revenueByMonth = Enumerable.Range(0, 6)
            .Select(i => sixMonthsAgo.AddMonths(i))
            .Select(m =>
            {
                var row = revenueRows.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month);
                return new MonthlyRevenueDto(m.Year, m.Month, row?.Revenue ?? 0, row?.Bookings ?? 0);
            })
            .ToList();

        var confirmedThisMonth = await db.Bookings.CountAsync(
            b => b.Status == BookingStatus.Confirmed && b.ConfirmedAt >= monthStart, ct);
        var pendingPayments = await db.Bookings.CountAsync(b => b.Status == BookingStatus.PendingPayment, ct);
        var in30Days = today.AddDays(30);
        var travellersNext30 = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.Departure.StartDate >= today && b.Departure.StartDate <= in30Days)
            .SumAsync(b => b.Seats, ct);
        var customers = await db.Users.CountAsync(u => u.Role == UserRole.Customer, ct);

        var topRows = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed)
            .GroupBy(b => b.Departure.TourPackageId)
            .Select(g => new { PackageId = g.Key, Bookings = g.Count(), Travellers = g.Sum(b => b.Seats), Revenue = g.Sum(b => b.Total) })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(ct);
        var topIds = topRows.Select(r => r.PackageId).ToList();
        var packageNames = await db.TourPackages.AsNoTracking()
            .Where(p => topIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Title, p.Destination }, ct);
        var topPackages = topRows
            .Select(r => new TopPackageDto(r.PackageId, packageNames[r.PackageId].Title, packageNames[r.PackageId].Destination,
                r.Bookings, r.Travellers, r.Revenue))
            .ToList();

        var in60Days = today.AddDays(60);
        var upcoming = await db.Departures.AsNoTracking()
            .Where(d => d.Status == DepartureStatus.Scheduled && d.StartDate >= today && d.StartDate <= in60Days)
            .OrderBy(d => d.StartDate)
            .Select(d => new DepartureOccupancyDto(
                d.Id, d.TourPackageId, d.TourPackage.Title, d.StartDate, d.Capacity, d.SeatsBooked,
                d.Capacity == 0 ? 0 : Math.Round(d.SeatsBooked * 100m / d.Capacity, 1)))
            .ToListAsync(ct);

        var recent = await db.Bookings.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .Select(BookingService.ToListItem)
            .ToListAsync(ct);

        return new DashboardDto(
            revenueThisMonth,
            revenueLastMonth,
            confirmedThisMonth,
            pendingPayments,
            travellersNext30,
            upcoming.Count == 0 ? 0 : Math.Round(upcoming.Average(d => d.OccupancyPercent), 1),
            customers,
            revenueByMonth,
            topPackages,
            upcoming.Take(10).ToList(),
            recent);
    }
}
