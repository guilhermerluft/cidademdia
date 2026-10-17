namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public static class MercadoPagoSignupFeeExternalReference
{
    private const string Prefix = "ced-signup";

    public static string Create(Guid masterUserId, Guid planVersionId)
    {
        if (masterUserId == Guid.Empty)
            throw new ArgumentException("Master user id is required.", nameof(masterUserId));

        if (planVersionId == Guid.Empty)
            throw new ArgumentException("Plan version id is required.", nameof(planVersionId));

        return $"{Prefix}:{masterUserId:N}:{planVersionId:N}";
    }

    public static bool TryParse(
        string? externalReference,
        out Guid masterUserId,
        out Guid planVersionId)
    {
        masterUserId = Guid.Empty;
        planVersionId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(externalReference))
            return false;

        var parts = externalReference.Split(':');
        return parts.Length == 3 &&
            string.Equals(parts[0], Prefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(parts[1], "N", out masterUserId) &&
            Guid.TryParseExact(parts[2], "N", out planVersionId);
    }
}
