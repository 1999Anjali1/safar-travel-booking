using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Safar.Application.Admin;
using Safar.Application.Auth;
using Safar.Application.Bookings;
using Safar.Application.Packages;

namespace Safar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IAdminBookingService, AdminBookingService>();

        return services;
    }
}
