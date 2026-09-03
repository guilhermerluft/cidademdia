using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class ProfileService(
    AppDbContext dbContext,
    R2ObjectStorage storage) : IProfileService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AvatarTypes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"]
        };

    public async Task<PrivateUserProfile?> GetPrivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Profile)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        return user?.Profile is null ? null : ToPrivate(user, user.Profile);
    }

    public async Task<PublicUserProfile?> GetPublicAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return profile is null
            ? null
            : new PublicUserProfile(profile.UserId, profile.DisplayName, profile.AvatarMediaId);
    }

    public async Task<ProfileUpdateResult> UpdateAsync(
        Guid userId,
        string displayName,
        string? document,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 2 or > 160)
            return ProfileUpdateResult.Failure("invalid_input");

        if (!BrazilianDocumentValidator.TryNormalize(document, out var normalizedDocument))
            return ProfileUpdateResult.Failure("invalid_document");

        if (!TryNormalizePhone(phone, out var normalizedPhone))
            return ProfileUpdateResult.Failure("invalid_phone");

        var profile = await LoadProfileAsync(userId, cancellationToken);
        if (profile is null)
            return ProfileUpdateResult.Failure("profile_not_found");

        profile.Update(displayName, normalizedDocument, normalizedPhone);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProfileUpdateResult.Success(ToPrivate(profile.User, profile));
    }

    public async Task<ProfileAvatarUploadResult> RequestAvatarUploadAsync(
        Guid userId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!storage.IsConfigured)
            return ProfileAvatarUploadResult.Failure("storage_not_configured");

        if (!TryValidateAvatarDeclaration(fileName, contentType, sizeBytes, out var normalizedType, out var errorCode))
            return ProfileAvatarUploadResult.Failure(errorCode!);

        var profileExists = await dbContext.UserProfiles
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId, cancellationToken);
        if (!profileExists)
            return ProfileAvatarUploadResult.Failure("profile_not_found");

        var avatarMediaId = Guid.NewGuid();
        var objectKey = AvatarObjectKey(userId, avatarMediaId);
        var now = DateTimeOffset.UtcNow;
        var uploadUrl = storage.CreateUploadUrl(objectKey, normalizedType!, now, out var expiresAt);

        return ProfileAvatarUploadResult.Success(
            new ProfileAvatarUploadItem(
                avatarMediaId,
                normalizedType!,
                uploadUrl,
                expiresAt));
    }

    public async Task<ProfileAvatarConfirmationResult> ConfirmAvatarUploadAsync(
        Guid userId,
        Guid avatarMediaId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (avatarMediaId == Guid.Empty)
            return ProfileAvatarConfirmationResult.Failure("invalid_avatar_request");
        if (!storage.IsConfigured)
            return ProfileAvatarConfirmationResult.Failure("storage_not_configured");

        var normalizedType = contentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType) || !AvatarTypes.ContainsKey(normalizedType))
            return ProfileAvatarConfirmationResult.Failure("avatar_type_not_allowed");

        var profile = await LoadProfileAsync(userId, cancellationToken);
        if (profile is null)
            return ProfileAvatarConfirmationResult.Failure("profile_not_found");

        var objectKey = AvatarObjectKey(userId, avatarMediaId);
        R2ObjectMetadata? metadata;
        byte[]? signature;
        try
        {
            metadata = await storage.GetObjectMetadataAsync(objectKey, cancellationToken);
            signature = metadata is null
                ? null
                : await storage.ReadObjectPrefixAsync(objectKey, 32, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return ProfileAvatarConfirmationResult.Failure("storage_verification_failed", exception.Message);
        }

        if (metadata is null || signature is null)
            return ProfileAvatarConfirmationResult.Failure("avatar_object_missing");

        if (metadata.SizeBytes <= 0 || metadata.SizeBytes > storage.MaxImageBytes)
        {
            await DeleteQuietlyAsync(objectKey, cancellationToken);
            return ProfileAvatarConfirmationResult.Failure("avatar_size_not_allowed");
        }

        if (!string.Equals(metadata.ContentType, normalizedType, StringComparison.OrdinalIgnoreCase)
            || !MediaSignatureValidator.HasExpectedSignature(normalizedType, signature))
        {
            await DeleteQuietlyAsync(objectKey, cancellationToken);
            return ProfileAvatarConfirmationResult.Failure("avatar_signature_invalid");
        }

        var previousAvatar = profile.AvatarMediaId;
        profile.SetAvatar(avatarMediaId);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (previousAvatar.HasValue && previousAvatar.Value != avatarMediaId)
        {
            await DeleteQuietlyAsync(
                AvatarObjectKey(userId, previousAvatar.Value),
                cancellationToken);
        }

        var avatar = CreateAvatarReadItem(userId, avatarMediaId);
        return ProfileAvatarConfirmationResult.Success(
            new ProfileAvatarConfirmation(ToPrivate(profile.User, profile), avatar));
    }

    public async Task<ProfileAvatarReadResult> GetAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!storage.IsConfigured)
            return ProfileAvatarReadResult.Failure("storage_not_configured");

        var avatarMediaId = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.AvatarMediaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!avatarMediaId.HasValue)
            return ProfileAvatarReadResult.Failure("avatar_not_found");

        return ProfileAvatarReadResult.Success(CreateAvatarReadItem(userId, avatarMediaId.Value));
    }

    public async Task<ProfileAvatarRemovalResult> RemoveAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        if (profile is null)
            return ProfileAvatarRemovalResult.Failure("profile_not_found");

        var previousAvatar = profile.AvatarMediaId;
        profile.SetAvatar(null);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (previousAvatar.HasValue && storage.IsConfigured)
        {
            await DeleteQuietlyAsync(
                AvatarObjectKey(userId, previousAvatar.Value),
                cancellationToken);
        }

        return ProfileAvatarRemovalResult.Success(ToPrivate(profile.User, profile));
    }

    private async Task<UserProfile?> LoadProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.UserProfiles
            .Include(x => x.User)
                .ThenInclude(x => x.Roles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    private ProfileAvatarReadItem CreateAvatarReadItem(Guid userId, Guid avatarMediaId)
    {
        var readUrl = storage.CreateReadUrl(
            AvatarObjectKey(userId, avatarMediaId),
            DateTimeOffset.UtcNow,
            out var expiresAt);
        return new ProfileAvatarReadItem(avatarMediaId, readUrl, expiresAt);
    }

    private async Task DeleteQuietlyAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteObjectAsync(objectKey, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // A persistência do perfil é a fonte de verdade; limpeza de objeto antigo é best effort.
        }
    }

    private bool TryValidateAvatarDeclaration(
        string fileName,
        string contentType,
        long sizeBytes,
        out string? normalizedType,
        out string? errorCode)
    {
        normalizedType = contentType?.Trim().ToLowerInvariant();
        errorCode = null;

        if (string.IsNullOrWhiteSpace(normalizedType)
            || !AvatarTypes.TryGetValue(normalizedType, out var extensions))
        {
            errorCode = "avatar_type_not_allowed";
            return false;
        }

        var safeFileName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
        {
            errorCode = "invalid_avatar_request";
            return false;
        }

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errorCode = "avatar_extension_not_allowed";
            return false;
        }

        if (sizeBytes <= 0 || sizeBytes > storage.MaxImageBytes)
        {
            errorCode = "avatar_size_not_allowed";
            return false;
        }

        return true;
    }

    private static string AvatarObjectKey(Guid userId, Guid avatarMediaId) =>
        $"profiles/{userId:N}/avatars/{avatarMediaId:N}";

    private static PrivateUserProfile ToPrivate(User user, UserProfile profile)
    {
        var roles = user.Roles
            .Select(x => x.Role.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new PrivateUserProfile(
            user.Id,
            user.Email,
            profile.DisplayName,
            profile.Document,
            profile.Phone,
            profile.AvatarMediaId,
            roles);
    }

    private static bool TryNormalizePhone(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        normalized = new string(value.Where(char.IsDigit).ToArray());
        return normalized.Length is >= 10 and <= 15;
    }
}
