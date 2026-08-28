using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceTarget : BaseEntity
{
    private OccurrenceTarget()
    {
    }

    internal OccurrenceTarget(Guid occurrenceId, Guid masterUserId, DateTimeOffset sentAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Occurrence target occurrence is required.");
        if (masterUserId == Guid.Empty)
            throw new DomainException("Occurrence target Master is required.");

        OccurrenceId = occurrenceId;
        MasterUserId = masterUserId;
        Status = OccurrenceTargetStatus.Pending;
        SentAt = sentAt;
    }

    public Guid OccurrenceId { get; private set; }
    public Guid MasterUserId { get; private set; }
    public OccurrenceTargetStatus Status { get; private set; } = OccurrenceTargetStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public Occurrence Occurrence { get; private set; } = null!;
    public User MasterUser { get; private set; } = null!;
}
