using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Safar.Application.Common;
using Safar.Infrastructure.Ai;
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

        // AI trip planner: typed HttpClient for any OpenAI-compatible provider (Groq by default)
        services.Configure<AiOptions>(config.GetSection(AiOptions.SectionName));
        services.AddHttpClient<ILanguageModel, OpenAiCompatibleModel>((sp, client) =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(ai.BaseUrl.EndsWith('/') ? ai.BaseUrl : ai.BaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });

        services.AddSingleton<ExpireUnpaidBookingsJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ExpireUnpaidBookingsJob>());

        return services;
    }
}
