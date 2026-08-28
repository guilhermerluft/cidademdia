namespace CidadeEmDia.Application.Occurrences;

public sealed record OccurrenceTargetDecisionItem(
    Guid TargetId,
    Guid OccurrenceId,
    Guid MasterUserId,
    string OccurrenceStatus,
    string TargetStatus,
    string? RejectionReason,
    DateTimeOffset SentAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? ClosedAt);

public sealed record OccurrenceTargetDecisionResult(
    bool Succeeded,
    OccurrenceTargetDecisionItem? Decision,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceTargetDecisionResult Success(OccurrenceTargetDecisionItem decision) =>
        new(true, decision, null, null);

    public static OccurrenceTargetDecisionResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}
