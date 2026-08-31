using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Domain.Billing;

public enum SubscriptionStatus
{
    Pending = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4
}

public sealed class Plan : BaseEntity
{
    private Plan() { }

    public Plan(string key, string name)
    {
        Key = NormalizeKey(key);
        Name = RequireText(name, nameof(name), 80);
    }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public ICollection<PlanOffer> Offers { get; private set; } = new List<PlanOffer>();

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private static string NormalizeKey(string value) => RequireText(value, nameof(value), 40).ToUpperInvariant();
    private static string RequireText(string value, string parameter, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException($"{parameter} is required and must have at most {maxLength} characters.", parameter);
        return value;
    }
}

public sealed class PlanCategory : BaseEntity
{
    private PlanCategory() { }

    public PlanCategory(string key, string name, int billingIntervalMonths)
    {
        if (billingIntervalMonths is not (1 or 3 or 6 or 12))
            throw new ArgumentOutOfRangeException(nameof(billingIntervalMonths));

        Key = NormalizeKey(key);
        Name = RequireText(name, nameof(name), 80);
        BillingIntervalMonths = billingIntervalMonths;
    }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int BillingIntervalMonths { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ICollection<PlanOffer> Offers { get; private set; } = new List<PlanOffer>();

    private static string NormalizeKey(string value) => RequireText(value, nameof(value), 40).ToUpperInvariant();
    private static string RequireText(string value, string parameter, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException($"{parameter} is required and must have at most {maxLength} characters.", parameter);
        return value;
    }
}

public sealed class PlanOffer : BaseEntity
{
    private PlanOffer() { }

    public PlanOffer(Guid planId, Guid categoryId, string key)
    {
        if (planId == Guid.Empty) throw new ArgumentException("Plan id is required.", nameof(planId));
        if (categoryId == Guid.Empty) throw new ArgumentException("Category id is required.", nameof(categoryId));

        PlanId = planId;
        CategoryId = categoryId;
        Key = (key?.Trim() ?? string.Empty).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(Key) || Key.Length > 80)
            throw new ArgumentException("Offer key is required and must have at most 80 characters.", nameof(key));
    }

    public Guid PlanId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Plan Plan { get; private set; } = null!;
    public PlanCategory Category { get; private set; } = null!;
    public ICollection<PlanVersion> Versions { get; private set; } = new List<PlanVersion>();
}

public sealed class PlanVersion : BaseEntity
{
    private PlanVersion() { }

    public PlanVersion(
        Guid planOfferId,
        int version,
        long priceCents,
        long signupFeeCents,
        int subaccountLimit,
        int monthlyPublicationLimit,
        DateTimeOffset effectiveFrom,
        long? marketingReferencePriceCents = null)
    {
        if (planOfferId == Guid.Empty) throw new ArgumentException("Plan offer id is required.", nameof(planOfferId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (priceCents < 0) throw new ArgumentOutOfRangeException(nameof(priceCents));
        if (signupFeeCents < 0) throw new ArgumentOutOfRangeException(nameof(signupFeeCents));
        if (subaccountLimit < 0) throw new ArgumentOutOfRangeException(nameof(subaccountLimit));
        if (monthlyPublicationLimit < 0) throw new ArgumentOutOfRangeException(nameof(monthlyPublicationLimit));
        if (marketingReferencePriceCents is < 0) throw new ArgumentOutOfRangeException(nameof(marketingReferencePriceCents));

        PlanOfferId = planOfferId;
        Version = version;
        PriceCents = priceCents;
        SignupFeeCents = signupFeeCents;
        SubaccountLimit = subaccountLimit;
        MonthlyPublicationLimit = monthlyPublicationLimit;
        EffectiveFrom = effectiveFrom;
        MarketingReferencePriceCents = marketingReferencePriceCents;
    }

    public Guid PlanOfferId { get; private set; }
    public int Version { get; private set; }
    public long PriceCents { get; private set; }
    public long SignupFeeCents { get; private set; }
    public int SubaccountLimit { get; private set; }
    public int MonthlyPublicationLimit { get; private set; }
    public long? MarketingReferencePriceCents { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public PlanOffer PlanOffer { get; private set; } = null!;

    public bool IsEffectiveAt(DateTimeOffset at) => EffectiveFrom <= at && (EffectiveTo == null || at < EffectiveTo.Value);

    public void Close(DateTimeOffset effectiveTo)
    {
        if (effectiveTo <= EffectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(effectiveTo));
        EffectiveTo = effectiveTo;
        Touch();
    }
}

public sealed class BillingCustomer : BaseEntity
{
    private BillingCustomer() { }

    public BillingCustomer(Guid masterUserId)
    {
        if (masterUserId == Guid.Empty) throw new ArgumentException("Master user id is required.", nameof(masterUserId));
        MasterUserId = masterUserId;
    }

    public Guid MasterUserId { get; private set; }
    public DateTimeOffset? SignupFeePaidAt { get; private set; }
    public User MasterUser { get; private set; } = null!;

    public bool HasPaidSignupFee => SignupFeePaidAt.HasValue;

    public void MarkSignupFeePaid(DateTimeOffset paidAt)
    {
        SignupFeePaidAt ??= paidAt;
        Touch();
    }
}

public sealed class Subscription : BaseEntity
{
    public const int PastDueGraceDays = 2;

    private Subscription() { }

    public Subscription(Guid masterUserId, Guid planVersionId, DateTimeOffset startedAt, DateTimeOffset currentPeriodEnd)
    {
        if (masterUserId == Guid.Empty) throw new ArgumentException("Master user id is required.", nameof(masterUserId));
        if (planVersionId == Guid.Empty) throw new ArgumentException("Plan version id is required.", nameof(planVersionId));
        if (currentPeriodEnd <= startedAt) throw new ArgumentOutOfRangeException(nameof(currentPeriodEnd));

        MasterUserId = masterUserId;
        PlanVersionId = planVersionId;
        StartedAt = startedAt;
        CurrentPeriodStart = startedAt;
        CurrentPeriodEnd = currentPeriodEnd;
        Status = SubscriptionStatus.Pending;
    }

    public Guid MasterUserId { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public Guid? PendingPlanVersionId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset? PastDueAt { get; private set; }
    public DateTimeOffset? GracePeriodEndsAt { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public User MasterUser { get; private set; } = null!;
    public PlanVersion PlanVersion { get; private set; } = null!;
    public PlanVersion? PendingPlanVersion { get; private set; }
    public ICollection<UsageCounter> UsageCounters { get; private set; } = new List<UsageCounter>();

    public void Activate()
    {
        Status = SubscriptionStatus.Active;
        PastDueAt = null;
        GracePeriodEndsAt = null;
        Touch();
    }

    public void MarkPastDue(DateTimeOffset failedAt)
    {
        Status = SubscriptionStatus.PastDue;
        PastDueAt = failedAt;
        GracePeriodEndsAt = failedAt.AddDays(PastDueGraceDays);
        Touch();
    }

    public bool AllowsAccess(DateTimeOffset at) =>
        Status == SubscriptionStatus.Active ||
        (Status == SubscriptionStatus.PastDue && GracePeriodEndsAt.HasValue && at < GracePeriodEndsAt.Value);

    public void ScheduleChange(Guid planVersionId)
    {
        if (planVersionId == Guid.Empty) throw new ArgumentException("Plan version id is required.", nameof(planVersionId));
        PendingPlanVersionId = planVersionId;
        Touch();
    }

    public void RequestCancellation()
    {
        CancelAtPeriodEnd = true;
        PendingPlanVersionId = null;
        Touch();
    }

    public void Renew(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (periodEnd <= periodStart) throw new ArgumentOutOfRangeException(nameof(periodEnd));
        if (PendingPlanVersionId.HasValue)
        {
            PlanVersionId = PendingPlanVersionId.Value;
            PendingPlanVersionId = null;
        }

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Status = SubscriptionStatus.Active;
        PastDueAt = null;
        GracePeriodEndsAt = null;
        Touch();
    }

    public void Cancel(DateTimeOffset canceledAt)
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAt = canceledAt;
        CancelAtPeriodEnd = false;
        PendingPlanVersionId = null;
        Touch();
    }

    public (DateTimeOffset Start, DateTimeOffset End) GetMonthlyUsageWindow(DateTimeOffset at)
    {
        if (at < StartedAt)
            return (StartedAt, StartedAt.AddMonths(1));

        var months = (at.Year - StartedAt.Year) * 12 + at.Month - StartedAt.Month;
        var candidate = StartedAt.AddMonths(months);
        if (candidate > at)
            months--;

        var start = StartedAt.AddMonths(Math.Max(0, months));
        return (start, StartedAt.AddMonths(Math.Max(0, months) + 1));
    }
}

public sealed class UsageCounter : BaseEntity
{
    private UsageCounter() { }

    public UsageCounter(Guid subscriptionId, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        if (subscriptionId == Guid.Empty) throw new ArgumentException("Subscription id is required.", nameof(subscriptionId));
        if (windowEnd <= windowStart) throw new ArgumentOutOfRangeException(nameof(windowEnd));
        SubscriptionId = subscriptionId;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
    }

    public Guid SubscriptionId { get; private set; }
    public DateTimeOffset WindowStart { get; private set; }
    public DateTimeOffset WindowEnd { get; private set; }
    public int PublicationCount { get; private set; }
    public Subscription Subscription { get; private set; } = null!;

    public void IncrementPublication(int limit)
    {
        if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (PublicationCount >= limit)
            throw new InvalidOperationException("publication_limit_reached");
        PublicationCount++;
        Touch();
    }
}
