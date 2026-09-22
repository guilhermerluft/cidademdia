using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Institutions;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceTarget : BaseEntity
{
    public const int MaxAddresseeLength = 180;

    private OccurrenceTarget()
    {
    }

    internal OccurrenceTarget(
        Guid occurrenceId,
        Guid? masterUserId,
        Guid? institutionId,
        string? addressee,
        DateTimeOffset sentAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Occurrence target occurrence is required.");

        var hasMaster = masterUserId.HasValue && masterUserId.Value != Guid.Empty;
        var hasInstitution = institutionId.HasValue && institutionId.Value != Guid.Empty;
        if (hasMaster == hasInstitution)
            throw new DomainException("Occurrence target must reference exactly one initial destination.");

        OccurrenceId = occurrenceId;
        MasterUserId = hasMaster ? masterUserId : null;
        InstitutionId = hasInstitution ? institutionId : null;
        Addressee = NormalizeAddressee(addressee);
        Status = OccurrenceTargetStatus.Pending;
        SentAt = sentAt;
    }

    public Guid OccurrenceId { get; private set; }
    public Guid? MasterUserId { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public string? Addressee { get; private set; }
    public OccurrenceTargetStatus Status { get; private set; } = OccurrenceTargetStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public Occurrence Occurrence { get; private set; } = null!;
    public User? MasterUser { get; private set; }
    public Institution? Institution { get; private set; }

    internal void ClaimByMaster(Guid masterUserId)
    {
        if (masterUserId == Guid.Empty)
            throw new DomainException("Occurrence target Master is required.");

        if (MasterUserId.HasValue && MasterUserId.Value != masterUserId)
            throw new DomainException("Occurrence target was already claimed by another Master.");

        MasterUserId = masterUserId;
        Touch();
    }

    internal void Accept(DateTimeOffset acceptedAt)
    {
        EnsurePendingDecision();
        EnsureDecisionNotBeforeSentAt(acceptedAt);

        if (!MasterUserId.HasValue)
            throw new DomainException("Occurrence target must be claimed by a Master before acceptance.");

        Status = OccurrenceTargetStatus.Accepted;
        AcceptedAt = acceptedAt;
        RejectedAt = null;
        RejectionReason = null;
        Touch();
    }

    internal void Reject(string rejectionReason, DateTimeOffset rejectedAt)
    {
        EnsurePendingDecision();
        EnsureDecisionNotBeforeSentAt(rejectedAt);

        if (!MasterUserId.HasValue)
            throw new DomainException("Occurrence target must be claimed by a Master before rejection.");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new DomainException("Occurrence target rejection reason is required.");

        var normalizedReason = rejectionReason.Trim();
        if (normalizedReason.Length > 1000)
            throw new DomainException("Occurrence target rejection reason must contain at most 1000 characters.");

        Status = OccurrenceTargetStatus.Rejected;
        RejectionReason = normalizedReason;
        RejectedAt = rejectedAt;
        AcceptedAt = null;
        Touch();
    }

    private static string? NormalizeAddressee(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > MaxAddresseeLength)
            throw new DomainException($"Occurrence target addressee must contain at most {MaxAddresseeLength} characters.");
        return normalized;
    }

    private void EnsurePendingDecision()
    {
        if (Status != OccurrenceTargetStatus.Pending)
            throw new DomainException("Occurrence target has already been decided.");
    }

    private void EnsureDecisionNotBeforeSentAt(DateTimeOffset decidedAt)
    {
        if (decidedAt < SentAt)
            throw new DomainException("Occurrence target decision cannot predate target assignment.");
    }
}
