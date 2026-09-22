using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceTarget : BaseEntity
{
    public const int MaxAddresseeLength = 180;

    private OccurrenceTarget()
    {
    }

    internal OccurrenceTarget(
        Guid occurrenceId,
        Guid masterUserId,
        string? addressee,
        DateTimeOffset sentAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Occurrence target occurrence is required.");
        if (masterUserId == Guid.Empty)
            throw new DomainException("Occurrence target Master is required.");

        OccurrenceId = occurrenceId;
        MasterUserId = masterUserId;
        Addressee = NormalizeAddressee(addressee);
        Status = OccurrenceTargetStatus.Pending;
        SentAt = sentAt;
    }

    public Guid OccurrenceId { get; private set; }
    public Guid MasterUserId { get; private set; }
    public string? Addressee { get; private set; }
    public OccurrenceTargetStatus Status { get; private set; } = OccurrenceTargetStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public Occurrence Occurrence { get; private set; } = null!;
    public User MasterUser { get; private set; } = null!;

    internal void Accept(DateTimeOffset acceptedAt)
    {
        EnsurePendingDecision();
        EnsureDecisionNotBeforeSentAt(acceptedAt);

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
