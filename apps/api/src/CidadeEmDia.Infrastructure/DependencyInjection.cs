using CidadeEmDia.Application.Authentication;
using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Infrastructure.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CidadeEmDia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_CONNECTION"]
            ?? configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("PostgreSQL connection string was not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));

        var jwtOptions = JwtOptions.FromConfiguration(configuration);
        var passwordResetOptions = PasswordResetOptions.FromConfiguration(configuration);
        var subaccountInvitationOptions = SubaccountInvitationOptions.FromConfiguration(configuration);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(passwordResetOptions);
        services.AddSingleton(subaccountInvitationOptions);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddSingleton<ISubaccountInvitationEmailSender, SmtpSubaccountInvitationEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISubaccountLimitProvider, ConfigurationSubaccountLimitProvider>();
        services.AddScoped<IMasterSubaccountService, MasterSubaccountService>();
        services.AddScoped<ISubaccountAccessStateService, SubaccountAccessStateService>();
        services.AddHostedService<IdentitySeedHostedService>();

        return services;
    }
}
