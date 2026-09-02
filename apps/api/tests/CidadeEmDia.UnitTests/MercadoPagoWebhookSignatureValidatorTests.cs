using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoWebhookSignatureValidatorTests
{
    private const string Secret = "top-secret";

    private static MercadoPagoWebhookSignatureValidator CreateValidator(
        string secret = Secret)
    {
        var options = new MercadoPagoOptions(
            AccessToken: string.Empty,
            WebhookSecret: secret,
            ApiBaseUrl: MercadoPagoOptions.DefaultApiBaseUrl,
            BackUrl: "https://example.com/billing/return");

        return new MercadoPagoWebhookSignatureValidator(options);
    }

    [Fact]
    public void IsValid_AcceptsOfficialManifestHmac()
    {
        var validator = CreateValidator();

        var signature =
            "ts=1704908010," +
            "v1=278c6aac1501d594fd331471360244fe07fb48ceed7b8123e643468bc0d26500";

        var valid = validator.IsValid(
            signature,
            "req-abc",
            "123456");

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_AcceptsManifestWithoutRequestId()
    {
        var validator = CreateValidator();

        var signature =
            "ts=1704908010," +
            "v1=bbea65be2bbee6d627f368bc4f1e6ef35fbdbe895f9a285400150f3e904d9348";

        var valid = validator.IsValid(
            signature,
            null,
            "123456");

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_LowercasesAlphanumericDataId()
    {
        var validator = CreateValidator();

        var signature =
            "ts=1704908010," +
            "v1=16dcd99e026b0b4ce8c21bb20271d67ad6e9395cf5965a78d1eb7a7d2f5fb35f";

        var valid = validator.IsValid(
            signature,
            "req-abc",
            "ABC123");

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_RejectsTamperedDataId()
    {
        var validator = CreateValidator();

        var signature =
            "ts=1704908010," +
            "v1=278c6aac1501d594fd331471360244fe07fb48ceed7b8123e643468bc0d26500";

        var valid = validator.IsValid(
            signature,
            "req-abc",
            "654321");

        Assert.False(valid);
    }

    [Fact]
    public void IsValid_RejectsMissingSecret()
    {
        var validator = CreateValidator(string.Empty);

        Assert.False(
            validator.IsValid(
                "ts=1704908010,v1=abc",
                "req-abc",
                "123456"));
    }

    [Fact]
    public void IsValid_RejectsMalformedSignature()
    {
        var validator = CreateValidator();

        Assert.False(
            validator.IsValid(
                "invalid-signature",
                "req-abc",
                "123456"));
    }

    [Fact]
    public void IsValid_RejectsInvalidHexHash()
    {
        var validator = CreateValidator();

        Assert.False(
            validator.IsValid(
                "ts=1704908010,v1=not-hex",
                "req-abc",
                "123456"));
    }
}
