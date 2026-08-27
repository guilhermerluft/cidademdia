namespace CidadeEmDia.Application.Profiles;

public sealed record PrivateUserProfile(
    Guid UserId,
    string Email,
    string DisplayName,
    string? Document,
    string? Phone,
    Guid? AvatarMediaId,
    IReadOnlyCollection<string> Roles);

public sealed record PublicUserProfile(
    Guid UserId,
    string DisplayName,
    Guid? AvatarMediaId);

public sealed record ProfileUpdateResult(bool Succeeded, string? ErrorCode, PrivateUserProfile? Profile)
{
    public static ProfileUpdateResult Success(PrivateUserProfile profile) => new(true, null, profile);
    public static ProfileUpdateResult Failure(string errorCode) => new(false, errorCode, null);
}
