namespace CidadeEmDia.Application.Subaccounts;

public interface IMasterSubaccountService
{
    Task<MasterSubaccountTeam> ListAsync(Guid masterUserId, CancellationToken cancellationToken = default);
    Task<MasterSubaccountResult> AddAsync(
        Guid masterUserId,
        string email,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);
    Task<SubaccountInvitationResult> InviteAsync(
        Guid masterUserId,
        string email,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);
    Task<SubaccountInvitationPreview?> PreviewInvitationAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
    Task<SubaccountInvitationAcceptResult> AcceptInvitationAsync(
        string rawToken,
        string password,
        string displayName,
        CancellationToken cancellationToken = default);
    Task<MasterSubaccountResult> UpdatePermissionsAsync(
        Guid masterUserId,
        Guid linkId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);
    Task<MasterSubaccountResult> RevokeAsync(
        Guid masterUserId,
        Guid linkId,
        CancellationToken cancellationToken = default);
    Task<SubaccountContext?> GetContextAsync(
        Guid subaccountUserId,
        Guid masterUserId,
        CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(
        Guid subaccountUserId,
        Guid masterUserId,
        string permissionKey,
        CancellationToken cancellationToken = default);
}

public interface ISubaccountLimitProvider
{
    Task<int?> GetLimitAsync(Guid masterUserId, CancellationToken cancellationToken = default);
}
