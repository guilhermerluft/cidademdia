using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoExternalReferenceTests
{
    [Fact]
    public void Create_ContainsSubscriptionAndFitsPersistedLimit()
    {
        var subscriptionId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        var reference =
            MercadoPagoExternalReference.Create(
                subscriptionId);

        Assert.StartsWith(
            "ced:11111111222233334444555555555555:",
            reference);

        Assert.True(
            reference.Length <= 160);
    }

    [Fact]
    public void Create_IsUniquePerProviderBinding()
    {
        var subscriptionId =
            Guid.NewGuid();

        Assert.NotEqual(
            MercadoPagoExternalReference.Create(
                subscriptionId),
            MercadoPagoExternalReference.Create(
                subscriptionId));
    }
}
