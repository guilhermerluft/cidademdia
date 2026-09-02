using System.Data;
using System.Text;
using CidadeEmDia.Application.Billing;
using CidadeEmDia.Application.Content;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Content;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Content;

internal sealed class ContentService(
    AppDbContext dbContext,
    R2ObjectStorage storage,
    IBillingPublicationUsageTracker publicationUsageTracker)
    : IContentService
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

    public async Task<ContentPostResult> CreateDraftAsync(
        Guid requesterUserId,
        CreatePostDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || command is null)
            return ContentPostResult.Failure("invalid_post_request");

        var actor = await LoadPublisherActorAsync(requesterUserId, cancellationToken);
        if (!actor.Allowed)
            return ContentPostResult.Failure("post_publish_not_allowed");

        if (command.Placements is null || command.Placements.Count == 0)
            return ContentPostResult.Failure("post_placement_required");

        var normalizedPlacements = command.Placements
            .Select(x => new ContentPlacementInput(
                x.PlacementKey?.Trim().ToLowerInvariant() ?? string.Empty,
                x.Priority,
                x.DisplayOrder))
            .ToArray();

        if (normalizedPlacements.Any(x => !PostPlacementKeys.IsSupported(x.PlacementKey)))
            return ContentPostResult.Failure("post_placement_not_supported");

        if (normalizedPlacements
            .GroupBy(x => x.PlacementKey, StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1))
        {
            return ContentPostResult.Failure("post_placement_duplicate");
        }

        Post post;
        try
        {
            post = new Post(
                requesterUserId,
                actor.IsAdmin ? null : requesterUserId,
                command.Type,
                command.Title,
                command.Body,
                command.LinkUrl);

            foreach (var placement in normalizedPlacements)
            {
                post.Placements.Add(
                    new PostPlacement(
                        post.Id,
                        placement.PlacementKey,
                        placement.Priority,
                        placement.DisplayOrder));
            }
        }
        catch (DomainException exception)
        {
            return ContentPostResult.Failure(exception.Message, exception.Message);
        }

        dbContext.Posts.Add(post);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ContentPostResult.Failure("post_persistence_conflict");
        }

        return ContentPostResult.Success(ToItem(post, includeSignedUrls: false));
    }

    public async Task<ContentManagedPostListResult> ListManagedAsync(
        Guid requesterUserId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty)
            return ContentManagedPostListResult.Failure("invalid_post_request");

        var actor = await LoadPublisherActorAsync(requesterUserId, cancellationToken);
        if (!actor.Allowed)
            return ContentManagedPostListResult.Failure("post_publish_not_allowed");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        IQueryable<Post> query = dbContext.Posts.AsNoTracking();
        if (!actor.IsAdmin)
            query = query.Where(x => x.MasterUserId == requesterUserId);

        var totalItems = await query.CountAsync(cancellationToken);

        var posts = await query
            .Include(x => x.Media)
            .Include(x => x.Placements)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return ContentManagedPostListResult.Success(
            new ContentManagedPostPage(
                posts.Select(x => ToItem(x, includeSignedUrls: true)).ToArray(),
                page,
                pageSize,
                totalItems));
    }

    public async Task<ContentMediaUploadResult> RequestMediaUploadAsync(
        Guid requesterUserId,
        Guid postId,
        string fileName,
        string contentType,
        long sizeBytes,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || postId == Guid.Empty)
            return ContentMediaUploadResult.Failure("invalid_media_request");

        if (!storage.IsConfigured)
        {
            return ContentMediaUploadResult.Failure(
                "storage_not_configured",
                "Cloudflare R2 is not configured for this environment.");
        }

        var post = await dbContext.Posts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);

        if (post is null)
            return ContentMediaUploadResult.Failure("post_not_found");
        if (!await CanManagePostAsync(requesterUserId, post, cancellationToken))
            return ContentMediaUploadResult.Failure("post_access_denied");
        if (post.Status != PostStatusKeys.Draft)
            return ContentMediaUploadResult.Failure("post_not_draft");

        var normalizedType = contentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType)
            || !AllowedTypes.TryGetValue(normalizedType, out var mediaRule))
        {
            return ContentMediaUploadResult.Failure(
                "media_type_not_allowed",
                "Only JPEG, PNG, WebP, MP4 and WebM post media are accepted.");
        }

        var safeFileName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
            return ContentMediaUploadResult.Failure("invalid_media_request");

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!mediaRule.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return ContentMediaUploadResult.Failure("media_extension_not_allowed");

        var maxBytes = normalizedType.StartsWith("image/", StringComparison.Ordinal)
            ? storage.MaxImageBytes
            : storage.MaxVideoBytes;

        if (sizeBytes <= 0 || sizeBytes > maxBytes || sortOrder < 0)
            return ContentMediaUploadResult.Failure("media_size_not_allowed");

        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var objectKey = $"posts/{post.Id:N}/{now:yyyy/MM}/{mediaId:N}.{mediaRule.ObjectExtension}";

        PostMedia media;
        try
        {
            media = new PostMedia(
                post.Id,
                requesterUserId,
                objectKey,
                safeFileName,
                normalizedType,
                sizeBytes,
                sortOrder);
        }
        catch (DomainException exception)
        {
            return ContentMediaUploadResult.Failure(exception.Message, exception.Message);
        }

        dbContext.PostMedia.Add(media);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ContentMediaUploadResult.Failure("media_persistence_conflict");
        }

        var uploadUrl = storage.CreateUploadUrl(
            media.ObjectKey,
            media.ContentType,
            now,
            out var expiresAt);

        return ContentMediaUploadResult.Success(
            new ContentMediaUploadItem(
                media.Id,
                post.Id,
                media.Status,
                media.ContentType,
                media.ExpectedSizeBytes,
                media.SortOrder,
                uploadUrl,
                expiresAt));
    }

    public async Task<ContentMediaOperationResult> ConfirmMediaUploadAsync(
        Guid requesterUserId,
        Guid postId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || postId == Guid.Empty || mediaId == Guid.Empty)
            return ContentMediaOperationResult.Failure("invalid_media_request");

        if (!storage.IsConfigured)
            return ContentMediaOperationResult.Failure("storage_not_configured");

        var post = await dbContext.Posts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);

        if (post is null)
            return ContentMediaOperationResult.Failure("post_not_found");
        if (!await CanManagePostAsync(requesterUserId, post, cancellationToken))
            return ContentMediaOperationResult.Failure("post_access_denied");
        if (post.Status != PostStatusKeys.Draft)
            return ContentMediaOperationResult.Failure("post_not_draft");

        var media = await dbContext.PostMedia
            .FirstOrDefaultAsync(
                x => x.Id == mediaId && x.PostId == postId,
                cancellationToken);

        if (media is null)
            return ContentMediaOperationResult.Failure("media_not_found");

        if (media.Status == PostMediaStatusKeys.Ready)
        {
            return ContentMediaOperationResult.Success(
                ToMediaItem(media, includeSignedUrl: false),
                wasChanged: false);
        }

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
            return ContentMediaOperationResult.Failure("storage_verification_failed", exception.Message);
        }

        if (metadata is null || signature is null)
            return ContentMediaOperationResult.Failure("media_object_missing");
        if (!HasExpectedSignature(media.ContentType, signature))
            return ContentMediaOperationResult.Failure("media_signature_invalid");

        try
        {
            media.MarkReady(
                metadata.SizeBytes,
                metadata.ContentType ?? string.Empty,
                DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return ContentMediaOperationResult.Failure(exception.Message, exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ContentMediaOperationResult.Success(
            ToMediaItem(media, includeSignedUrl: false),
            wasChanged: true);
    }

    public async Task<ContentPostResult> PublishAsync(
        Guid requesterUserId,
        Guid postId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || postId == Guid.Empty)
            return ContentPostResult.Failure("invalid_post_request");

        var now = at ?? DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var post = await dbContext.Posts
            .Include(x => x.Media)
            .Include(x => x.Placements)
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);

        if (post is null)
            return ContentPostResult.Failure("post_not_found");
        if (!await CanManagePostAsync(requesterUserId, post, cancellationToken))
            return ContentPostResult.Failure("post_access_denied");

        if (post.Status == PostStatusKeys.Published)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ContentPostResult.Success(ToItem(post, includeSignedUrls: true), wasChanged: false);
        }

        if (post.Status != PostStatusKeys.Draft)
            return ContentPostResult.Failure("post_not_publishable");
        if (post.Media.Any(x => x.Status != PostMediaStatusKeys.Ready))
            return ContentPostResult.Failure("post_media_not_ready");

        if (post.MasterUserId.HasValue)
        {
            var usage = await publicationUsageTracker.TrackAsync(
                post.MasterUserId.Value,
                now,
                cancellationToken);

            if (!usage.Succeeded)
                return ContentPostResult.Failure(usage.ErrorCode!);
        }

        try
        {
            post.Publish(
                post.Media
                    .Where(x => x.Status == PostMediaStatusKeys.Ready)
                    .Select(x => x.ContentType)
                    .ToArray(),
                now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ContentPostResult.Failure(exception.Message, exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ContentPostResult.Failure("content_concurrency_conflict");
        }
        catch (PostgresException exception)
            when (exception.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ContentPostResult.Failure("content_concurrency_conflict");
        }

        return ContentPostResult.Success(ToItem(post, includeSignedUrls: true));
    }

    public async Task<ContentPostResult> ArchiveAsync(
        Guid requesterUserId,
        Guid postId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || postId == Guid.Empty)
            return ContentPostResult.Failure("invalid_post_request");

        var post = await dbContext.Posts
            .Include(x => x.Media)
            .Include(x => x.Placements)
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);

        if (post is null)
            return ContentPostResult.Failure("post_not_found");
        if (!await CanManagePostAsync(requesterUserId, post, cancellationToken))
            return ContentPostResult.Failure("post_access_denied");

        if (post.Status == PostStatusKeys.Archived)
            return ContentPostResult.Success(ToItem(post, includeSignedUrls: false), wasChanged: false);

        try
        {
            post.Archive(at ?? DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return ContentPostResult.Failure(exception.Message, exception.Message);
        }

        return ContentPostResult.Success(ToItem(post, includeSignedUrls: false));
    }

    public async Task<ContentPlacementListResult> ListPlacementAsync(
        string placementKey,
        string? cursor = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = placementKey?.Trim().ToLowerInvariant();
        if (!PostPlacementKeys.IsSupported(normalizedKey))
            return ContentPlacementListResult.Failure("post_placement_not_supported");

        limit = Math.Clamp(limit, 1, 50);

        PlacementCursor? decodedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor) && !TryDecodeCursor(cursor, out decodedCursor))
            return ContentPlacementListResult.Failure("invalid_cursor");

        var query = dbContext.Posts
            .AsNoTracking()
            .Include(x => x.Media)
            .Include(x => x.Placements)
            .Where(x =>
                x.Status == PostStatusKeys.Published &&
                x.PublishedAt != null &&
                x.Placements.Any(p => p.PlacementKey == normalizedKey));

        if (decodedCursor is not null)
        {
            var c = decodedCursor;
            query = query.Where(post =>
                post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.Priority).First() < c.Priority
                || (post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.Priority).First() == c.Priority
                    && post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.DisplayOrder).First() > c.DisplayOrder)
                || (post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.Priority).First() == c.Priority
                    && post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.DisplayOrder).First() == c.DisplayOrder
                    && post.PublishedAt < c.PublishedAt)
                || (post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.Priority).First() == c.Priority
                    && post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.DisplayOrder).First() == c.DisplayOrder
                    && post.PublishedAt == c.PublishedAt
                    && post.Id.CompareTo(c.PostId) < 0));
        }

        var posts = await query
            .OrderByDescending(post =>
                post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.Priority).First())
            .ThenBy(post =>
                post.Placements.Where(p => p.PlacementKey == normalizedKey).Select(p => p.DisplayOrder).First())
            .ThenByDescending(post => post.PublishedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = posts.Count > limit;
        if (hasMore)
            posts.RemoveAt(posts.Count - 1);

        var items = posts.Select(post => ToItem(post, includeSignedUrls: true)).ToArray();

        string? nextCursor = null;
        if (hasMore && posts.Count > 0)
        {
            var last = posts[^1];
            var placement = last.Placements.Single(x => x.PlacementKey == normalizedKey);
            nextCursor = EncodeCursor(
                new PlacementCursor(
                    placement.Priority,
                    placement.DisplayOrder,
                    last.PublishedAt!.Value,
                    last.Id));
        }

        return ContentPlacementListResult.Success(new ContentPlacementPage(items, nextCursor));
    }

    private async Task<PublisherActor> LoadPublisherActorAsync(
        Guid requesterUserId,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == requesterUserId && x.Status == UserStatus.Active,
                cancellationToken);

        if (!active)
            return PublisherActor.Denied;

        var roleKeys = await dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == requesterUserId)
            .Select(x => x.Role.Key)
            .ToArrayAsync(cancellationToken);

        var isAdmin = roleKeys.Contains(IdentityRoleKeys.Admin, StringComparer.OrdinalIgnoreCase);
        var isMaster = roleKeys.Contains(IdentityRoleKeys.Master, StringComparer.OrdinalIgnoreCase);

        return new PublisherActor(isAdmin || isMaster, isAdmin, isMaster);
    }

    private async Task<bool> CanManagePostAsync(
        Guid requesterUserId,
        Post post,
        CancellationToken cancellationToken)
    {
        var actor = await LoadPublisherActorAsync(requesterUserId, cancellationToken);
        if (!actor.Allowed)
            return false;
        if (actor.IsAdmin)
            return true;

        return actor.IsMaster && post.MasterUserId == requesterUserId;
    }

    private ContentPostItem ToItem(Post post, bool includeSignedUrls)
    {
        var media = post.Media
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => ToMediaItem(x, includeSignedUrls))
            .ToArray();

        var placements = post.Placements
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new ContentPostPlacementItem(
                x.PlacementKey,
                x.Priority,
                x.DisplayOrder))
            .ToArray();

        return new ContentPostItem(
            post.Id,
            post.PublisherUserId,
            post.MasterUserId,
            post.Type,
            post.Status,
            post.Title,
            post.Body,
            post.LinkUrl,
            post.CreatedAt,
            post.PublishedAt,
            post.ArchivedAt,
            placements,
            media);
    }

    private ContentPostMediaItem ToMediaItem(PostMedia media, bool includeSignedUrl)
    {
        Uri? readUrl = null;
        DateTimeOffset? expiresAt = null;

        if (includeSignedUrl && media.Status == PostMediaStatusKeys.Ready && storage.IsConfigured)
        {
            readUrl = storage.CreateReadUrl(
                media.ObjectKey,
                DateTimeOffset.UtcNow,
                out var expiration);
            expiresAt = expiration;
        }

        return new ContentPostMediaItem(
            media.Id,
            media.Status,
            media.ContentType,
            media.ActualSizeBytes ?? media.ExpectedSizeBytes,
            media.SortOrder,
            readUrl,
            expiresAt);
    }

    private static bool HasExpectedSignature(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            "video/mp4" => bytes.Length >= 8 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
            "video/webm" => bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };

    private static string EncodeCursor(PlacementCursor cursor)
    {
        var raw = string.Join(
            '|',
            cursor.Priority,
            cursor.DisplayOrder,
            cursor.PublishedAt.UtcDateTime.Ticks,
            cursor.PostId.ToString("N"));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string value, out PlacementCursor? cursor)
    {
        cursor = null;

        try
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(
                normalized.Length + ((4 - normalized.Length % 4) % 4),
                '=');

            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            var parts = raw.Split('|');

            if (parts.Length != 4
                || !int.TryParse(parts[0], out var priority)
                || !int.TryParse(parts[1], out var displayOrder)
                || !long.TryParse(parts[2], out var ticks)
                || !Guid.TryParseExact(parts[3], "N", out var postId))
            {
                return false;
            }

            cursor = new PlacementCursor(
                priority,
                displayOrder,
                new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)),
                postId);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed record MediaTypeRule(
        string ObjectExtension,
        IReadOnlyList<string> AllowedExtensions);

    private sealed record PublisherActor(bool Allowed, bool IsAdmin, bool IsMaster)
    {
        public static PublisherActor Denied => new(false, false, false);
    }

    private sealed record PlacementCursor(
        int Priority,
        int DisplayOrder,
        DateTimeOffset PublishedAt,
        Guid PostId);
}
