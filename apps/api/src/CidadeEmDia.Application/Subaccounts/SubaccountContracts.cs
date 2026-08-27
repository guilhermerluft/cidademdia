namespace CidadeEmDia.Application.Subaccounts;

public sealed record MasterSubaccountMember(
    Guid LinkId,
    Guid UserId,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyCollection<string> Permissions);

public sealed record MasterSubaccountTeam(
    int? Limit,
    int ActiveCount,
    IReadOnlyCollection<MasterSubaccountMember> Members);

public sealed record SubaccountContext(
    Guid LinkId,
    Guid MasterUserId,
    IReadOnlyCollection<string> Permissions);

public sealed record MasterSubaccountResult(
    bool Succeeded,
    string? ErrorCode,
    MasterSubaccountMember? Member)
{
    public static MasterSubaccountResult Success(MasterSubaccountMember member) => new(true, null, member);
    public static MasterSubaccountResult Failure(string errorCode) => new(false, errorCode, null);
}
