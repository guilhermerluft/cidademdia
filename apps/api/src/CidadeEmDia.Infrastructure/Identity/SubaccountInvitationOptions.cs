using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed record SubaccountInvitationOptions(
    TimeSpan TokenLifetime,
    string InviteUrl,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string SmtpPassword,
    string FromAddress,
    string FromName,
    bool EnableSsl)
{
    public bool IsEmailConfigured =>
        !string.IsNullOrWhiteSpace(InviteUrl) &&
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(FromAddress);

    public static SubaccountInvitationOptions FromConfiguration(IConfiguration configuration)
    {
        var tokenHours = int.TryParse(configuration["SUBACCOUNT_INVITE_HOURS"], out var configuredHours)
            ? Math.Clamp(configuredHours, 1, 168)
            : 48;

        var smtpPort = int.TryParse(configuration["SMTP_PORT"], out var configuredPort)
            ? configuredPort
            : 587;

        var enableSsl = !bool.TryParse(configuration["SMTP_ENABLE_SSL"], out var configuredSsl) || configuredSsl;
        var smtpUsername = configuration["SMTP_USER"]?.Trim() ?? string.Empty;
        var fromAddress = configuration["SMTP_FROM"]?.Trim();
        if (string.IsNullOrWhiteSpace(fromAddress))
            fromAddress = smtpUsername;

        return new SubaccountInvitationOptions(
            TimeSpan.FromHours(tokenHours),
            configuration["SUBACCOUNT_INVITE_URL"]?.Trim() ?? string.Empty,
            configuration["SMTP_HOST"]?.Trim() ?? string.Empty,
            smtpPort,
            smtpUsername,
            configuration["SMTP_PASSWORD"] ?? string.Empty,
            fromAddress ?? string.Empty,
            configuration["SMTP_FROM_NAME"]?.Trim() ?? "CidadeEmDia",
            enableSsl);
    }
}
