namespace CidadeEmDia.Application.Billing;

public sealed record BillingPublicationUsageResult(
    bool Succeeded,
    string? ErrorCode = null,
    Guid? SubscriptionId = null,
    int? UsedPublications = null,
    int? MonthlyPublicationLimit = null,
    DateTimeOffset? UsageWindowStart = null,
    DateTimeOffset? UsageWindowEnd = null)
{
    public static BillingPublicationUsageResult Success(
        Guid subscriptionId,
        int usedPublications,
        int monthlyPublicationLimit,
        DateTimeOffset usageWindowStart,
        DateTimeOffset usageWindowEnd) =>
        new(
            true,
            null,
            subscriptionId,
            usedPublications,
            monthlyPublicationLimit,
            usageWindowStart,
            usageWindowEnd);

    public static BillingPublicationUsageResult Failure(
        string errorCode) =>
        new(false, errorCode);
}

/// <summary>
/// Tracks publication consumption in the current scoped DbContext without
/// committing it. The caller must persist the functional change and the usage
/// counter in the same transaction.
/// </summary>
public interface IBillingPublicationUsageTracker
{
    Task<BillingPublicationUsageResult> TrackAsync(
        Guid masterUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
