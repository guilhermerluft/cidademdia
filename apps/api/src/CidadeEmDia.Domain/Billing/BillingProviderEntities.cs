using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Billing;

public static class BillingProviders
{
    public const string MercadoPago = "MERCADOPAGO";
}

public sealed class BillingProviderSubscription : BaseEntity
{
    private BillingProviderSubscription() { }

    public BillingProviderSubscription(
        Guid subscriptionId,
        string provider,
        string providerSubscriptionId,
        string externalReference,
        string checkoutUrl,
        string providerStatus,
        long recurringAmountCents,
        long initialAmountCents,
        bool signupFeeIncluded)
    {
        if (subscriptionId == Guid.Empty)
            throw new ArgumentException("Subscription id is required.", nameof(subscriptionId));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));

        if (string.IsNullOrWhiteSpace(providerSubscriptionId))
            throw new ArgumentException("Provider subscription id is required.", nameof(providerSubscriptionId));

        if (string.IsNullOrWhiteSpace(externalReference))
            throw new ArgumentException("External reference is required.", nameof(externalReference));

        if (string.IsNullOrWhiteSpace(checkoutUrl))
            throw new ArgumentException("Checkout URL is required.", nameof(checkoutUrl));

        if (string.IsNullOrWhiteSpace(providerStatus))
            throw new ArgumentException("Provider status is required.", nameof(providerStatus));

        if (recurringAmountCents <= 0)
            throw new ArgumentOutOfRangeException(nameof(recurringAmountCents));

        if (initialAmountCents < recurringAmountCents)
            throw new ArgumentOutOfRangeException(nameof(initialAmountCents));

        SubscriptionId = subscriptionId;
        Provider = provider.Trim().ToUpperInvariant();
        ProviderSubscriptionId = providerSubscriptionId.Trim();
        ExternalReference = externalReference.Trim();
        CheckoutUrl = checkoutUrl.Trim();
        ProviderStatus = providerStatus.Trim();
        RecurringAmountCents = recurringAmountCents;
        InitialAmountCents = initialAmountCents;
        SignupFeeIncluded = signupFeeIncluded;
    }

    public Guid SubscriptionId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderSubscriptionId { get; private set; } = string.Empty;
    public string ExternalReference { get; private set; } = string.Empty;
    public string CheckoutUrl { get; private set; } = string.Empty;
    public string ProviderStatus { get; private set; } = string.Empty;
    public long RecurringAmountCents { get; private set; }
    public long InitialAmountCents { get; private set; }
    public bool SignupFeeIncluded { get; private set; }
    public DateTimeOffset? FirstApprovedPaymentAt { get; private set; }
    public DateTimeOffset? RecurringAmountSynchronizedAt { get; private set; }

    public Subscription Subscription { get; private set; } = null!;

    public bool RequiresRecurringAmountSynchronization =>
        SignupFeeIncluded &&
        InitialAmountCents != RecurringAmountCents &&
        RecurringAmountSynchronizedAt is null;

    public bool IsCurrent { get; private set; } = true;
    public DateTimeOffset? EndedAt { get; private set; }

    public Guid? TargetPlanVersionId { get; private set; }
    public DateTimeOffset? ScheduledFor { get; private set; }

    public bool IsScheduledReplacement =>
        !IsCurrent &&
        EndedAt is null &&
        TargetPlanVersionId.HasValue &&
        ScheduledFor.HasValue;


    public void UpdateProviderState(string providerStatus, string? checkoutUrl = null)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
            throw new ArgumentException("Provider status is required.", nameof(providerStatus));

        ProviderStatus = providerStatus.Trim();

        if (!string.IsNullOrWhiteSpace(checkoutUrl))
            CheckoutUrl = checkoutUrl.Trim();

        Touch();
    }

    public void UpdateRecurringAmount(
        long recurringAmountCents,
        string providerStatus)
    {
        if (recurringAmountCents <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(recurringAmountCents));

        if (string.IsNullOrWhiteSpace(providerStatus))
            throw new ArgumentException(
                "Provider status is required.",
                nameof(providerStatus));

        RecurringAmountCents =
            recurringAmountCents;

        ProviderStatus =
            providerStatus.Trim();

        Touch();
    }


    public void MarkFirstApprovedPayment(DateTimeOffset approvedAt)
    {
        FirstApprovedPaymentAt ??= approvedAt;
        Touch();
    }

    public void MarkRecurringAmountSynchronized(DateTimeOffset synchronizedAt)
    {
        RecurringAmountSynchronizedAt ??= synchronizedAt;
        Touch();
    }

    public void MarkScheduledReplacement(
        Guid targetPlanVersionId,
        DateTimeOffset scheduledFor)
    {
        if (targetPlanVersionId == Guid.Empty)
            throw new ArgumentException(
                "Target plan version id is required.",
                nameof(targetPlanVersionId));

        if (!IsCurrent)
            throw new InvalidOperationException(
                "Only a new/current binding can be scheduled.");

        IsCurrent = false;
        EndedAt = null;

        TargetPlanVersionId =
            targetPlanVersionId;

        ScheduledFor =
            scheduledFor;

        Touch();
    }

    public void PromoteScheduledReplacement()
    {
        if (!IsScheduledReplacement)
            throw new InvalidOperationException(
                "Binding is not a scheduled replacement.");

        IsCurrent = true;

        Touch();
    }

    public void AbandonScheduledReplacement(
        DateTimeOffset endedAt)
    {
        if (!IsScheduledReplacement)
            return;

        EndedAt =
            endedAt;

        Touch();
    }

    public void MarkReplaced(
        DateTimeOffset replacedAt)
    {
        if (!IsCurrent)
            return;

        IsCurrent = false;
        EndedAt = replacedAt;
        Touch();
    }

}

public sealed class BillingPayment : BaseEntity
{
    private BillingPayment() { }

    public BillingPayment(
        Guid subscriptionId,
        string provider,
        string providerPaymentId,
        string? providerAuthorizedPaymentId,
        long amountCents,
        string currency,
        string status,
        string? statusDetail,
        DateTimeOffset? approvedAt)
    {
        if (subscriptionId == Guid.Empty)
            throw new ArgumentException("Subscription id is required.", nameof(subscriptionId));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));

        if (string.IsNullOrWhiteSpace(providerPaymentId))
            throw new ArgumentException("Provider payment id is required.", nameof(providerPaymentId));

        if (amountCents < 0)
            throw new ArgumentOutOfRangeException(nameof(amountCents));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        SubscriptionId = subscriptionId;
        Provider = provider.Trim().ToUpperInvariant();
        ProviderPaymentId = providerPaymentId.Trim();
        ProviderAuthorizedPaymentId = string.IsNullOrWhiteSpace(providerAuthorizedPaymentId)
            ? null
            : providerAuthorizedPaymentId.Trim();
        AmountCents = amountCents;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status.Trim();
        StatusDetail = string.IsNullOrWhiteSpace(statusDetail)
            ? null
            : statusDetail.Trim();
        ApprovedAt = approvedAt;
    }

    public Guid SubscriptionId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderPaymentId { get; private set; } = string.Empty;
    public string? ProviderAuthorizedPaymentId { get; private set; }
    public long AmountCents { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? StatusDetail { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    public Subscription Subscription { get; private set; } = null!;

    public void UpdateFromProvider(
        long amountCents,
        string currency,
        string status,
        string? statusDetail,
        DateTimeOffset? approvedAt,
        string? providerAuthorizedPaymentId = null)
    {
        if (amountCents < 0)
            throw new ArgumentOutOfRangeException(nameof(amountCents));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        AmountCents = amountCents;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status.Trim();
        StatusDetail = string.IsNullOrWhiteSpace(statusDetail)
            ? null
            : statusDetail.Trim();

        if (approvedAt.HasValue)
            ApprovedAt = approvedAt;

        if (!string.IsNullOrWhiteSpace(providerAuthorizedPaymentId))
            ProviderAuthorizedPaymentId = providerAuthorizedPaymentId.Trim();

        Touch();
    }
}

public sealed class BillingPaymentEvent : BaseEntity
{
    private BillingPaymentEvent() { }

    public BillingPaymentEvent(
        string provider,
        string providerEventId,
        string type,
        string? action,
        string resourceId,
        string requestId,
        bool liveMode,
        string payloadJson,
        DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));

        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new ArgumentException("Provider event id is required.", nameof(providerEventId));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Event type is required.", nameof(type));

        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource id is required.", nameof(resourceId));

        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request id is required.", nameof(requestId));

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload is required.", nameof(payloadJson));

        Provider = provider.Trim().ToUpperInvariant();
        ProviderEventId = providerEventId.Trim();
        Type = type.Trim();
        Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
        ResourceId = resourceId.Trim();
        RequestId = requestId.Trim();
        LiveMode = liveMode;
        PayloadJson = payloadJson;
        ReceivedAt = receivedAt;
    }

    public string Provider { get; private set; } = string.Empty;
    public string ProviderEventId { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string? Action { get; private set; }
    public string ResourceId { get; private set; } = string.Empty;
    public string RequestId { get; private set; } = string.Empty;
    public bool LiveMode { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? ProcessingError { get; private set; }

    public bool IsProcessed => ProcessedAt.HasValue;

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        ProcessingError = null;
        Touch();
    }

    public void MarkFailed(string error)
    {
        ProcessingError = string.IsNullOrWhiteSpace(error)
            ? "webhook_processing_failed"
            : error.Trim();

        Touch();
    }
}
