using CidadeEmDia.Application.Administration;
using CidadeEmDia.Application.Authentication;
using CidadeEmDia.Application.Billing;
using CidadeEmDia.Application.Chat;
using CidadeEmDia.Application.Content;
using CidadeEmDia.Application.Institutions;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Infrastructure.Administration;
using CidadeEmDia.Infrastructure.Billing;
using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using CidadeEmDia.Infrastructure.Chat;
using CidadeEmDia.Infrastructure.Content;
using CidadeEmDia.Infrastructure.Identity;
using CidadeEmDia.Infrastructure.Institutions;
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
        var mercadoPagoOptions = MercadoPagoOptions.FromConfiguration(configuration);
        services.AddSingleton(jwtOptions);
        services.AddSingleton(passwordResetOptions);
        services.AddSingleton(subaccountInvitationOptions);
        services.AddSingleton(r2Options);
        services.AddSingleton(mercadoPagoOptions);
        services.AddSingleton<MercadoPagoWebhookSignatureValidator>();
        services.AddHttpClient<
            IMercadoPagoClient,
            MercadoPagoClient>(
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        services.AddSingleton<R2ObjectStorage>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddSingleton<ISubaccountInvitationEmailSender, SmtpSubaccountInvitationEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IBillingCatalogService, BillingCatalogService>();
        services.AddScoped<IBillingEntitlementService, BillingEntitlementService>();
        services.AddScoped<IBillingSubscriptionService, BillingSubscriptionService>();
        services.AddScoped<IBillingPublicationUsageTracker, BillingPublicationUsageTracker>();
        services.AddScoped<IBillingCheckoutService, MercadoPagoCheckoutService>();
        services.AddScoped<
            IBillingSubscriptionManagementService,
            MercadoPagoSubscriptionManagementService>();
        services.AddScoped<
            IBillingProviderWebhookService,
            MercadoPagoWebhookService>();
        services.AddScoped<ISubaccountLimitProvider, BillingSubaccountLimitProvider>();
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
        services.AddScoped<IContentService, ContentService>();
        services.AddHostedService<IdentitySeedHostedService>();
        services.AddHostedService<BillingCatalogSeedHostedService>();

        return services;
    }
}
