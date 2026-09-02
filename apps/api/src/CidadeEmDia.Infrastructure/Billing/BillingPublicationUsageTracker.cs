using CidadeEmDia.Application.Billing;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Billing;

internal sealed class BillingPublicationUsageTracker(
    AppDbContext dbContext)
    : IBillingPublicationUsageTracker
{
    public async Task<BillingPublicationUsageResult> TrackAsync(
        Guid masterUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty)
            return BillingPublicationUsageResult.Failure("invalid_master");

        var subscription = await dbContext.Subscriptions
            .Include(x => x.PlanVersion)
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return BillingPublicationUsageResult.Failure("subscription_not_found");

        if (at < subscription.StartedAt
            || at >= subscription.CurrentPeriodEnd
            || !subscription.AllowsAccess(at))
        {
            return BillingPublicationUsageResult.Failure("subscription_access_denied");
        }

        var limit = subscription.PlanVersion.MonthlyPublicationLimit;
        var (windowStart, windowEnd) = subscription.GetMonthlyUsageWindow(at);

        var counter = await dbContext.UsageCounters
            .FirstOrDefaultAsync(
                x =>
                    x.SubscriptionId == subscription.Id &&
                    x.WindowStart == windowStart,
                cancellationToken);

        if (counter is null)
        {
            counter = new UsageCounter(
                subscription.Id,
                windowStart,
                windowEnd);
            dbContext.UsageCounters.Add(counter);
        }

        try
        {
            counter.IncrementPublication(limit);
        }
        catch (InvalidOperationException exception)
            when (exception.Message == "publication_limit_reached")
        {
            return BillingPublicationUsageResult.Failure(
                "publication_limit_reached");
        }

        return BillingPublicationUsageResult.Success(
            subscription.Id,
            counter.PublicationCount,
            limit,
            windowStart,
            windowEnd);
    }
}
