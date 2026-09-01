using System.Text.Json;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public sealed record MercadoPagoWebhookEnvelope(
    string EventId,
    string Type,
    string? Action,
    string ResourceId,
    bool LiveMode)
{
    public static bool TryParse(
        string json,
        out MercadoPagoWebhookEnvelope? envelope)
    {
        envelope = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document =
                JsonDocument.Parse(json);

            var root = document.RootElement;

            if (!root.TryGetProperty(
                    "id",
                    out var eventIdElement) ||
                !root.TryGetProperty(
                    "type",
                    out var typeElement) ||
                !root.TryGetProperty(
                    "data",
                    out var dataElement) ||
                dataElement.ValueKind !=
                    JsonValueKind.Object ||
                !dataElement.TryGetProperty(
                    "id",
                    out var resourceIdElement))
            {
                return false;
            }

            var eventId =
                ReadScalar(eventIdElement);

            var type =
                ReadScalar(typeElement);

            var resourceId =
                ReadScalar(resourceIdElement);

            if (string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(type) ||
                string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            string? action = null;

            if (root.TryGetProperty(
                    "action",
                    out var actionElement))
            {
                action = ReadScalar(
                    actionElement);
            }

            var liveMode =
                root.TryGetProperty(
                    "live_mode",
                    out var liveModeElement) &&
                liveModeElement.ValueKind ==
                    JsonValueKind.True;

            envelope =
                new MercadoPagoWebhookEnvelope(
                    eventId.Trim(),
                    type.Trim(),
                    string.IsNullOrWhiteSpace(action)
                        ? null
                        : action.Trim(),
                    resourceId.Trim(),
                    liveMode);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool MatchesSignatureDataId(
        string? queryDataId)
    {
        if (string.IsNullOrWhiteSpace(
                queryDataId))
        {
            return true;
        }

        return string.Equals(
            ResourceId.Trim(),
            queryDataId.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadScalar(
        JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String =>
                element.GetString(),

            JsonValueKind.Number =>
                element.GetRawText(),

            _ => null
        };
}

public static class MercadoPagoPaymentStatuses
{
    public static bool IsApproved(
        string? status) =>
        string.Equals(
            status,
            "approved",
            StringComparison.OrdinalIgnoreCase);

    public static bool IsFailure(
        string? status) =>
        status is not null &&
        (
            status.Equals(
                "rejected",
                StringComparison.OrdinalIgnoreCase) ||

            status.Equals(
                "cancelled",
                StringComparison.OrdinalIgnoreCase) ||

            status.Equals(
                "refunded",
                StringComparison.OrdinalIgnoreCase) ||

            status.Equals(
                "charged_back",
                StringComparison.OrdinalIgnoreCase)
        );
}
