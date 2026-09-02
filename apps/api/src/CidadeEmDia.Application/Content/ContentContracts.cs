namespace CidadeEmDia.Application.Content;

public sealed record ContentPlacementInput(
    string PlacementKey,
    int Priority = 0,
    int DisplayOrder = 0);

public sealed record CreatePostDraftCommand(
    string Type,
    string? Title,
    string? Body,
    string? LinkUrl,
    IReadOnlyCollection<ContentPlacementInput> Placements);

public sealed record ContentPostMediaItem(
    Guid Id,
    string Status,
    string ContentType,
    long SizeBytes,
    int SortOrder,
    Uri? ReadUrl = null,
    DateTimeOffset? ReadUrlExpiresAt = null);

public sealed record ContentPostPlacementItem(
    string PlacementKey,
    int Priority,
    int DisplayOrder);

public sealed record ContentPostItem(
    Guid Id,
    Guid PublisherUserId,
    Guid? MasterUserId,
    string Type,
    string Status,
    string? Title,
    string? Body,
    string? LinkUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyCollection<ContentPostPlacementItem> Placements,
    IReadOnlyCollection<ContentPostMediaItem> Media);

public sealed record ContentPostResult(
    bool Succeeded,
    ContentPostItem? Post = null,
    bool WasChanged = false,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static ContentPostResult Success(
        ContentPostItem post,
        bool wasChanged = true) =>
        new(true, post, wasChanged);

    public static ContentPostResult Failure(
        string errorCode,
        string? errorDetail = null) =>
        new(false, null, false, errorCode, errorDetail);
}

public sealed record ContentMediaUploadItem(
    Guid Id,
    Guid PostId,
    string Status,
    string ContentType,
    long ExpectedSizeBytes,
    int SortOrder,
    Uri UploadUrl,
    DateTimeOffset UploadUrlExpiresAt);

public sealed record ContentMediaUploadResult(
    bool Succeeded,
    ContentMediaUploadItem? Upload = null,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static ContentMediaUploadResult Success(
        ContentMediaUploadItem upload) =>
        new(true, upload);

    public static ContentMediaUploadResult Failure(
        string errorCode,
        string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ContentMediaOperationResult(
    bool Succeeded,
    ContentPostMediaItem? Media = null,
    bool WasChanged = false,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static ContentMediaOperationResult Success(
        ContentPostMediaItem media,
        bool wasChanged) =>
        new(true, media, wasChanged);

    public static ContentMediaOperationResult Failure(
        string errorCode,
        string? errorDetail = null) =>
        new(false, null, false, errorCode, errorDetail);
}

public sealed record ContentPlacementPage(
    IReadOnlyCollection<ContentPostItem> Items,
    string? NextCursor);

public sealed record ContentPlacementListResult(
    bool Succeeded,
    ContentPlacementPage? Page = null,
    string? ErrorCode = null)
{
    public static ContentPlacementListResult Success(
        ContentPlacementPage page) =>
        new(true, page);

    public static ContentPlacementListResult Failure(
        string errorCode) =>
        new(false, null, errorCode);
}

public sealed record ContentManagedPostPage(
    IReadOnlyCollection<ContentPostItem> Items,
    int Page,
    int PageSize,
    int TotalItems);

public sealed record ContentManagedPostListResult(
    bool Succeeded,
    ContentManagedPostPage? Page = null,
    string? ErrorCode = null)
{
    public static ContentManagedPostListResult Success(
        ContentManagedPostPage page) =>
        new(true, page);

    public static ContentManagedPostListResult Failure(
        string errorCode) =>
        new(false, null, errorCode);
}

public interface IContentService
{
    Task<ContentPostResult> CreateDraftAsync(
        Guid requesterUserId,
        CreatePostDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<ContentManagedPostListResult> ListManagedAsync(
        Guid requesterUserId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ContentMediaUploadResult> RequestMediaUploadAsync(
        Guid requesterUserId,
        Guid postId,
        string fileName,
        string contentType,
        long sizeBytes,
        int sortOrder,
        CancellationToken cancellationToken = default);

    Task<ContentMediaOperationResult> ConfirmMediaUploadAsync(
        Guid requesterUserId,
        Guid postId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<ContentPostResult> PublishAsync(
        Guid requesterUserId,
        Guid postId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<ContentPostResult> ArchiveAsync(
        Guid requesterUserId,
        Guid postId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default);

    Task<ContentPlacementListResult> ListPlacementAsync(
        string placementKey,
        string? cursor = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
