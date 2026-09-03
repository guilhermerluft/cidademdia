namespace CidadeEmDia.Application.Profiles;

public interface IProfileService
{
    Task<PrivateUserProfile?> GetPrivateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PublicUserProfile?> GetPublicAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProfileUpdateResult> UpdateAsync(
        Guid userId,
        string displayName,
        string? document,
        string? phone,
        CancellationToken cancellationToken = default);
    Task<ProfileAvatarUploadResult> RequestAvatarUploadAsync(
        Guid userId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);
    Task<ProfileAvatarConfirmationResult> ConfirmAvatarUploadAsync(
        Guid userId,
        Guid avatarMediaId,
        string contentType,
        CancellationToken cancellationToken = default);
    Task<ProfileAvatarReadResult> GetAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<ProfileAvatarRemovalResult> RemoveAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
