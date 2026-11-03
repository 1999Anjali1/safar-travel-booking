using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Safar.Application.Common;
using Safar.Infrastructure.Auth;
using Safar.Infrastructure.BackgroundJobs;
using Safar.Infrastructure.Payments;
using Safar.Infrastructure.Persistence;

namespace Safar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
                               ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();

        services.AddSingleton<ExpireUnpaidBookingsJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ExpireUnpaidBookingsJob>());

        return services;
    }
}
