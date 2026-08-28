using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class Occurrence : BaseEntity
{
    public const int MaxTargetsPerOccurrence = 3;

    private readonly List<OccurrenceStatusChange> _statusHistory = [];
    private readonly List<OccurrenceComplement> _complements = [];
    private readonly List<OccurrenceServiceForecast> _serviceForecastHistory = [];
    private readonly List<OccurrenceTarget> _targets = [];

    private Occurrence()
    {
    }

    public Occurrence(
        Guid authorUserId,
        Guid categoryId,
        string title,
        string? description,
        string addressText,
        OccurrenceLocation location,
        string? postalCode = null,
        Guid? cityId = null,
        string? stateCode = null,
        string? externalProtocolNumber = null,
        string? externalProtocolAgency = null)
    {
        if (authorUserId == Guid.Empty)
            throw new DomainException("Occurrence author is required.");
        if (categoryId == Guid.Empty)
            throw new DomainException("Occurrence category is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Occurrence title is required.");
        if (string.IsNullOrWhiteSpace(addressText))
            throw new DomainException("Occurrence address is required.");

        AuthorUserId = authorUserId;
        CategoryId = categoryId;
        Title = title.Trim();
        Description = NormalizeOptionalText(description);
        AddressText = addressText.Trim();
        Location = location ?? throw new DomainException("Occurrence location is required.");
        PostalCode = NormalizePostalCode(postalCode);
        CityId = cityId;
        StateCode = NormalizeStateCode(stateCode);
        ExternalProtocolNumber = NormalizeOptionalText(externalProtocolNumber);
        ExternalProtocolAgency = NormalizeOptionalText(externalProtocolAgency);
        PublicCode = OccurrencePublicCode.New();
        Status = OccurrenceStatus.New;

        _statusHistory.Add(new OccurrenceStatusChange(
            fromStatus: null,
            toStatus: Status,
            changedByUserId: authorUserId,
            createdAt: CreatedAt,
            reason: null));
    }

    public Guid AuthorUserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public OccurrencePublicCode PublicCode { get; private set; } = null!;
    public string? ExternalProtocolNumber { get; private set; }
    public string? ExternalProtocolAgency { get; private set; }
    public OccurrenceStatus Status { get; private set; } = OccurrenceStatus.New;
    public string? PostalCode { get; private set; }
    public string AddressText { get; private set; } = string.Empty;
    public Guid? CityId { get; private set; }
    public string? StateCode { get; private set; }
    public OccurrenceLocation Location { get; private set; } = null!;
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public IReadOnlyList<OccurrenceStatusChange> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyList<OccurrenceComplement> Complements => _complements.AsReadOnly();
    public IReadOnlyList<OccurrenceServiceForecast> ServiceForecastHistory => _serviceForecastHistory.AsReadOnly();
    public IReadOnlyList<OccurrenceTarget> Targets => _targets.AsReadOnly();
    public DateTimeOffset? CurrentServiceForecast => _serviceForecastHistory.LastOrDefault()?.EstimatedFor;

    public OccurrenceTarget AddMasterTarget(Guid masterUserId, DateTimeOffset sentAt)
    {
        EnsureNotBeforeCreation(sentAt);

        if (_targets.Any(target => target.MasterUserId == masterUserId))
            throw new DomainException("Occurrence is already shared with this Master.");

        if (_targets.Count >= MaxTargetsPerOccurrence)
            throw new DomainException($"Occurrence cannot have more than {MaxTargetsPerOccurrence} targets.");

        var target = new OccurrenceTarget(Id, masterUserId, sentAt);
        _targets.Add(target);
        Touch();
        return target;
    }

    public OccurrenceTarget AcceptMasterTarget(
        Guid targetId,
        Guid masterUserId,
        DateTimeOffset acceptedAt)
    {
        var target = FindAssignedTarget(targetId, masterUserId);
        target.Accept(acceptedAt);

        if (Status == OccurrenceStatus.New)
        {
            TransitionTo(
                OccurrenceStatus.Received,
                masterUserId,
                acceptedAt,
                "Occurrence received after acceptance by an assigned Master.");
        }
        else
        {
            Touch();
        }

        return target;
    }

    public OccurrenceTarget RejectMasterTarget(
        Guid targetId,
        Guid masterUserId,
        string rejectionReason,
        DateTimeOffset rejectedAt)
    {
        var target = FindAssignedTarget(targetId, masterUserId);
        target.Reject(rejectionReason, rejectedAt);
        Touch();
        return target;
    }

    public void TransitionTo(
        OccurrenceStatus targetStatus,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        string? reason = null)
    {
        if (targetStatus is null)
            throw new DomainException("Target occurrence status is required.");
        if (changedByUserId == Guid.Empty)
            throw new DomainException("Status change actor is required.");
        if (targetStatus == Status)
            throw new DomainException("Occurrence is already in the requested status.");
        if (Status.IsTerminal)
            throw new DomainException($"Occurrence in status '{Status.Value}' cannot transition to another status.");

        EnsureNotBeforeCreation(changedAt);

        var previousStatus = Status;
        Status = targetStatus;

        if (targetStatus == OccurrenceStatus.Closed)
            ClosedAt = changedAt;
        else if (targetStatus == OccurrenceStatus.Cancelled)
            CancelledAt = changedAt;

        _statusHistory.Add(new OccurrenceStatusChange(
            previousStatus,
            targetStatus,
            changedByUserId,
            changedAt,
            reason));

        Touch();
    }

    public OccurrenceComplement AddComplement(
        Guid authorUserId,
        string content,
        DateTimeOffset createdAt)
    {
        EnsureNotBeforeCreation(createdAt);

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
        EnsureNotBeforeCreation(definedAt);

        var forecast = new OccurrenceServiceForecast(
            estimatedFor,
            definedByUserId,
            definedAt,
            note);

        _serviceForecastHistory.Add(forecast);
        Touch();
        return forecast;
    }

    private OccurrenceTarget FindAssignedTarget(Guid targetId, Guid masterUserId)
    {
        if (targetId == Guid.Empty)
            throw new DomainException("Occurrence target is required.");
        if (masterUserId == Guid.Empty)
            throw new DomainException("Occurrence target Master is required.");

        return _targets.FirstOrDefault(target =>
                target.Id == targetId && target.MasterUserId == masterUserId)
            ?? throw new DomainException("Occurrence target is not assigned to this Master.");
    }

    private void EnsureNotBeforeCreation(DateTimeOffset eventAt)
    {
        if (eventAt < CreatedAt)
            throw new DomainException("Occurrence events cannot predate the occurrence creation.");
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePostalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length != 8)
            throw new DomainException("Occurrence postal code must contain 8 digits.");

        return digits;
    }

    private static string? NormalizeStateCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length != 2 || normalized.Any(character => !char.IsLetter(character)))
            throw new DomainException("Occurrence state code must contain 2 letters.");

        return normalized;
    }
}
