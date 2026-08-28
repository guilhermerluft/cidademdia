using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceStatusChange
{
    internal OccurrenceStatusChange(
        OccurrenceStatus status,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        string? note)
    {
        if (changedByUserId == Guid.Empty)
            throw new DomainException("Status change actor is required.");

        Id = Guid.NewGuid();
        Status = status ?? throw new DomainException("Occurrence status is required.");
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; }
    public OccurrenceStatus Status { get; }
    public Guid ChangedByUserId { get; }
    public DateTimeOffset ChangedAt { get; }
    public string? Note { get; }
}
