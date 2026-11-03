using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Safar.Domain.Enums;
using Safar.Infrastructure.Persistence;

namespace Safar.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs every minute: bookings still unpaid after the 15-minute hold are expired
/// and their seats go back on sale.
/// </summary>
public class ExpireUnpaidBookingsJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ExpireUnpaidBookingsJob> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                var expired = await RunOnceAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Expired {Count} unpaid booking(s)", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Expiring unpaid bookings failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;

        var overdue = await db.Bookings
            .Include(b => b.Departure)
            .Where(b => b.Status == BookingStatus.PendingPayment && b.ExpiresAt < now)
            .Take(200)
            .ToListAsync(ct);

        foreach (var booking in overdue) booking.Expire(now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A customer booked the same departure at that moment - the next run will pick these up
            return 0;
        }
        return overdue.Count;
    }
}
