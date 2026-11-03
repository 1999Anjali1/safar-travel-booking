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

/// <summary>/health reports Unhealthy if PostgreSQL can't be reached.</summary>
public class DatabaseHealthCheck(Safar.Infrastructure.Persistence.AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL");
}
