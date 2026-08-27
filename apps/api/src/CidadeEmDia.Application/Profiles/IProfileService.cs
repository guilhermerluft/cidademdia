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
}
