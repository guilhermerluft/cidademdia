namespace CidadeEmDia.Application.Subaccounts;

public sealed record MasterSubaccountMember(
    Guid LinkId,
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyCollection<string> Permissions);

public sealed record SubaccountInvitationSummary(
    Guid InvitationId,
    string Email,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset ExpiresAt);

public sealed record MasterSubaccountTeam(
    int? Limit,
    int ActiveCount,
    int PendingInvitationCount,
    IReadOnlyCollection<MasterSubaccountMember> Members,
    IReadOnlyCollection<SubaccountInvitationSummary> Invitations);

public sealed record SubaccountContext(
    Guid LinkId,
    Guid MasterUserId,
    IReadOnlyCollection<string> Permissions);

public sealed record SubaccountInvitationPreview(
    string Email,
    string MasterDisplayName,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset ExpiresAt);

public sealed record MasterSubaccountResult(
    bool Succeeded,
    string? ErrorCode,
    MasterSubaccountMember? Member)
{
    public static MasterSubaccountResult Success(MasterSubaccountMember member) => new(true, null, member);
    public static MasterSubaccountResult Failure(string errorCode) => new(false, errorCode, null);
}

public sealed record SubaccountInvitationResult(
    bool Succeeded,
    string? ErrorCode,
    SubaccountInvitationSummary? Invitation)
{
    public static SubaccountInvitationResult Success(SubaccountInvitationSummary invitation) => new(true, null, invitation);
    public static SubaccountInvitationResult Failure(string errorCode) => new(false, errorCode, null);
}

public sealed record SubaccountInvitationAcceptResult(bool Succeeded, string? ErrorCode)
{
    public static SubaccountInvitationAcceptResult Success() => new(true, null);
    public static SubaccountInvitationAcceptResult Failure(string errorCode) => new(false, errorCode);
}
