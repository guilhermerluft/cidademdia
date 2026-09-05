namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceAssignmentService
{
    Task<IReadOnlyList<MasterOccurrenceTargetItem>?> ListMasterTargetsAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignedOccurrenceItem>?> ListAssignedAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceAssignmentResult> AssignAsync(
        Guid masterUserId,
        Guid targetId,
        Guid masterSubaccountId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceAssignmentResult> UnassignAsync(
        Guid masterUserId,
        Guid targetId,
        CancellationToken cancellationToken = default);
}

public sealed record OccurrenceAssignmentItem(
    Guid AssignmentId,
    Guid TargetId,
    Guid OccurrenceId,
    Guid MasterUserId,
    Guid MasterSubaccountId,
    Guid SubaccountUserId,
    string SubaccountDisplayName,
    DateTimeOffset AssignedAt);

public sealed record MasterOccurrenceTargetItem(
    Guid TargetId,
    Guid OccurrenceId,
    string PublicCode,
    string Title,
    string AddressText,
    string OccurrenceStatus,
    string TargetStatus,
    DateTimeOffset UpdatedAt,
    PublicOccurrenceMediaItem? CoverMedia,
    OccurrenceAssignmentItem? Assignment);

public sealed record AssignedOccurrenceItem(
    Guid AssignmentId,
    Guid TargetId,
    Guid OccurrenceId,
    Guid MasterUserId,
    Guid MasterSubaccountId,
    string PublicCode,
    string Title,
    string AddressText,
    string OccurrenceStatus,
    string TargetStatus,
    bool CanChangeStatus,
    DateTimeOffset AssignedAt,
    DateTimeOffset UpdatedAt);

public sealed record OccurrenceAssignmentResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorDetail,
    OccurrenceAssignmentItem? Assignment)
{
    public static OccurrenceAssignmentResult Success(OccurrenceAssignmentItem? assignment) =>
        new(true, null, null, assignment);

    public static OccurrenceAssignmentResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, errorCode, errorDetail, null);
}
