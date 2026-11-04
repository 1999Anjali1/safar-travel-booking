using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Safar.Api.Hubs;
using Safar.Application.Common;
using Safar.Domain.Common;
using Safar.Domain.Enums;
using Safar.Infrastructure.Auth;

namespace Safar.Api.Infrastructure;

/// <summary>Reads the signed-in user from the JWT claims.</summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaims.UserId), out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(AppClaims.Role), out var role) ? role : null;
}

/// <summary>Sends booking events to everyone connected to the staff notifications hub.</summary>
public class SignalRNotificationService(IHubContext<NotificationsHub> hub) : INotificationService
{
    public Task BookingEventAsync(BookingNotification notification, CancellationToken ct = default) =>
        hub.Clients.Group(NotificationsHub.StaffGroup).SendAsync("bookingEvent", notification, ct);
}

/// <summary>
/// Maps exceptions to RFC 7807 ProblemDetails:
/// validation → 400, unauthorized → 401, forbidden → 403, not found → 404, conflict → 409, business rule → 422.
/// </summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Seats on this departure changed while you were booking. Please try again."),
            DomainException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong. Please try again.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);

        var problem = new ProblemDetails { Status = status, Title = title };

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}

/// <summary>/health is Unhealthy until PostgreSQL is reachable and every migration is applied (Kubernetes readiness).</summary>
public class DatabaseHealthCheck(Safar.Infrastructure.Persistence.AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!await db.Database.CanConnectAsync(ct)) return HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL");
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
        return pending == 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy($"{pending} migration(s) pending");
    }
}

public static class DatabaseSetup
{
    /// <summary>Applies EF Core migrations (waiting up to ~1 minute for the database) and seeds demo data.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Safar.Infrastructure.Persistence.AppDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 30 && ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException or InvalidOperationException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/30): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        if (config.GetValue("Database:SeedDemoData", true))
        {
            await Safar.Infrastructure.Persistence.DbSeeder.SeedAsync(db,
                scope.ServiceProvider.GetRequiredService<IPasswordService>(),
                scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }
        logger.LogInformation("Database migrated{Seed}", config.GetValue("Database:SeedDemoData", true) ? " and seeded" : "");
    }
}
