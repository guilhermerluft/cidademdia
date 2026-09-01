using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public interface IMercadoPagoClient
{
    Task<MercadoPagoPreapprovalResponse> CreatePreapprovalAsync(
        MercadoPagoCreatePreapprovalRequest request,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPreapprovalResponse> GetPreapprovalAsync(
        string preapprovalId,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPreapprovalResponse> UpdateRecurringAmountAsync(
        string preapprovalId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPreapprovalResponse> UpdateStatusAsync(
        string preapprovalId,
        string status,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoAuthorizedPaymentResponse> GetAuthorizedPaymentAsync(
        string authorizedPaymentId,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPaymentResponse> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default);
}

public sealed record MercadoPagoCreatePreapprovalRequest(
    string Reason,
    string ExternalReference,
    string PayerEmail,
    int Frequency,
    string FrequencyType,
    decimal TransactionAmount,
    string CurrencyId,
    string BackUrl,
    DateTimeOffset? StartDate = null);

public sealed record MercadoPagoAutoRecurringResponse(
    [property: JsonPropertyName("frequency")]
    int Frequency,

    [property: JsonPropertyName("frequency_type")]
    string FrequencyType,

    [property: JsonPropertyName("transaction_amount")]
    decimal TransactionAmount,

    [property: JsonPropertyName("currency_id")]
    string CurrencyId);

public sealed record MercadoPagoPreapprovalResponse(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("external_reference")]
    string? ExternalReference,

    [property: JsonPropertyName("init_point")]
    string? InitPoint,

    [property: JsonPropertyName("payer_id")]
    long? PayerId,

    [property: JsonPropertyName("next_payment_date")]
    DateTimeOffset? NextPaymentDate,

    [property: JsonPropertyName("auto_recurring")]
    MercadoPagoAutoRecurringResponse? AutoRecurring);

public sealed record MercadoPagoAuthorizedPaymentPaymentResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("status_detail")]
    string? StatusDetail);

public sealed record MercadoPagoAuthorizedPaymentResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("preapproval_id")]
    string? PreapprovalId,

    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("debit_date")]
    DateTimeOffset? DebitDate,

    [property: JsonPropertyName("retry_attempt")]
    int? RetryAttempt,

    [property: JsonPropertyName("transaction_amount")]
    [property: JsonNumberHandling(
        JsonNumberHandling.AllowReadingFromString)]
    decimal? TransactionAmount,

    [property: JsonPropertyName("payment")]
    MercadoPagoAuthorizedPaymentPaymentResponse? Payment);

public sealed record MercadoPagoPaymentResponse(
    [property: JsonPropertyName("id")]
    long Id,

    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("status_detail")]
    string? StatusDetail,

    [property: JsonPropertyName("transaction_amount")]
    decimal TransactionAmount,

    [property: JsonPropertyName("currency_id")]
    string CurrencyId,

    [property: JsonPropertyName("date_approved")]
    DateTimeOffset? DateApproved,

    [property: JsonPropertyName("external_reference")]
    string? ExternalReference);

public sealed class MercadoPagoApiException(
    HttpStatusCode statusCode,
    string responseBody)
    : Exception(
        $"Mercado Pago API returned {(int)statusCode} ({statusCode}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}

public sealed class MercadoPagoConfigurationException(string message)
    : Exception(message);

public sealed class MercadoPagoClient(
    HttpClient httpClient,
    MercadoPagoOptions options)
    : IMercadoPagoClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<MercadoPagoPreapprovalResponse> CreatePreapprovalAsync(
        MercadoPagoCreatePreapprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            string.IsNullOrWhiteSpace(request.ExternalReference) ||
            string.IsNullOrWhiteSpace(request.PayerEmail) ||
            request.Frequency <= 0 ||
            string.IsNullOrWhiteSpace(request.FrequencyType) ||
            request.TransactionAmount <= 0 ||
            string.IsNullOrWhiteSpace(request.CurrencyId) ||
            !Uri.TryCreate(request.BackUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                "Invalid Mercado Pago preapproval request.");
        }

        var autoRecurring =
            new Dictionary<string, object>
            {
                ["frequency"] =
                    request.Frequency,

                ["frequency_type"] =
                    request.FrequencyType.Trim(),

                ["transaction_amount"] =
                    request.TransactionAmount,

                ["currency_id"] =
                    request.CurrencyId
                        .Trim()
                        .ToUpperInvariant()
            };

        if (request.StartDate.HasValue)
        {
            autoRecurring["start_date"] =
                request.StartDate.Value;
        }

        var payload = new
        {
            reason =
                request.Reason.Trim(),

            external_reference =
                request.ExternalReference.Trim(),

            payer_email =
                request.PayerEmail.Trim(),

            auto_recurring =
                autoRecurring,

            back_url =
                request.BackUrl,

            status =
                "pending"
        };

        using var message = CreateRequest(
            HttpMethod.Post,
            "/preapproval");

        message.Content = JsonContent.Create(payload);

        return await SendAsync<MercadoPagoPreapprovalResponse>(
            message,
            cancellationToken);
    }

    public async Task<MercadoPagoPreapprovalResponse> GetPreapprovalAsync(
        string preapprovalId,
        CancellationToken cancellationToken = default)
    {
        RequireId(preapprovalId, nameof(preapprovalId));

        using var message = CreateRequest(
            HttpMethod.Get,
            $"/preapproval/{Uri.EscapeDataString(preapprovalId)}");

        return await SendAsync<MercadoPagoPreapprovalResponse>(
            message,
            cancellationToken);
    }

    public async Task<MercadoPagoPreapprovalResponse> UpdateRecurringAmountAsync(
        string preapprovalId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        RequireId(preapprovalId, nameof(preapprovalId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        using var message = CreateRequest(
            HttpMethod.Put,
            $"/preapproval/{Uri.EscapeDataString(preapprovalId)}");

        message.Content = JsonContent.Create(new
        {
            auto_recurring = new
            {
                transaction_amount =
                    amount,

                currency_id =
                    "BRL"
            }
        });

        return await SendAsync<MercadoPagoPreapprovalResponse>(
            message,
            cancellationToken);
    }

    public async Task<MercadoPagoPreapprovalResponse> UpdateStatusAsync(
        string preapprovalId,
        string status,
        CancellationToken cancellationToken = default)
    {
        RequireId(preapprovalId, nameof(preapprovalId));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException(
                "Status is required.",
                nameof(status));

        using var message = CreateRequest(
            HttpMethod.Put,
            $"/preapproval/{Uri.EscapeDataString(preapprovalId)}");

        message.Content = JsonContent.Create(new
        {
            status = status.Trim()
        });

        return await SendAsync<MercadoPagoPreapprovalResponse>(
            message,
            cancellationToken);
    }

    public async Task<MercadoPagoAuthorizedPaymentResponse> GetAuthorizedPaymentAsync(
        string authorizedPaymentId,
        CancellationToken cancellationToken = default)
    {
        RequireId(
            authorizedPaymentId,
            nameof(authorizedPaymentId));

        using var message = CreateRequest(
            HttpMethod.Get,
            $"/authorized_payments/{Uri.EscapeDataString(authorizedPaymentId)}");

        return await SendAsync<MercadoPagoAuthorizedPaymentResponse>(
            message,
            cancellationToken);
    }

    public async Task<MercadoPagoPaymentResponse> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        RequireId(paymentId, nameof(paymentId));

        using var message = CreateRequest(
            HttpMethod.Get,
            $"/v1/payments/{Uri.EscapeDataString(paymentId)}");

        return await SendAsync<MercadoPagoPaymentResponse>(
            message,
            cancellationToken);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path)
    {
        if (!options.HasAccessToken)
        {
            throw new MercadoPagoConfigurationException(
                "MERCADOPAGO_ACCESS_TOKEN is not configured.");
        }

        var baseUrl = options.ApiBaseUrl.TrimEnd('/');

        var request = new HttpRequestMessage(
            method,
            $"{baseUrl}{path}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                options.AccessToken);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        return request;
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MercadoPagoApiException(
                response.StatusCode,
                body);
        }

        var result = JsonSerializer.Deserialize<T>(
            body,
            JsonOptions);

        return result
            ?? throw new MercadoPagoApiException(
                response.StatusCode,
                "Mercado Pago returned an empty or invalid response.");
    }

    private static void RequireId(
        string id,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Provider resource id is required.",
                parameterName);
        }
    }
}
