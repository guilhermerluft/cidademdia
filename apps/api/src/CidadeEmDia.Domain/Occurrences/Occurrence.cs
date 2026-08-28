using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class Occurrence : BaseEntity
{
    private readonly List<OccurrenceStatusChange> _statusHistory = [];
    private readonly List<OccurrenceComplement> _complements = [];
    private readonly List<OccurrenceServiceForecast> _serviceForecastHistory = [];

    private Occurrence()
    {
    }

    public Occurrence(
        Guid authorUserId,
        string title,
        string? description,
        OccurrenceType type,
        OccurrenceLocation location,
        DateTimeOffset registeredAt)
    {
        if (authorUserId == Guid.Empty)
            throw new DomainException("Occurrence author is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Occurrence title is required.");

        AuthorUserId = authorUserId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Type = type ?? throw new DomainException("Occurrence type is required.");
        Location = location ?? throw new DomainException("Occurrence location is required.");
        PublicCode = OccurrencePublicCode.New();
        Status = OccurrenceStatus.Open;
        RegisteredAt = registeredAt;

        _statusHistory.Add(new OccurrenceStatusChange(Status, authorUserId, registeredAt, null));
    }

    public Guid AuthorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public OccurrenceType Type { get; private set; } = null!;
    public OccurrenceLocation Location { get; private set; } = null!;
    public OccurrencePublicCode PublicCode { get; private set; } = null!;
    public OccurrenceStatus Status { get; private set; } = OccurrenceStatus.Open;
    public DateTimeOffset RegisteredAt { get; private set; }

    public IReadOnlyList<OccurrenceStatusChange> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyList<OccurrenceComplement> Complements => _complements.AsReadOnly();
    public IReadOnlyList<OccurrenceServiceForecast> ServiceForecastHistory => _serviceForecastHistory.AsReadOnly();
    public DateTimeOffset? CurrentServiceForecast => _serviceForecastHistory.LastOrDefault()?.EstimatedFor;

    public void TransitionTo(
        OccurrenceStatus targetStatus,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        string? note = null)
    {
        if (targetStatus is null)
            throw new DomainException("Target occurrence status is required.");
        if (changedByUserId == Guid.Empty)
            throw new DomainException("Status change actor is required.");

        EnsureNotBeforeRegistration(changedAt);

        if (!CanTransition(Status, targetStatus))
        {
            throw new DomainException(
                $"Occurrence cannot transition from '{Status.Value}' to '{targetStatus.Value}'.");
        }

        Status = targetStatus;
        _statusHistory.Add(new OccurrenceStatusChange(targetStatus, changedByUserId, changedAt, note));
        Touch();
    }

    public OccurrenceComplement AddComplement(
        Guid authorUserId,
        string content,
        DateTimeOffset createdAt)
    {
        EnsureNotBeforeRegistration(createdAt);

        var complement = new OccurrenceComplement(authorUserId, content, createdAt);
        _complements.Add(complement);
        Touch();
        return complement;
    }

    public OccurrenceServiceForecast SetServiceForecast(
        DateTimeOffset estimatedFor,
        Guid definedByUserId,
        DateTimeOffset definedAt,
        string? note = null)
    {
        EnsureNotBeforeRegistration(definedAt);

        var forecast = new OccurrenceServiceForecast(
            estimatedFor,
            definedByUserId,
            definedAt,
            note);

        _serviceForecastHistory.Add(forecast);
        Touch();
        return forecast;
    }

    private void EnsureNotBeforeRegistration(DateTimeOffset eventAt)
    {
        if (eventAt < RegisteredAt)
            throw new DomainException("Occurrence events cannot predate the occurrence registration.");
    }

    private static bool CanTransition(
        OccurrenceStatus currentStatus,
        OccurrenceStatus targetStatus)
    {
        if (currentStatus == OccurrenceStatus.Open)
        {
            return targetStatus == OccurrenceStatus.InProgress
                || targetStatus == OccurrenceStatus.Cancelled;
        }

        if (currentStatus == OccurrenceStatus.InProgress)
            return targetStatus == OccurrenceStatus.Resolved;

        return false;
    }
}
