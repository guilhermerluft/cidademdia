namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceLifecycleService
{
    Task<OccurrenceLifecycleResult> ChangeStatusAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        string targetStatus,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<OccurrenceLifecycleResult> CancelAsync(
        Guid authorUserId,
        Guid occurrenceId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<OccurrenceDeleteResult> DeleteAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);
}

public sealed record OccurrenceLifecycleItem(
    Guid Id,
    string Status,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt);

public sealed record OccurrenceLifecycleResult(
    bool Succeeded,
    OccurrenceLifecycleItem? Occurrence,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceLifecycleResult Success(OccurrenceLifecycleItem occurrence) =>
        new(true, occurrence, null, null);

    public static OccurrenceLifecycleResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record OccurrenceDeleteResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceDeleteResult Success() => new(true, null, null);

    public static OccurrenceDeleteResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, errorCode, errorDetail);
}
