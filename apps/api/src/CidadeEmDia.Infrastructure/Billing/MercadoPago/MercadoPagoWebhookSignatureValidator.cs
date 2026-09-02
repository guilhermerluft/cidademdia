using System.Security.Cryptography;
using System.Text;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public sealed class MercadoPagoWebhookSignatureValidator(MercadoPagoOptions options)
{
    public bool IsValid(
        string? xSignature,
        string? xRequestId,
        string? dataId)
    {
        if (!options.HasWebhookSecret ||
            string.IsNullOrWhiteSpace(xSignature))
        {
            return false;
        }

        string? timestamp = null;
        string? receivedHash = null;

        foreach (var item in xSignature.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf('=');

            if (separator <= 0 || separator == item.Length - 1)
                continue;

            var key = item[..separator].Trim();
            var value = item[(separator + 1)..].Trim();

            if (key.Equals("ts", StringComparison.OrdinalIgnoreCase))
                timestamp = value;
            else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
                receivedHash = value;
        }

        if (string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(receivedHash))
        {
            return false;
        }

        var manifest = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(dataId))
            manifest.Append(
                $"id:{dataId.Trim().ToLowerInvariant()};");

        if (!string.IsNullOrWhiteSpace(xRequestId))
            manifest.Append($"request-id:{xRequestId};");

        manifest.Append($"ts:{timestamp};");

        byte[] receivedBytes;

        try
        {
            receivedBytes = Convert.FromHexString(receivedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(options.WebhookSecret));

        var expectedBytes = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(manifest.ToString()));

        return receivedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   receivedBytes,
                   expectedBytes);
    }
}
