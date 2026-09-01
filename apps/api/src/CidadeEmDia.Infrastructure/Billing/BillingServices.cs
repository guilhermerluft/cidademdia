using CidadeEmDia.Application.Billing;
using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Billing;

internal sealed class BillingCatalogService(AppDbContext dbContext) : IBillingCatalogService
{
    public async Task<IReadOnlyCollection<BillingCatalogOffer>> ListCurrentOffersAsync(
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var versions = await dbContext.PlanVersions
            .AsNoTracking()
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Plan)
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Category)
            .Where(x =>
                x.PlanOffer.IsActive &&
                x.PlanOffer.Plan.IsActive &&
                x.PlanOffer.Category.IsActive &&
                x.EffectiveFrom <= now &&
                (x.EffectiveTo == null || now < x.EffectiveTo))
            .OrderBy(x => x.PlanOffer.Plan.Name)
            .ThenBy(x => x.PlanOffer.Category.BillingIntervalMonths)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        return versions
            .GroupBy(x => x.PlanOfferId)
            .Select(group => group.OrderByDescending(x => x.Version).First())
            .Select(ToOffer)
            .OrderBy(x => x.PlanName)
            .ThenBy(x => x.BillingIntervalMonths)
            .ToArray();
    }

    private static BillingCatalogOffer ToOffer(PlanVersion version) => new(
        version.PlanOfferId,
        version.Id,
        version.PlanOffer.Plan.Key,
        version.PlanOffer.Plan.Name,
        version.PlanOffer.Category.Key,
        version.PlanOffer.Category.Name,
        version.PlanOffer.Category.BillingIntervalMonths,
        version.PriceCents,
        version.SignupFeeCents,
        version.MarketingReferencePriceCents,
        version.SubaccountLimit,
        version.MonthlyPublicationLimit,
        version.Version);
}

internal sealed class BillingEntitlementService(AppDbContext dbContext) : IBillingEntitlementService
{
    public async Task<BillingEntitlements?> GetForMasterAsync(
        Guid masterUserId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty)
            return null;

        var now = at ?? DateTimeOffset.UtcNow;
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .Include(x => x.PlanVersion)
                .ThenInclude(x => x.PlanOffer)
                    .ThenInclude(x => x.Plan)
            .Include(x => x.PlanVersion)
                .ThenInclude(x => x.PlanOffer)
                    .ThenInclude(x => x.Category)
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return null;

        var (windowStart, windowEnd) = subscription.GetMonthlyUsageWindow(now);
        var usedPublications = await dbContext.UsageCounters
            .AsNoTracking()
            .Where(x => x.SubscriptionId == subscription.Id && x.WindowStart == windowStart)
            .Select(x => (int?)x.PublicationCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var version = subscription.PlanVersion;
        var offer = version.PlanOffer;
        var accessAllowed = now >= subscription.StartedAt
            && now < subscription.CurrentPeriodEnd
            && subscription.AllowsAccess(now);

        return new BillingEntitlements(
            subscription.Id,
            version.Id,
            offer.Plan.Key,
            offer.Plan.Name,
            offer.Category.Key,
            offer.Category.Name,
            subscription.Status.ToString(),
            accessAllowed,
            version.SubaccountLimit,
            version.MonthlyPublicationLimit,
            usedPublications,
            windowStart,
            windowEnd,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.GracePeriodEndsAt,
            subscription.CancelAtPeriodEnd,
            subscription.PendingPlanVersionId);
    }

    public Task<bool> HasPaidSignupFeeAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default) =>
        dbContext.BillingCustomers
            .AsNoTracking()
            .AnyAsync(x => x.MasterUserId == masterUserId && x.SignupFeePaidAt != null, cancellationToken);
}

internal sealed class BillingSubscriptionService(AppDbContext dbContext) : IBillingSubscriptionService
{
    public async Task<BillingSubscriptionOperationResult> CreatePendingAsync(
        Guid masterUserId,
        Guid planVersionId,
        DateTimeOffset startedAt,
        DateTimeOffset currentPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty || planVersionId == Guid.Empty || currentPeriodEnd <= startedAt)
            return BillingSubscriptionOperationResult.Failure("invalid_input");

        var masterExists = await dbContext.Users.AnyAsync(x => x.Id == masterUserId, cancellationToken);
        if (!masterExists)
            return BillingSubscriptionOperationResult.Failure("master_not_found");

        var versionExists = await dbContext.PlanVersions.AnyAsync(x => x.Id == planVersionId, cancellationToken);
        if (!versionExists)
            return BillingSubscriptionOperationResult.Failure("plan_version_not_found");

        var hasOpenSubscription = await dbContext.Subscriptions.AnyAsync(
            x => x.MasterUserId == masterUserId && x.Status != SubscriptionStatus.Canceled,
            cancellationToken);
        if (hasOpenSubscription)
            return BillingSubscriptionOperationResult.Failure("subscription_already_exists");

        var customer = await dbContext.BillingCustomers
            .FirstOrDefaultAsync(x => x.MasterUserId == masterUserId, cancellationToken);
        if (customer is null)
            dbContext.BillingCustomers.Add(new BillingCustomer(masterUserId));

        dbContext.Subscriptions.Add(new Subscription(masterUserId, planVersionId, startedAt, currentPeriodEnd));
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> ActivateAsync(
        Guid masterUserId,
        bool signupFeePaid,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        if (signupFeePaid)
        {
            var customer = await dbContext.BillingCustomers
                .FirstOrDefaultAsync(x => x.MasterUserId == masterUserId, cancellationToken);
            if (customer is null)
            {
                customer = new BillingCustomer(masterUserId);
                dbContext.BillingCustomers.Add(customer);
            }
            customer.MarkSignupFeePaid(activatedAt);
        }

        var intervalMonths =
            await dbContext.PlanVersions
                .Where(x =>
                    x.Id == subscription.PlanVersionId)
                .Select(x =>
                    x.PlanOffer
                        .Category
                        .BillingIntervalMonths)
                .SingleAsync(
                    cancellationToken);

        subscription.ActivateInitialPeriod(
            activatedAt,
            activatedAt.AddMonths(intervalMonths));

        await ReconcileSubaccountsAsync(
            masterUserId,
            subscription.PlanVersionId,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> MarkPastDueAsync(
        Guid masterUserId,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        subscription.MarkPastDue(failedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> ScheduleChangeAsync(
        Guid masterUserId,
        Guid nextPlanVersionId,
        CancellationToken cancellationToken = default)
    {
        if (nextPlanVersionId == Guid.Empty)
            return BillingSubscriptionOperationResult.Failure("invalid_input");

        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        if (!await dbContext.PlanVersions.AnyAsync(x => x.Id == nextPlanVersionId, cancellationToken))
            return BillingSubscriptionOperationResult.Failure("plan_version_not_found");

        subscription.ScheduleChange(nextPlanVersionId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> RequestCancellationAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        subscription.RequestCancellation();
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> ApplyRenewalAsync(
        Guid masterUserId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        if (subscription.CancelAtPeriodEnd)
        {
            subscription.Cancel(periodStart);
            await dbContext.SaveChangesAsync(cancellationToken);
            return BillingSubscriptionOperationResult.Success();
        }

        subscription.Renew(periodStart, periodEnd);
        await ReconcileSubaccountsAsync(masterUserId, subscription.PlanVersionId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    public async Task<BillingSubscriptionOperationResult> CancelAsync(
        Guid masterUserId,
        DateTimeOffset canceledAt,
        CancellationToken cancellationToken = default)
    {
        var subscription = await GetOpenSubscriptionAsync(masterUserId, cancellationToken);
        if (subscription is null)
            return BillingSubscriptionOperationResult.Failure("subscription_not_found");

        subscription.Cancel(canceledAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingSubscriptionOperationResult.Success();
    }

    private Task<Subscription?> GetOpenSubscriptionAsync(Guid masterUserId, CancellationToken cancellationToken) =>
        dbContext.Subscriptions
            .Where(x => x.MasterUserId == masterUserId && x.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task ReconcileSubaccountsAsync(
        Guid masterUserId,
        Guid planVersionId,
        CancellationToken cancellationToken)
    {
        var limit = await dbContext.PlanVersions
            .Where(x => x.Id == planVersionId)
            .Select(x => x.SubaccountLimit)
            .SingleAsync(cancellationToken);

        var links = await dbContext.MasterSubaccounts
            .Where(x =>
                x.MasterUserId == masterUserId &&
                (x.Status == MasterSubaccountStatus.Active || x.Status == MasterSubaccountStatus.SuspendedByPlan))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        SubaccountPlanReconciliation.ReconcileOrderedOldestFirst(links, limit);
    }
}

internal sealed class BillingSubaccountLimitProvider(IBillingEntitlementService entitlementService) : ISubaccountLimitProvider
{
    public async Task<int?> GetLimitAsync(Guid masterUserId, CancellationToken cancellationToken = default)
    {
        var entitlement = await entitlementService.GetForMasterAsync(masterUserId, cancellationToken: cancellationToken);
        return entitlement?.AccessAllowed == true
            ? entitlement.SubaccountLimit
            : null;
    }
}
