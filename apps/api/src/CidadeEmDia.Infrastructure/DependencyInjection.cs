using CidadeEmDia.Application.Authentication;
using CidadeEmDia.Application.Chat;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Infrastructure.Chat;
using CidadeEmDia.Infrastructure.Identity;
using CidadeEmDia.Infrastructure.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
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
        var r2Options = R2Options.FromConfiguration(configuration);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(passwordResetOptions);
        services.AddSingleton(subaccountInvitationOptions);
        services.AddSingleton(r2Options);
        services.AddSingleton<R2ObjectStorage>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddSingleton<ISubaccountInvitationEmailSender, SmtpSubaccountInvitationEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISubaccountLimitProvider, ConfigurationSubaccountLimitProvider>();
        services.AddScoped<IMasterSubaccountService, MasterSubaccountService>();
        services.AddScoped<ISubaccountAccessStateService, SubaccountAccessStateService>();
        services.AddScoped<IOccurrenceService, OccurrenceService>();
        services.AddScoped<IOccurrenceCreationService, OccurrenceCreationService>();
        services.AddScoped<IOccurrenceTargetDecisionService, OccurrenceTargetDecisionService>();
        services.AddScoped<IOccurrenceLifecycleService, OccurrenceLifecycleService>();
        services.AddScoped<IOccurrenceFollowUpService, OccurrenceFollowUpService>();
        services.AddScoped<IOccurrenceSupportService, OccurrenceSupportService>();
        services.AddScoped<IOccurrenceMediaService, OccurrenceMediaService>();
        services.AddScoped<IOccurrenceAssignmentService, OccurrenceAssignmentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddHostedService<IdentitySeedHostedService>();

        return services;
    }
}
