using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceMedia : BaseEntity
{
    private OccurrenceMedia()
    {
        ObjectKey = string.Empty;
        OriginalFileName = string.Empty;
        ContentType = string.Empty;
        Status = OccurrenceMediaStatus.Pending;
    }

    public OccurrenceMedia(
        Guid id,
        Guid uploaderUserId,
        string objectKey,
        string originalFileName,
        string contentType,
        long expectedSizeBytes,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("Occurrence media id is required.");
        if (uploaderUserId == Guid.Empty)
            throw new DomainException("Occurrence media uploader is required.");
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Trim().Length > 500)
            throw new DomainException("Occurrence media object key is required and must contain at most 500 characters.");
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Trim().Length > 255)
            throw new DomainException("Occurrence media original file name is required and must contain at most 255 characters.");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Trim().Length > 120)
            throw new DomainException("Occurrence media content type is required and must contain at most 120 characters.");
        if (expectedSizeBytes <= 0)
            throw new DomainException("Occurrence media size must be greater than zero.");

        Id = id;
        UploaderUserId = uploaderUserId;
        ObjectKey = objectKey.Trim();
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim().ToLowerInvariant();
        ExpectedSizeBytes = expectedSizeBytes;
        Status = OccurrenceMediaStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UploaderUserId { get; private set; }
    public Guid? OccurrenceId { get; private set; }
    public string ObjectKey { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long ExpectedSizeBytes { get; private set; }
    public long? ActualSizeBytes { get; private set; }
    public OccurrenceMediaStatus Status { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? AttachedAt { get; private set; }

    public void MarkReady(long actualSizeBytes, string verifiedContentType, DateTimeOffset readyAt)
    {
        if (Status == OccurrenceMediaStatus.Ready)
            return;
        if (actualSizeBytes != ExpectedSizeBytes)
            throw new DomainException("Uploaded occurrence media size does not match the declared size.");
        if (!string.Equals(ContentType, verifiedContentType?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Uploaded occurrence media content type does not match the declared content type.");

        ActualSizeBytes = actualSizeBytes;
        ReadyAt = readyAt;
        Status = OccurrenceMediaStatus.Ready;
        UpdatedAt = readyAt;
    }

    public void AttachToOccurrence(Guid occurrenceId, DateTimeOffset attachedAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Occurrence is required to attach media.");
        if (Status != OccurrenceMediaStatus.Ready)
            throw new DomainException("Only ready occurrence media can be attached.");
        if (OccurrenceId.HasValue && OccurrenceId.Value != occurrenceId)
            throw new DomainException("Occurrence media is already attached to another occurrence.");

        OccurrenceId = occurrenceId;
        AttachedAt ??= attachedAt;
        UpdatedAt = attachedAt;
    }
}
