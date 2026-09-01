using CidadeEmDia.Domain.Billing;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoReactivationDomainTests
{
    [Fact]
    public void DelayedReactivation_ClearsCancellationBeforeRenewal()
    {
        var currentPlanVersionId = Guid.NewGuid();
        var targetPlanVersionId = Guid.NewGuid();
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var boundary =
            startedAt.AddMonths(1);

        var approvedAt =
            boundary.AddHours(2);

        var subscription =
            new Subscription(
                Guid.NewGuid(),
                currentPlanVersionId,
                startedAt,
                boundary);

        subscription.Activate();
        subscription.RequestCancellation();

        // A promoção do replacement agenda a versão que será
        // efetivada quando o pagamento da reativação for aprovado.
        subscription.ScheduleChange(
            targetPlanVersionId);

        subscription.ClearCancellationRequest();
        subscription.Renew(
            approvedAt,
            approvedAt.AddMonths(1));

        Assert.Equal(
            SubscriptionStatus.Active,
            subscription.Status);

        Assert.False(
            subscription.CancelAtPeriodEnd);

        Assert.Equal(
            targetPlanVersionId,
            subscription.PlanVersionId);

        Assert.Null(
            subscription.PendingPlanVersionId);

        Assert.Equal(
            approvedAt,
            subscription.CurrentPeriodStart);

        Assert.Equal(
            approvedAt.AddMonths(1),
            subscription.CurrentPeriodEnd);

        Assert.True(
            subscription.AllowsAccess(
                approvedAt));
    }
}
