using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoWebhookEnvelopeTests
{
    [Fact]
    public void TryParse_ReadsStandardPaymentNotification()
    {
        var parsed =
            MercadoPagoWebhookEnvelope.TryParse(
                """
                {
                  "id": 12345,
                  "live_mode": false,
                  "type": "payment",
                  "action": "payment.updated",
                  "data": {
                    "id": "999999999"
                  }
                }
                """,
                out var result);

        Assert.True(parsed);
        Assert.NotNull(result);

        Assert.Equal(
            "12345",
            result!.EventId);

        Assert.Equal(
            "payment",
            result.Type);

        Assert.Equal(
            "payment.updated",
            result.Action);

        Assert.Equal(
            "999999999",
            result.ResourceId);

        Assert.False(
            result.LiveMode);
    }

    [Fact]
    public void TryParse_RejectsMissingResourceId()
    {
        var parsed =
            MercadoPagoWebhookEnvelope.TryParse(
                """
                {
                  "id": 12345,
                  "type": "payment",
                  "data": {}
                }
                """,
                out _);

        Assert.False(parsed);
    }

    [Fact]
    public void MatchesSignatureDataId_IsCaseInsensitive()
    {
        var envelope =
            new MercadoPagoWebhookEnvelope(
                "evt-1",
                "subscription_preapproval",
                "updated",
                "ABC123",
                false);

        Assert.True(
            envelope.MatchesSignatureDataId(
                "abc123"));

        Assert.False(
            envelope.MatchesSignatureDataId(
                "other"));
    }

    [Fact]
    public void PaymentStatusClassification_IsSafe()
    {
        Assert.True(
            MercadoPagoPaymentStatuses.IsApproved(
                "approved"));

        Assert.True(
            MercadoPagoPaymentStatuses.IsFailure(
                "rejected"));

        Assert.True(
            MercadoPagoPaymentStatuses.IsFailure(
                "charged_back"));

        Assert.False(
            MercadoPagoPaymentStatuses.IsFailure(
                "pending"));
    }
}
