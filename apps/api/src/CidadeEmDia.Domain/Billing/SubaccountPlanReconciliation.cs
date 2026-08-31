using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Domain.Billing;

public static class SubaccountPlanReconciliation
{
    public static void ReconcileOrderedOldestFirst(
        IReadOnlyList<MasterSubaccount> links,
        int allowedActiveCount)
    {
        if (allowedActiveCount < 0)
            throw new ArgumentOutOfRangeException(nameof(allowedActiveCount));

        var eligibleIndex = 0;
        foreach (var link in links)
        {
            if (link.Status == MasterSubaccountStatus.Revoked)
                continue;

            if (eligibleIndex < allowedActiveCount)
                link.Reactivate();
            else
                link.SuspendByPlan();

            eligibleIndex++;
        }
    }
}
