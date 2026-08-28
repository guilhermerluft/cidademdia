namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceTargetDecisionService
{
    Task<OccurrenceTargetDecisionResult> AcceptAsync(
        Guid masterUserId,
        Guid occurrenceId,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceTargetDecisionResult> RejectAsync(
        Guid masterUserId,
        Guid occurrenceId,
        Guid targetId,
        string rejectionReason,
        CancellationToken cancellationToken = default);

    Task<OccurrenceTargetDecisionItem?> GetAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        Guid targetId,
        CancellationToken cancellationToken = default);
}
