using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Domain.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class BillingDomainTests
{
    [Fact]
    public void MonthlyUsageWindow_UsesSubscriptionAnniversaryWithoutAccumulation()
    {
        var startedAt = new DateTimeOffset(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), startedAt, startedAt.AddMonths(12));

        var beforeFebruaryAnniversary = subscription.GetMonthlyUsageWindow(
            new DateTimeOffset(2026, 2, 28, 9, 59, 59, TimeSpan.Zero));
        Assert.Equal(startedAt, beforeFebruaryAnniversary.Start);
        Assert.Equal(startedAt.AddMonths(1), beforeFebruaryAnniversary.End);

        var onFebruaryAnniversary = subscription.GetMonthlyUsageWindow(startedAt.AddMonths(1));
        Assert.Equal(startedAt.AddMonths(1), onFebruaryAnniversary.Start);
        Assert.Equal(startedAt.AddMonths(2), onFebruaryAnniversary.End);
    }

    [Fact]
    public void PastDue_AllowsTwoDayGraceThenBlocks()
    {
        var startedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), startedAt, startedAt.AddMonths(1));
        subscription.Activate();

        var failedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        subscription.MarkPastDue(failedAt);

        Assert.True(subscription.AllowsAccess(failedAt.AddDays(1)));
        Assert.False(subscription.AllowsAccess(failedAt.AddDays(2)));
        Assert.Equal(failedAt.AddDays(2), subscription.GracePeriodEndsAt);
    }

    [Fact]
    public void ScheduledChange_OnlyChangesVersionOnRenewal()
    {
        var initialVersionId = Guid.NewGuid();
        var nextVersionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var subscription = new Subscription(Guid.NewGuid(), initialVersionId, startedAt, startedAt.AddMonths(1));
        subscription.Activate();

        subscription.ScheduleChange(nextVersionId);

        Assert.Equal(initialVersionId, subscription.PlanVersionId);
        Assert.Equal(nextVersionId, subscription.PendingPlanVersionId);

        var nextStart = startedAt.AddMonths(1);
        subscription.Renew(nextStart, nextStart.AddMonths(3));

        Assert.Equal(nextVersionId, subscription.PlanVersionId);
        Assert.Null(subscription.PendingPlanVersionId);
    }

    [Fact]
    public void UsageCounter_RejectsPublicationBeyondLimit()
    {
        var now = DateTimeOffset.UtcNow;
        var counter = new UsageCounter(Guid.NewGuid(), now, now.AddMonths(1));

        counter.IncrementPublication(1);

        Assert.Equal(1, counter.PublicationCount);
        Assert.Throws<InvalidOperationException>(() => counter.IncrementPublication(1));
    }

    [Fact]
    public void BillingCustomer_ChargesSignupFeeOnlyOnce()
    {
        var customer = new BillingCustomer(Guid.NewGuid());
        var firstPaidAt = DateTimeOffset.UtcNow;

        customer.MarkSignupFeePaid(firstPaidAt);
        customer.MarkSignupFeePaid(firstPaidAt.AddYears(1));

        Assert.True(customer.HasPaidSignupFee);
        Assert.Equal(firstPaidAt, customer.SignupFeePaidAt);
    }

    [Fact]
    public void Downgrade_SuspendsNewestLinksAndUpgradeRestoresPlanSuspendedLinks()
    {
        var masterId = Guid.NewGuid();
        var oldest = new MasterSubaccount(masterId, Guid.NewGuid());
        var middle = new MasterSubaccount(masterId, Guid.NewGuid());
        var newest = new MasterSubaccount(masterId, Guid.NewGuid());
        var linksOldestFirst = new[] { oldest, middle, newest };

        SubaccountPlanReconciliation.ReconcileOrderedOldestFirst(linksOldestFirst, 2);

        Assert.True(oldest.IsActive);
        Assert.True(middle.IsActive);
        Assert.True(newest.IsSuspendedByPlan);

        SubaccountPlanReconciliation.ReconcileOrderedOldestFirst(linksOldestFirst, 3);

        Assert.All(linksOldestFirst, link => Assert.True(link.IsActive));
    }

    [Fact]
    public void PlanReconciliation_DoesNotRestoreManuallyRevokedLink()
    {
        var masterId = Guid.NewGuid();
        var revoked = new MasterSubaccount(masterId, Guid.NewGuid());
        revoked.Revoke(DateTimeOffset.UtcNow);
        var active = new MasterSubaccount(masterId, Guid.NewGuid());

        SubaccountPlanReconciliation.ReconcileOrderedOldestFirst(new[] { revoked, active }, 2);

        Assert.Equal(MasterSubaccountStatus.Revoked, revoked.Status);
        Assert.True(active.IsActive);
    }
}
