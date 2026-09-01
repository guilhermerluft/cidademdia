using System.Net;
using System.Text;
using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoClientTests
{
    [Fact]
    public async Task CreatePreapproval_SendsExpectedRequestAndParsesResponse()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;

        var handler = new StubHandler(async request =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();

            return Json(
                HttpStatusCode.Created,
                """
                {
                  "id": "preapproval-123",
                  "status": "pending",
                  "external_reference": "subscription-abc",
                  "init_point": "https://checkout.example/init",
                  "payer_id": 123456,
                  "next_payment_date": null,
                  "auto_recurring": {
                    "frequency": 3,
                    "frequency_type": "months",
                    "transaction_amount": 850.00,
                    "currency_id": "BRL"
                  }
                }
                """);
        });

        var client = CreateClient(handler);

        var result = await client.CreatePreapprovalAsync(
            new MercadoPagoCreatePreapprovalRequest(
                "CidadeEmDia Master 10 Bronze",
                "subscription-abc",
                "master@example.com",
                3,
                "months",
                850.00m,
                "BRL",
                "https://homolog.example/billing/return"));

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal(
            "https://api.mercadopago.test/preapproval",
            captured.RequestUri!.ToString());

        Assert.Equal(
            "Bearer",
            captured.Headers.Authorization!.Scheme);

        Assert.Equal(
            "test-access-token",
            captured.Headers.Authorization.Parameter);

        Assert.NotNull(capturedBody);
        Assert.Contains(
            "\"external_reference\":\"subscription-abc\"",
            capturedBody);

        Assert.Contains(
            "\"payer_email\":\"master@example.com\"",
            capturedBody);

        Assert.Contains(
            "\"frequency\":3",
            capturedBody);

        Assert.Contains(
            "\"frequency_type\":\"months\"",
            capturedBody);

        Assert.Contains(
            "\"transaction_amount\":850.00",
            capturedBody);

        Assert.Contains(
            "\"currency_id\":\"BRL\"",
            capturedBody);

        Assert.Contains(
            "\"status\":\"pending\"",
            capturedBody);

        Assert.DoesNotContain(
            "\"start_date\"",
            capturedBody);

        Assert.Equal("preapproval-123", result.Id);
        Assert.Equal("pending", result.Status);
        Assert.Equal(
            "https://checkout.example/init",
            result.InitPoint);

        Assert.NotNull(result.AutoRecurring);
        Assert.Equal(
            850.00m,
            result.AutoRecurring!.TransactionAmount);
    }

    [Fact]
    public async Task CreatePreapproval_WithStartDate_SendsStartDate()
    {
        string? capturedBody =
            null;

        var handler =
            new StubHandler(
                async request =>
                {
                    capturedBody =
                        await request.Content!
                            .ReadAsStringAsync();

                    return Json(
                        HttpStatusCode.Created,
                        """
                        {
                          "id": "pre-future",
                          "status": "pending",
                          "external_reference": "subscription-future",
                          "init_point": "https://checkout.example/future",
                          "payer_id": null,
                          "next_payment_date": null,
                          "auto_recurring": {
                            "frequency": 3,
                            "frequency_type": "months",
                            "transaction_amount": 550.00,
                            "currency_id": "BRL"
                          }
                        }
                        """);
                });

        var client =
            CreateClient(handler);

        var startDate =
            new DateTimeOffset(
                2026,
                12,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        await client.CreatePreapprovalAsync(
            new MercadoPagoCreatePreapprovalRequest(
                "CidadeEmDia Bronze",
                "subscription-future",
                "master@example.com",
                3,
                "months",
                550.00m,
                "BRL",
                "https://homolog.example/billing/return",
                startDate));

        Assert.NotNull(
            capturedBody);

        Assert.Contains(
            "\"start_date\":\"2026-12-01T00:00:00+00:00\"",
            capturedBody);
    }

    [Fact]
    public async Task GetPayment_UsesPaymentEndpointAndParsesApprovedPayment()
    {
        HttpRequestMessage? captured = null;

        var handler = new StubHandler(request =>
        {
            captured = request;

            return Task.FromResult(
                Json(
                    HttpStatusCode.OK,
                    """
                    {
                      "id": 987654,
                      "status": "approved",
                      "status_detail": "accredited",
                      "transaction_amount": 1100.00,
                      "currency_id": "BRL",
                      "date_approved": "2026-09-01T18:00:00Z",
                      "external_reference": "subscription-abc"
                    }
                    """));
        });

        var client = CreateClient(handler);

        var result = await client.GetPaymentAsync("987654");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal(
            "https://api.mercadopago.test/v1/payments/987654",
            captured.RequestUri!.ToString());

        Assert.Equal(987654, result.Id);
        Assert.Equal("approved", result.Status);
        Assert.Equal("accredited", result.StatusDetail);
        Assert.Equal(1100.00m, result.TransactionAmount);
        Assert.Equal("BRL", result.CurrencyId);
    }

    [Fact]
    public async Task UpdateRecurringAmount_SendsAmountAndCurrency()
    {
        string? capturedBody = null;

        var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(
                "https://api.mercadopago.test/preapproval/pre-1",
                request.RequestUri!.ToString());

            capturedBody = await request.Content!.ReadAsStringAsync();

            return Json(
                HttpStatusCode.OK,
                """
                {
                  "id": "pre-1",
                  "status": "authorized",
                  "external_reference": "subscription-1",
                  "init_point": null,
                  "payer_id": 1,
                  "next_payment_date": null,
                  "auto_recurring": {
                    "frequency": 1,
                    "frequency_type": "months",
                    "transaction_amount": 800.00,
                    "currency_id": "BRL"
                  }
                }
                """);
        });

        var client = CreateClient(handler);

        var result = await client.UpdateRecurringAmountAsync(
            "pre-1",
            800.00m);

        Assert.NotNull(capturedBody);
        Assert.Contains(
            "\"transaction_amount\":800.00",
            capturedBody);

        Assert.Contains(
            "\"currency_id\":\"BRL\"",
            capturedBody);

        Assert.Equal("pre-1", result.Id);
    }

    [Fact]
    public async Task UpdateStatus_SendsCanceledStatus()
    {
        HttpRequestMessage? captured =
            null;

        string? capturedBody =
            null;

        var handler =
            new StubHandler(
                async request =>
                {
                    captured =
                        request;

                    capturedBody =
                        await request.Content!
                            .ReadAsStringAsync();

                    return Json(
                        HttpStatusCode.OK,
                        """
                        {
                          "id": "pre-cancel",
                          "status": "canceled",
                          "external_reference": "ced:test",
                          "init_point": "https://checkout.example/cancel",
                          "payer_id": null,
                          "next_payment_date": null,
                          "auto_recurring": {
                            "frequency": 1,
                            "frequency_type": "months",
                            "transaction_amount": 200.00,
                            "currency_id": "BRL"
                          }
                        }
                        """);
                });

        var client =
            CreateClient(handler);

        var result =
            await client.UpdateStatusAsync(
                "pre-cancel",
                "canceled");

        Assert.NotNull(
            captured);

        Assert.Equal(
            HttpMethod.Put,
            captured!.Method);

        Assert.Equal(
            "/preapproval/pre-cancel",
            captured.RequestUri!.AbsolutePath);

        Assert.Contains(
            "\"status\":\"canceled\"",
            capturedBody);

        Assert.Equal(
            "canceled",
            result.Status);
    }

    [Fact]
    public async Task ApiFailure_ThrowsProviderExceptionWithResponseBody()
    {
        var handler = new StubHandler(_ =>
            Task.FromResult(
                Json(
                    HttpStatusCode.BadRequest,
                    """
                    {
                      "message": "invalid request"
                    }
                    """)));

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<
            MercadoPagoApiException>(
            () => client.GetPreapprovalAsync("pre-invalid"));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            exception.StatusCode);

        Assert.Contains(
            "invalid request",
            exception.ResponseBody);
    }

    [Fact]
    public async Task MissingAccessToken_DoesNotCallProvider()
    {
        var called = false;

        var handler = new StubHandler(_ =>
        {
            called = true;

            return Task.FromResult(
                Json(HttpStatusCode.OK, "{}"));
        });

        var httpClient = new HttpClient(handler);

        var options = new MercadoPagoOptions(
            AccessToken: string.Empty,
            WebhookSecret: "secret",
            ApiBaseUrl: "https://api.mercadopago.test",
            BackUrl: "https://homolog.example/billing/return");

        var client = new MercadoPagoClient(
            httpClient,
            options);

        await Assert.ThrowsAsync<
            MercadoPagoConfigurationException>(
            () => client.GetPreapprovalAsync("pre-1"));

        Assert.False(called);
    }

    [Fact]
    public async Task GetAuthorizedPayment_ParsesInvoicePaymentShape()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(
                "https://api.mercadopago.test/authorized_payments/6114264375",
                request.RequestUri!.ToString());

            return Task.FromResult(
                Json(
                    HttpStatusCode.OK,
                    """
                    {
                      "id": 6114264375,
                      "preapproval_id": "pre-123",
                      "transaction_amount": "24.50",
                      "debit_date": "2026-09-01T18:00:00Z",
                      "retry_attempt": 0,
                      "status": "processed",
                      "payment": {
                        "id": 19951521071,
                        "status": "approved",
                        "status_detail": "accredited"
                      }
                    }
                    """));
        });

        var client =
            CreateClient(handler);

        var result =
            await client.GetAuthorizedPaymentAsync(
                "6114264375");

        Assert.Equal(
            6114264375,
            result.Id);

        Assert.Equal(
            "pre-123",
            result.PreapprovalId);

        Assert.Equal(
            24.50m,
            result.TransactionAmount);

        Assert.NotNull(
            result.Payment);

        Assert.Equal(
            19951521071,
            result.Payment!.Id);

        Assert.Equal(
            "approved",
            result.Payment.Status);
    }

    private static MercadoPagoClient CreateClient(
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);

        var options = new MercadoPagoOptions(
            AccessToken: "test-access-token",
            WebhookSecret: "test-secret",
            ApiBaseUrl: "https://api.mercadopago.test",
            BackUrl: "https://homolog.example/billing/return");

        return new MercadoPagoClient(
            httpClient,
            options);
    }

    private static HttpResponseMessage Json(
        HttpStatusCode statusCode,
        string body) =>
        new(statusCode)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request);
    }
}
