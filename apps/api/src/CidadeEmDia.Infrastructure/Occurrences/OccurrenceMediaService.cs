using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceMediaService(
    AppDbContext dbContext,
    R2ObjectStorage storage)
    : IOccurrenceMediaService
{
    private static readonly IReadOnlyDictionary<string, MediaTypeRule> AllowedTypes =
        new Dictionary<string, MediaTypeRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new("jpg", [".jpg", ".jpeg"]),
            ["image/png"] = new("png", [".png"]),
            ["image/webp"] = new("webp", [".webp"]),
            ["video/mp4"] = new("mp4", [".mp4"]),
            ["video/webm"] = new("webm", [".webm"])
        };

    public async Task<OccurrenceMediaUploadResult> RequestUploadAsync(
        Guid requesterUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty)
            return OccurrenceMediaUploadResult.Failure("invalid_media_request");

        if (!storage.IsConfigured)
            return OccurrenceMediaUploadResult.Failure(
                "storage_not_configured",
                "Cloudflare R2 is not configured for this environment.");

        var normalizedType = contentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType)
            || !AllowedTypes.TryGetValue(normalizedType, out var mediaRule))
        {
            return OccurrenceMediaUploadResult.Failure(
                "media_type_not_allowed",
                "Only JPEG, PNG, WebP, MP4 and WebM occurrence media are accepted.");
        }

        var safeFileName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
        {
            return OccurrenceMediaUploadResult.Failure(
                "invalid_media_request",
                "A valid original file name with at most 255 characters is required.");
        }

        var originalExtension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!mediaRule.AllowedExtensions.Contains(originalExtension, StringComparer.OrdinalIgnoreCase))
        {
            return OccurrenceMediaUploadResult.Failure(
                "media_extension_not_allowed",
                "The file extension does not match the declared occurrence media content type.");
        }

        var maxBytes = normalizedType.StartsWith("image/", StringComparison.Ordinal)
            ? storage.MaxImageBytes
            : storage.MaxVideoBytes;

        if (sizeBytes <= 0 || sizeBytes > maxBytes)
        {
            return OccurrenceMediaUploadResult.Failure(
                "media_size_not_allowed",
                $"Declared media size must be between 1 and {maxBytes} bytes for this content type.");
        }

        var requesterIsActive = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == requesterUserId && user.Status == UserStatus.Active,
                cancellationToken);

        if (!requesterIsActive)
            return OccurrenceMediaUploadResult.Failure("media_upload_not_allowed");

        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var objectKey = $"occurrences/uploads/{now:yyyy/MM}/{mediaId:N}.{mediaRule.ObjectExtension}";

        OccurrenceMedia media;
        try
        {
            media = new OccurrenceMedia(
                mediaId,
                requesterUserId,
                objectKey,
                safeFileName,
                normalizedType,
                sizeBytes,
                now);
        }
        catch (DomainException exception)
        {
            return OccurrenceMediaUploadResult.Failure("invalid_media_request", exception.Message);
        }

        dbContext.OccurrenceMedia.Add(media);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceMediaUploadResult.Failure(
                "media_persistence_conflict",
                "The pending occurrence media record could not be persisted.");
        }

        var uploadUrl = storage.CreateUploadUrl(
            media.ObjectKey,
            media.ContentType,
            now,
            out var uploadUrlExpiresAt);

        return OccurrenceMediaUploadResult.Success(
            new OccurrenceMediaUploadItem(
                media.Id,
                media.Status.Value,
                media.ContentType,
                media.ExpectedSizeBytes,
                uploadUrl,
                uploadUrlExpiresAt));
    }

    public async Task<OccurrenceMediaOperationResult> ConfirmUploadAsync(
        Guid requesterUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || mediaId == Guid.Empty)
            return OccurrenceMediaOperationResult.Failure("invalid_media_request");

        if (!storage.IsConfigured)
            return OccurrenceMediaOperationResult.Failure("storage_not_configured");

        var media = await dbContext.OccurrenceMedia
            .FirstOrDefaultAsync(
                item => item.Id == mediaId && item.UploaderUserId == requesterUserId,
                cancellationToken);

        if (media is null)
            return OccurrenceMediaOperationResult.Failure("media_not_found");

        if (media.Status == OccurrenceMediaStatus.Ready)
            return OccurrenceMediaOperationResult.Success(ToItem(media), wasChanged: false);

        R2ObjectMetadata? metadata;
        byte[]? signature;
        try
        {
            metadata = await storage.GetObjectMetadataAsync(media.ObjectKey, cancellationToken);
            signature = metadata is null
                ? null
                : await storage.ReadObjectPrefixAsync(media.ObjectKey, 32, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return OccurrenceMediaOperationResult.Failure("storage_verification_failed", exception.Message);
        }

        if (metadata is null || signature is null)
        {
            return OccurrenceMediaOperationResult.Failure(
                "media_object_missing",
                "The uploaded object was not found in Cloudflare R2.");
        }

        if (!MediaSignatureValidator.HasExpectedSignature(media.ContentType, signature))
        {
            return OccurrenceMediaOperationResult.Failure(
                "media_signature_invalid",
                "The uploaded object signature does not match the declared occurrence media content type.");
        }

        try
        {
            media.MarkReady(
                metadata.SizeBytes,
                metadata.ContentType ?? string.Empty,
                DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return OccurrenceMediaOperationResult.Failure("media_verification_failed", exception.Message);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceMediaOperationResult.Failure("media_persistence_conflict");
        }

        return OccurrenceMediaOperationResult.Success(ToItem(media), wasChanged: true);
    }

    public async Task<OccurrenceMediaListResult> ListForOccurrenceAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceMediaListResult.Failure("invalid_media_request");

        if (!await CanReadOccurrenceAsync(requesterUserId, occurrenceId, cancellationToken))
            return OccurrenceMediaListResult.Failure("occurrence_not_found");

        var items = await dbContext.OccurrenceMedia
            .AsNoTracking()
            .Where(media => media.OccurrenceId == occurrenceId && media.Status == OccurrenceMediaStatus.Ready)
            .OrderBy(media => media.AttachedAt)
            .ThenBy(media => media.Id)
            .ToListAsync(cancellationToken);

        return OccurrenceMediaListResult.Success(items.Select(ToItem).ToArray());
    }

    public async Task<OccurrenceMediaReadUrlResult> GetReadUrlAsync(
        Guid requesterUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || mediaId == Guid.Empty)
            return OccurrenceMediaReadUrlResult.Failure("invalid_media_request");

        if (!storage.IsConfigured)
            return OccurrenceMediaReadUrlResult.Failure("storage_not_configured");

        var media = await dbContext.OccurrenceMedia
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mediaId, cancellationToken);

        if (media is null)
            return OccurrenceMediaReadUrlResult.Failure("media_not_found");

        if (media.Status != OccurrenceMediaStatus.Ready)
            return OccurrenceMediaReadUrlResult.Failure("media_not_ready");

        var authorized = media.UploaderUserId == requesterUserId;
        if (!authorized && media.OccurrenceId.HasValue)
        {
            authorized = await CanReadOccurrenceAsync(
                requesterUserId,
                media.OccurrenceId.Value,
                cancellationToken);
        }

        if (!authorized)
            return OccurrenceMediaReadUrlResult.Failure("media_access_denied");

        var now = DateTimeOffset.UtcNow;
        var readUrl = storage.CreateReadUrl(media.ObjectKey, now, out var expiresAt);

        return OccurrenceMediaReadUrlResult.Success(
            new OccurrenceMediaReadUrlItem(media.Id, readUrl, expiresAt));
    }

    private async Task<bool> CanReadOccurrenceAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Occurrences
            .AsNoTracking()
            .AnyAsync(
                occurrence => occurrence.Id == occurrenceId
                    && occurrence.AuthorUserId == requesterUserId,
                cancellationToken))
        {
            return true;
        }

        if (await dbContext.OccurrenceTargets
            .AsNoTracking()
            .AnyAsync(
                target => target.OccurrenceId == occurrenceId
                    && target.MasterUserId == requesterUserId
                    && target.Status == OccurrenceTargetStatus.Accepted,
                cancellationToken))
        {
            return true;
        }

        return await dbContext.OccurrenceTargetAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.OccurrenceTarget.OccurrenceId == occurrenceId
                    && assignment.OccurrenceTarget.Status == OccurrenceTargetStatus.Accepted
                    && assignment.MasterSubaccount.SubaccountUserId == requesterUserId
                    && assignment.MasterSubaccount.SubaccountUser.Status == UserStatus.Active
                    && assignment.MasterSubaccount.Status == MasterSubaccountStatus.Active
                    && assignment.MasterSubaccount.Permissions.Any(permission =>
                        permission.Permission.Key == SubaccountPermissionKeys.OccurrenceReadTargeted),
                cancellationToken);
    }

    private static OccurrenceMediaItem ToItem(OccurrenceMedia media) =>
        new(
            media.Id,
            media.OccurrenceId,
            media.Status.Value,
            media.OriginalFileName,
            media.ContentType,
            media.ActualSizeBytes ?? media.ExpectedSizeBytes,
            media.CreatedAt,
            media.ReadyAt,
            media.AttachedAt);

    private sealed record MediaTypeRule(
        string ObjectExtension,
        IReadOnlyList<string> AllowedExtensions);
}
