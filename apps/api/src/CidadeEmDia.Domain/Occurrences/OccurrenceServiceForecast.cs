using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceServiceForecast
{
    internal OccurrenceServiceForecast(
        DateTimeOffset estimatedFor,
        Guid definedByUserId,
        DateTimeOffset definedAt,
        string? note)
    {
        if (definedByUserId == Guid.Empty)
            throw new DomainException("Forecast actor is required.");
        if (estimatedFor <= definedAt)
            throw new DomainException("Occurrence service forecast must be in the future.");

        Id = Guid.NewGuid();
        EstimatedFor = estimatedFor;
        DefinedByUserId = definedByUserId;
        DefinedAt = definedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; }
    public DateTimeOffset EstimatedFor { get; }
    public Guid DefinedByUserId { get; }
    public DateTimeOffset DefinedAt { get; }
    public string? Note { get; }
}
