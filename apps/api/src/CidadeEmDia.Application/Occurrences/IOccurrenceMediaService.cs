namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceMediaService
{
    Task<OccurrenceMediaUploadResult> RequestUploadAsync(
        Guid requesterUserId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<OccurrenceMediaOperationResult> ConfirmUploadAsync(
        Guid requesterUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceMediaListResult> ListForOccurrenceAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceMediaReadUrlResult> GetReadUrlAsync(
        Guid requesterUserId,
        Guid mediaId,
        CancellationToken cancellationToken = default);
}

public sealed record OccurrenceMediaUploadItem(
    Guid Id,
    string Status,
    string ContentType,
    long ExpectedSizeBytes,
    Uri UploadUrl,
    DateTimeOffset UploadUrlExpiresAt);

public sealed record OccurrenceMediaItem(
    Guid Id,
    Guid? OccurrenceId,
    string Status,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? AttachedAt);

public sealed record OccurrenceMediaReadUrlItem(
    Guid Id,
    Uri ReadUrl,
    DateTimeOffset ReadUrlExpiresAt);

public sealed record OccurrenceMediaUploadResult(
    bool Succeeded,
    OccurrenceMediaUploadItem? Upload,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static OccurrenceMediaUploadResult Success(OccurrenceMediaUploadItem upload) =>
        new(true, upload);

    public static OccurrenceMediaUploadResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record OccurrenceMediaOperationResult(
    bool Succeeded,
    bool WasChanged,
    OccurrenceMediaItem? Media,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static OccurrenceMediaOperationResult Success(OccurrenceMediaItem media, bool wasChanged) =>
        new(true, wasChanged, media);

    public static OccurrenceMediaOperationResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, false, null, errorCode, errorDetail);
}

public sealed record OccurrenceMediaListResult(
    bool Succeeded,
    IReadOnlyList<OccurrenceMediaItem>? Media,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static OccurrenceMediaListResult Success(IReadOnlyList<OccurrenceMediaItem> media) =>
        new(true, media);

    public static OccurrenceMediaListResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record OccurrenceMediaReadUrlResult(
    bool Succeeded,
    OccurrenceMediaReadUrlItem? Media,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static OccurrenceMediaReadUrlResult Success(OccurrenceMediaReadUrlItem media) =>
        new(true, media);

    public static OccurrenceMediaReadUrlResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}
