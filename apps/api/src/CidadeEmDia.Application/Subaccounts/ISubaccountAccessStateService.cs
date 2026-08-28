namespace CidadeEmDia.Application.Subaccounts;

public interface ISubaccountAccessStateService
{
    Task<IReadOnlyCollection<SubaccountContext>> ListActiveContextsAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default);

    Task RemoveGlobalRoleIfNoActiveLinksAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default);
}
