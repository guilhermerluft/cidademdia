namespace CidadeEmDia.Infrastructure.Billing;

internal sealed record BillingPlanDefinition(string Key, string Name);
internal sealed record BillingCategoryDefinition(string Key, string Name, int BillingIntervalMonths);
internal sealed record BillingOfferDefinition(
    string PlanKey,
    string CategoryKey,
    long PriceCents,
    long SignupFeeCents,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    long? MarketingReferencePriceCents = null);

internal static class BillingSeedCatalog
{
    public static readonly DateTimeOffset EffectiveFrom = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<BillingPlanDefinition> Plans =
    [
        new("INDIVIDUAL", "Individual"),
        new("MASTER_5", "Master 5"),
        new("MASTER_10", "Master 10")
    ];

    public static readonly IReadOnlyList<BillingCategoryDefinition> Categories =
    [
        new("BASICO", "Básico", 1),
        new("BRONZE", "Bronze", 3),
        new("PRATA", "Prata", 6),
        new("OURO", "Ouro", 12)
    ];

    public static readonly IReadOnlyList<BillingOfferDefinition> Offers =
    [
        new("INDIVIDUAL", "BASICO", 20_000, 30_000, 2, 10),
        new("INDIVIDUAL", "BRONZE", 55_000, 30_000, 2, 10),
        new("INDIVIDUAL", "PRATA", 110_000, 30_000, 2, 10),
        new("INDIVIDUAL", "OURO", 220_000, 0, 2, 10, 270_000),

        new("MASTER_5", "BASICO", 40_000, 30_000, 5, 20),
        new("MASTER_5", "BRONZE", 147_200, 30_000, 5, 20),
        new("MASTER_5", "PRATA", 220_000, 30_000, 5, 20),
        new("MASTER_5", "OURO", 396_000, 0, 5, 20, 480_000),

        new("MASTER_10", "BASICO", 80_000, 30_000, 10, 30),
        new("MASTER_10", "BRONZE", 404_000, 30_000, 10, 30),
        new("MASTER_10", "PRATA", 604_400, 30_000, 10, 30),
        new("MASTER_10", "OURO", 836_000, 0, 10, 30, 960_000)
    ];

    public static string OfferKey(string planKey, string categoryKey) => $"{planKey}_{categoryKey}";
}
