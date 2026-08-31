using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceTargetAssignment : BaseEntity
{
    private OccurrenceTargetAssignment()
    {
    }

    public OccurrenceTargetAssignment(
        Guid occurrenceTargetId,
        Guid masterSubaccountId,
        Guid assignedByMasterUserId,
        DateTimeOffset assignedAt)
    {
        if (occurrenceTargetId == Guid.Empty)
            throw new DomainException("Occurrence target is required for assignment.");
        if (masterSubaccountId == Guid.Empty)
            throw new DomainException("Master subaccount link is required for assignment.");
        if (assignedByMasterUserId == Guid.Empty)
            throw new DomainException("Assigning Master is required.");

        OccurrenceTargetId = occurrenceTargetId;
        MasterSubaccountId = masterSubaccountId;
        AssignedByMasterUserId = assignedByMasterUserId;
        AssignedAt = assignedAt;
    }

    public Guid OccurrenceTargetId { get; private set; }
    public Guid MasterSubaccountId { get; private set; }
    public Guid AssignedByMasterUserId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public OccurrenceTarget OccurrenceTarget { get; private set; } = null!;
    public MasterSubaccount MasterSubaccount { get; private set; } = null!;
    public User AssignedByMasterUser { get; private set; } = null!;

    public void Reassign(
        Guid masterSubaccountId,
        Guid assignedByMasterUserId,
        DateTimeOffset assignedAt)
    {
        if (masterSubaccountId == Guid.Empty)
            throw new DomainException("Master subaccount link is required for assignment.");
        if (assignedByMasterUserId == Guid.Empty)
            throw new DomainException("Assigning Master is required.");

        MasterSubaccountId = masterSubaccountId;
        AssignedByMasterUserId = assignedByMasterUserId;
        AssignedAt = assignedAt;
        Touch();
    }
}
