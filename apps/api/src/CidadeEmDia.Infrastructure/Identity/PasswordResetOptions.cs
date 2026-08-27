using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed record PasswordResetOptions(
    TimeSpan TokenLifetime,
    string ResetUrl,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string SmtpPassword,
    string FromAddress,
    string FromName,
    bool EnableSsl)
{
    public bool IsEmailConfigured =>
        !string.IsNullOrWhiteSpace(ResetUrl) &&
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(FromAddress);

    public static PasswordResetOptions FromConfiguration(IConfiguration configuration)
    {
        var tokenMinutes = int.TryParse(configuration["PASSWORD_RESET_TOKEN_MINUTES"], out var configuredMinutes)
            ? Math.Clamp(configuredMinutes, 5, 120)
            : 30;

        var smtpPort = int.TryParse(configuration["SMTP_PORT"], out var configuredPort)
            ? configuredPort
            : 587;

        var enableSsl = !bool.TryParse(configuration["SMTP_ENABLE_SSL"], out var configuredSsl) || configuredSsl;
        var smtpUsername = configuration["SMTP_USER"]?.Trim() ?? string.Empty;
        var fromAddress = configuration["SMTP_FROM"]?.Trim();
        if (string.IsNullOrWhiteSpace(fromAddress))
            fromAddress = smtpUsername;

        return new PasswordResetOptions(
            TimeSpan.FromMinutes(tokenMinutes),
            configuration["PASSWORD_RESET_URL"]?.Trim() ?? string.Empty,
            configuration["SMTP_HOST"]?.Trim() ?? string.Empty,
            smtpPort,
            smtpUsername,
            configuration["SMTP_PASSWORD"] ?? string.Empty,
            fromAddress ?? string.Empty,
            configuration["SMTP_FROM_NAME"]?.Trim() ?? "CidadeEmDia",
            enableSsl);
    }
}
