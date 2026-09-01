using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public sealed record MercadoPagoOptions(
    string AccessToken,
    string WebhookSecret,
    string ApiBaseUrl,
    string BackUrl)
{
    public const string DefaultApiBaseUrl = "https://api.mercadopago.com";

    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);
    public bool HasWebhookSecret => !string.IsNullOrWhiteSpace(WebhookSecret);
    public bool HasBackUrl => Uri.TryCreate(BackUrl, UriKind.Absolute, out _);

    public static MercadoPagoOptions FromConfiguration(IConfiguration configuration)
    {
        var accessToken = configuration["MERCADOPAGO_ACCESS_TOKEN"]?.Trim() ?? string.Empty;
        var webhookSecret = configuration["MERCADOPAGO_WEBHOOK_SECRET"]?.Trim() ?? string.Empty;

        var apiBaseUrl = configuration["MERCADOPAGO_API_BASE_URL"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            apiBaseUrl = DefaultApiBaseUrl;

        apiBaseUrl = apiBaseUrl.TrimEnd('/');

        var backUrl = configuration["MERCADOPAGO_BACK_URL"]?.Trim() ?? string.Empty;

        return new MercadoPagoOptions(
            accessToken,
            webhookSecret,
            apiBaseUrl,
            backUrl);
    }
}
