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
    public void InitialActivation_ReanchorsPeriodToApprovedPayment()
    {
        var checkoutCreatedAt =
            new DateTimeOffset(
                2026,
                9,
                1,
                12,
                0,
                0,
                TimeSpan.Zero);

        var approvedAt =
            new DateTimeOffset(
                2026,
                9,
                4,
                18,
                30,
                0,
                TimeSpan.Zero);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                checkoutCreatedAt,
                checkoutCreatedAt.AddMonths(3));

        subscription.ActivateInitialPeriod(
            approvedAt,
            approvedAt.AddMonths(3));

        Assert.Equal(
            approvedAt,
            subscription.StartedAt);

        Assert.Equal(
            approvedAt,
            subscription.CurrentPeriodStart);

        Assert.Equal(
            approvedAt.AddMonths(3),
            subscription.CurrentPeriodEnd);

        Assert.Equal(
            SubscriptionStatus.Active,
            subscription.Status);

        var usageWindow =
            subscription.GetMonthlyUsageWindow(
                approvedAt.AddDays(15));

        Assert.Equal(
            approvedAt,
            usageWindow.Start);

        Assert.Equal(
            approvedAt.AddMonths(1),
            usageWindow.End);
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
    public void PastDue_RepeatedFailureDoesNotExtendGrace()
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                8,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                startedAt,
                startedAt.AddMonths(1));

        subscription.Activate();

        var firstFailure =
            new DateTimeOffset(
                2026,
                8,
                10,
                12,
                0,
                0,
                TimeSpan.Zero);

        subscription.MarkPastDue(
            firstFailure);

        var originalPastDueAt =
            subscription.PastDueAt;

        var originalGraceEnd =
            subscription.GracePeriodEndsAt;

        subscription.MarkPastDue(
            firstFailure.AddDays(1));

        Assert.Equal(
            originalPastDueAt,
            subscription.PastDueAt);

        Assert.Equal(
            originalGraceEnd,
            subscription.GracePeriodEndsAt);

        Assert.Equal(
            firstFailure.AddDays(2),
            subscription.GracePeriodEndsAt);

        Assert.False(
            subscription.AllowsAccess(
                firstFailure.AddDays(2)));
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

    [Fact]
    public void RequestCancellation_DoesNotCancelPaidPeriodImmediately()
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var periodEnd =
            startedAt.AddMonths(1);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                startedAt,
                periodEnd);

        subscription.Activate();

        subscription.RequestCancellation();

        Assert.Equal(
            SubscriptionStatus.Active,
            subscription.Status);

        Assert.True(
            subscription.CancelAtPeriodEnd);

        Assert.Null(
            subscription.CanceledAt);

        Assert.Equal(
            periodEnd,
            subscription.CurrentPeriodEnd);

        Assert.True(
            subscription.AllowsAccess(
                startedAt.AddDays(15)));
    }


    [Fact]
    public void CancellationAtPeriodEnd_StopsAccessAtBoundary()
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var periodEnd =
            startedAt.AddMonths(1);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                startedAt,
                periodEnd);

        subscription.Activate();
        subscription.RequestCancellation();

        Assert.True(
            subscription.AllowsAccess(
                periodEnd.AddTicks(-1)));

        Assert.False(
            subscription.AllowsAccess(
                periodEnd));

        Assert.False(
            subscription.AllowsAccess(
                periodEnd.AddDays(1)));
    }


    [Fact]
    public void ClearCancellationRequest_RemovesPendingCancellation()
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                Guid.NewGuid(),
                startedAt,
                startedAt.AddMonths(1));

        subscription.Activate();
        subscription.RequestCancellation();

        Assert.True(
            subscription.CancelAtPeriodEnd);

        subscription.ClearCancellationRequest();

        Assert.False(
            subscription.CancelAtPeriodEnd);
    }

}
