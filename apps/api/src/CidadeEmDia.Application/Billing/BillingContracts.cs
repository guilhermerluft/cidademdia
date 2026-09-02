namespace CidadeEmDia.Application.Billing;

public sealed record BillingCatalogOffer(
    Guid OfferId,
    Guid PlanVersionId,
    string PlanKey,
    string PlanName,
    string CategoryKey,
    string CategoryName,
    int BillingIntervalMonths,
    long PriceCents,
    long SignupFeeCents,
    long? MarketingReferencePriceCents,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    int Version);

public sealed record BillingEntitlements(
    Guid SubscriptionId,
    Guid PlanVersionId,
    string PlanKey,
    string PlanName,
    string CategoryKey,
    string CategoryName,
    string SubscriptionStatus,
    bool AccessAllowed,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    int UsedPublications,
    DateTimeOffset UsageWindowStart,
    DateTimeOffset UsageWindowEnd,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset? GracePeriodEndsAt,
    bool CancelAtPeriodEnd,
    Guid? PendingPlanVersionId);

public sealed record BillingSubscriptionOperationResult(bool Succeeded, string? ErrorCode = null)
{
    public static BillingSubscriptionOperationResult Success() => new(true);
    public static BillingSubscriptionOperationResult Failure(string errorCode) => new(false, errorCode);
}

public interface IBillingCatalogService
{
    Task<IReadOnlyCollection<BillingCatalogOffer>> ListCurrentOffersAsync(
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);
}

public interface IBillingEntitlementService
{
    Task<BillingEntitlements?> GetForMasterAsync(
        Guid masterUserId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasPaidSignupFeeAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default);
}

public interface IBillingSubscriptionService
{
    Task<BillingSubscriptionOperationResult> CreatePendingAsync(
        Guid masterUserId,
        Guid planVersionId,
        DateTimeOffset startedAt,
        DateTimeOffset currentPeriodEnd,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> ActivateAsync(
        Guid masterUserId,
        bool signupFeePaid,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> MarkPastDueAsync(
        Guid masterUserId,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> ScheduleChangeAsync(
        Guid masterUserId,
        Guid nextPlanVersionId,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> RequestCancellationAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> ApplyRenewalAsync(
        Guid masterUserId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionOperationResult> CancelAsync(
        Guid masterUserId,
        DateTimeOffset canceledAt,
        CancellationToken cancellationToken = default);
}

public sealed record BillingCheckoutResult(
    bool Succeeded,
    string? ErrorCode = null,
    Guid? SubscriptionId = null,
    Guid? PlanVersionId = null,
    string? CheckoutUrl = null,
    string? ProviderStatus = null)
{
    public static BillingCheckoutResult Success(
        Guid subscriptionId,
        Guid planVersionId,
        string checkoutUrl,
        string providerStatus) =>
        new(
            true,
            null,
            subscriptionId,
            planVersionId,
            checkoutUrl,
            providerStatus);

    public static BillingCheckoutResult Failure(
        string errorCode) =>
        new(false, errorCode);
}

public interface IBillingCheckoutService
{
    Task<BillingCheckoutResult> CreateCheckoutAsync(
        Guid masterUserId,
        Guid planVersionId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);
}

public sealed record BillingProviderWebhookResult(
    bool Succeeded,
    bool Duplicate,
    bool Retryable,
    string? ErrorCode = null)
{
    public static BillingProviderWebhookResult Success() =>
        new(true, false, false);

    public static BillingProviderWebhookResult DuplicateEvent() =>
        new(true, true, false);

    public static BillingProviderWebhookResult Retry(
        string errorCode) =>
        new(false, false, true, errorCode);

    public static BillingProviderWebhookResult Failure(
        string errorCode) =>
        new(false, false, false, errorCode);
}

public interface IBillingProviderWebhookService
{
    Task<BillingProviderWebhookResult> ProcessAsync(
        string providerEventId,
        string type,
        string? action,
        string resourceId,
        string requestId,
        bool liveMode,
        string payloadJson,
        DateTimeOffset? receivedAt = null,
        CancellationToken cancellationToken = default);
}


public sealed record BillingSubscriptionManagementResult(
    bool Succeeded,
    string? ErrorCode = null,
    Guid? PlanVersionId = null,
    bool RequiresAuthorization = false,
    string? CheckoutUrl = null,
    DateTimeOffset? EffectiveAt = null,
    string? ProviderStatus = null)
{
    public static BillingSubscriptionManagementResult Success(
        Guid? planVersionId = null,
        bool requiresAuthorization = false,
        string? checkoutUrl = null,
        DateTimeOffset? effectiveAt = null,
        string? providerStatus = null) =>
        new(
            true,
            null,
            planVersionId,
            requiresAuthorization,
            checkoutUrl,
            effectiveAt,
            providerStatus);

    public static BillingSubscriptionManagementResult Failure(
        string errorCode) =>
        new(false, errorCode);
}

public interface IBillingSubscriptionManagementService
{
    Task<BillingSubscriptionManagementResult> SchedulePlanChangeAsync(
        Guid masterUserId,
        Guid targetPlanVersionId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionManagementResult> ReactivateAsync(
        Guid masterUserId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<BillingSubscriptionManagementResult> RequestCancellationAsync(
        Guid masterUserId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);
}
