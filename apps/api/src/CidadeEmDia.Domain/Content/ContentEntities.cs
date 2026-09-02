using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Content;

public static class PostTypeKeys
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Video = "video";
    public const string Link = "link";
    public const string Carousel = "carousel";

    public static bool IsSupported(string? value) =>
        value is Text or Image or Video or Link or Carousel;
}

public static class PostStatusKeys
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";
}

public static class PostMediaStatusKeys
{
    public const string PendingUpload = "pending_upload";
    public const string Ready = "ready";
}

public static class PostPlacementKeys
{
    public const string Feed = "feed";
    public const string Horizontal = "horizontal";
    public const string Vertical = "vertical";

    public static bool IsSupported(string? value) =>
        value is Feed or Horizontal or Vertical;
}

public sealed class Post : BaseEntity
{
    private Post() { }

    public Post(
        Guid publisherUserId,
        Guid? masterUserId,
        string type,
        string? title,
        string? body,
        string? linkUrl)
    {
        if (publisherUserId == Guid.Empty)
            throw new DomainException("publisher_user_id_required");

        var normalizedType = type?.Trim().ToLowerInvariant();
        if (!PostTypeKeys.IsSupported(normalizedType))
            throw new DomainException("post_type_not_supported");

        if (title?.Length > 200)
            throw new DomainException("post_title_too_long");
        if (body?.Length > 5000)
            throw new DomainException("post_body_too_long");
        if (linkUrl?.Length > 2048)
            throw new DomainException("post_link_too_long");

        PublisherUserId = publisherUserId;
        MasterUserId = masterUserId;
        Type = normalizedType!;
        Status = PostStatusKeys.Draft;
        Title = NormalizeOptional(title);
        Body = NormalizeOptional(body);
        LinkUrl = NormalizeOptional(linkUrl);
    }

    public Guid PublisherUserId { get; private set; }
    public Guid? MasterUserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Status { get; private set; } = PostStatusKeys.Draft;
    public string? Title { get; private set; }
    public string? Body { get; private set; }
    public string? LinkUrl { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public ICollection<PostMedia> Media { get; private set; } = new List<PostMedia>();
    public ICollection<PostPlacement> Placements { get; private set; } = new List<PostPlacement>();

    public void Publish(
        IReadOnlyCollection<string> readyMediaContentTypes,
        DateTimeOffset publishedAt)
    {
        if (Status == PostStatusKeys.Published)
            return;
        if (Status != PostStatusKeys.Draft)
            throw new DomainException("post_not_publishable");
        if (Placements.Count == 0)
            throw new DomainException("post_placement_required");

        ValidateContentForPublish(readyMediaContentTypes);

        Status = PostStatusKeys.Published;
        PublishedAt = publishedAt;
        ArchivedAt = null;
        Touch();
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status == PostStatusKeys.Archived)
            return;
        if (Status != PostStatusKeys.Published)
            throw new DomainException("post_not_archivable");

        Status = PostStatusKeys.Archived;
        ArchivedAt = archivedAt;
        Touch();
    }

    private void ValidateContentForPublish(
        IReadOnlyCollection<string> readyMediaContentTypes)
    {
        var mediaTypes = readyMediaContentTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToArray();

        switch (Type)
        {
            case PostTypeKeys.Text:
                if (string.IsNullOrWhiteSpace(Body))
                    throw new DomainException("post_body_required");
                break;

            case PostTypeKeys.Link:
                if (!Uri.TryCreate(LinkUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new DomainException("post_link_invalid");
                }
                break;

            case PostTypeKeys.Image:
                if (mediaTypes.Length != 1
                    || !mediaTypes[0].StartsWith("image/", StringComparison.Ordinal))
                {
                    throw new DomainException("post_image_media_required");
                }
                break;

            case PostTypeKeys.Video:
                if (mediaTypes.Length != 1
                    || !mediaTypes[0].StartsWith("video/", StringComparison.Ordinal))
                {
                    throw new DomainException("post_video_media_required");
                }
                break;

            case PostTypeKeys.Carousel:
                if (mediaTypes.Length < 2)
                    throw new DomainException("post_carousel_media_required");
                break;
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed class PostMedia : BaseEntity
{
    private PostMedia() { }

    public PostMedia(
        Guid postId,
        Guid uploaderUserId,
        string objectKey,
        string originalFileName,
        string contentType,
        long expectedSizeBytes,
        int sortOrder)
    {
        if (postId == Guid.Empty)
            throw new DomainException("post_id_required");
        if (uploaderUserId == Guid.Empty)
            throw new DomainException("uploader_user_id_required");
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new DomainException("post_media_object_key_required");
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new DomainException("post_media_file_name_required");
        if (string.IsNullOrWhiteSpace(contentType))
            throw new DomainException("post_media_content_type_required");
        if (expectedSizeBytes <= 0)
            throw new DomainException("post_media_size_invalid");
        if (sortOrder < 0)
            throw new DomainException("post_media_sort_order_invalid");

        PostId = postId;
        UploaderUserId = uploaderUserId;
        ObjectKey = objectKey.Trim();
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        ExpectedSizeBytes = expectedSizeBytes;
        SortOrder = sortOrder;
        Status = PostMediaStatusKeys.PendingUpload;
    }

    public Guid PostId { get; private set; }
    public Guid UploaderUserId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ExpectedSizeBytes { get; private set; }
    public long? ActualSizeBytes { get; private set; }
    public int SortOrder { get; private set; }
    public string Status { get; private set; } = PostMediaStatusKeys.PendingUpload;
    public DateTimeOffset? ReadyAt { get; private set; }

    public Post Post { get; private set; } = null!;

    public void MarkReady(
        long actualSizeBytes,
        string actualContentType,
        DateTimeOffset readyAt)
    {
        if (Status == PostMediaStatusKeys.Ready)
            return;
        if (actualSizeBytes != ExpectedSizeBytes)
            throw new DomainException("post_media_size_mismatch");
        if (!string.Equals(
                ContentType,
                actualContentType?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("post_media_content_type_mismatch");
        }

        ActualSizeBytes = actualSizeBytes;
        Status = PostMediaStatusKeys.Ready;
        ReadyAt = readyAt;
        Touch();
    }
}

public sealed class PostPlacement : BaseEntity
{
    private PostPlacement() { }

    public PostPlacement(
        Guid postId,
        string placementKey,
        int priority,
        int displayOrder)
    {
        if (postId == Guid.Empty)
            throw new DomainException("post_id_required");

        var normalizedKey = placementKey?.Trim().ToLowerInvariant();
        if (!PostPlacementKeys.IsSupported(normalizedKey))
            throw new DomainException("post_placement_not_supported");
        if (priority < 0)
            throw new DomainException("post_priority_invalid");
        if (displayOrder < 0)
            throw new DomainException("post_display_order_invalid");

        PostId = postId;
        PlacementKey = normalizedKey!;
        Priority = priority;
        DisplayOrder = displayOrder;
    }

    public Guid PostId { get; private set; }
    public string PlacementKey { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public int DisplayOrder { get; private set; }

    public Post Post { get; private set; } = null!;
}
