using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceServiceForecast
{
    private OccurrenceServiceForecast()
    {
    }

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

    public Guid Id { get; private set; }
    public DateTimeOffset EstimatedFor { get; private set; }
    public Guid DefinedByUserId { get; private set; }
    public DateTimeOffset DefinedAt { get; private set; }
    public string? Note { get; private set; }
}
