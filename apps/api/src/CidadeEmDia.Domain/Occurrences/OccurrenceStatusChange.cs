using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceStatusChange
{
    private OccurrenceStatusChange()
    {
    }

    internal OccurrenceStatusChange(
        OccurrenceStatus? fromStatus,
        OccurrenceStatus toStatus,
        Guid changedByUserId,
        DateTimeOffset createdAt,
        string? reason)
    {
        if (changedByUserId == Guid.Empty)
            throw new DomainException("Status change actor is required.");

        Id = Guid.NewGuid();
        FromStatus = fromStatus;
        ToStatus = toStatus ?? throw new DomainException("Target occurrence status is required.");
        ChangedByUserId = changedByUserId;
        CreatedAt = createdAt;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public Guid Id { get; private set; }
    public OccurrenceStatus? FromStatus { get; private set; }
    public OccurrenceStatus ToStatus { get; private set; } = OccurrenceStatus.New;
    public Guid ChangedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Reason { get; private set; }
}
