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

public sealed record ProfileAvatarUploadItem(
    Guid AvatarMediaId,
    string ContentType,
    Uri UploadUrl,
    DateTimeOffset UploadUrlExpiresAt);

public sealed record ProfileAvatarReadItem(
    Guid AvatarMediaId,
    Uri ReadUrl,
    DateTimeOffset ReadUrlExpiresAt);

public sealed record ProfileAvatarConfirmation(
    PrivateUserProfile Profile,
    ProfileAvatarReadItem Avatar);

public sealed record ProfileUpdateResult(bool Succeeded, string? ErrorCode, PrivateUserProfile? Profile)
{
    public static ProfileUpdateResult Success(PrivateUserProfile profile) => new(true, null, profile);
    public static ProfileUpdateResult Failure(string errorCode) => new(false, errorCode, null);
}

public sealed record ProfileAvatarUploadResult(
    bool Succeeded,
    ProfileAvatarUploadItem? Upload,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static ProfileAvatarUploadResult Success(ProfileAvatarUploadItem upload) =>
        new(true, upload);

    public static ProfileAvatarUploadResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ProfileAvatarConfirmationResult(
    bool Succeeded,
    ProfileAvatarConfirmation? Confirmation,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static ProfileAvatarConfirmationResult Success(ProfileAvatarConfirmation confirmation) =>
        new(true, confirmation);

    public static ProfileAvatarConfirmationResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ProfileAvatarReadResult(
    bool Succeeded,
    ProfileAvatarReadItem? Avatar,
    string? ErrorCode = null)
{
    public static ProfileAvatarReadResult Success(ProfileAvatarReadItem avatar) =>
        new(true, avatar);

    public static ProfileAvatarReadResult Failure(string errorCode) =>
        new(false, null, errorCode);
}

public sealed record ProfileAvatarRemovalResult(
    bool Succeeded,
    PrivateUserProfile? Profile,
    string? ErrorCode = null)
{
    public static ProfileAvatarRemovalResult Success(PrivateUserProfile profile) =>
        new(true, profile);

    public static ProfileAvatarRemovalResult Failure(string errorCode) =>
        new(false, null, errorCode);
}
