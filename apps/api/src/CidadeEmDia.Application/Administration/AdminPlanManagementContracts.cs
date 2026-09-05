namespace CidadeEmDia.Application.Administration;

public interface IAdminPlanManagementService
{
    Task<AdminResult<AdminPlanVersionChange>> UpdatePlanAsync(
        Guid requesterUserId,
        Guid currentPlanVersionId,
        AdminPlanUpdateCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record AdminPlanUpdateCommand(
    long PriceCents,
    long SignupFeeCents,
    int SubaccountLimit,
    int MonthlyPublicationLimit,
    string Reason);

public sealed record AdminPlanVersionChange(
    AdminPlanItem Plan,
    bool Changed);
