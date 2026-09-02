namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public static class MercadoPagoExternalReference
{
    public static string Create(
        Guid subscriptionId)
    {
        if (subscriptionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Subscription id is required.",
                nameof(subscriptionId));
        }

        return
            $"ced:{subscriptionId:N}:{Guid.NewGuid():N}";
    }
}
