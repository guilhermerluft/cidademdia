using CidadeEmDia.Application.Billing;
using CidadeEmDia.Infrastructure.Billing.MercadoPago;

namespace CidadeEmDia.Api.Endpoints;

public static class MercadoPagoWebhookEndpoints
{
    public static RouteGroupBuilder MapMercadoPagoWebhookEndpoints(
        this RouteGroupBuilder api)
    {
        api.MapPost(
            "/webhooks/mercadopago",
            HandleAsync);

        return api;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        MercadoPagoWebhookSignatureValidator validator,
        IBillingProviderWebhookService webhookService,
        CancellationToken cancellationToken)
    {
        var xSignature =
            httpContext.Request.Headers[
                "x-signature"
            ].ToString();

        var xRequestId =
            httpContext.Request.Headers[
                "x-request-id"
            ].ToString();

        var queryDataId =
            httpContext.Request.Query[
                "data.id"
            ].ToString();

        if (!validator.IsValid(
                xSignature,
                string.IsNullOrWhiteSpace(
                    xRequestId)
                    ? null
                    : xRequestId,
                string.IsNullOrWhiteSpace(
                    queryDataId)
                    ? null
                    : queryDataId))
        {
            return Results.Unauthorized();
        }

        using var reader =
            new StreamReader(
                httpContext.Request.Body);

        var payload =
            await reader.ReadToEndAsync(
                cancellationToken);

        if (!MercadoPagoWebhookEnvelope.TryParse(
                payload,
                out var envelope) ||
            envelope is null)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "invalid_webhook_payload"
                });
        }

        if (!envelope.MatchesSignatureDataId(
                queryDataId))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "webhook_resource_mismatch"
                });
        }

        var auditRequestId =
            string.IsNullOrWhiteSpace(
                xRequestId)
                ? $"missing:{envelope.EventId}"
                : xRequestId;

        var result =
            await webhookService.ProcessAsync(
                envelope.EventId,
                envelope.Type,
                envelope.Action,
                envelope.ResourceId,
                auditRequestId,
                envelope.LiveMode,
                payload,
                cancellationToken:
                    cancellationToken);

        if (result.Succeeded)
        {
            return Results.Ok(
                new
                {
                    received = true,
                    duplicate =
                        result.Duplicate
                });
        }

        if (result.Retryable)
        {
            return Results.Json(
                new
                {
                    error =
                        result.ErrorCode
                },
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
        }

        return Results.BadRequest(
            new
            {
                error =
                    result.ErrorCode ??
                    "webhook_failed"
            });
    }
}
