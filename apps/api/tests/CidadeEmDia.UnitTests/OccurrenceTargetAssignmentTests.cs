using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceTargetAssignmentTests
{
    [Fact]
    public void Assignment_tracks_target_link_and_assigning_master()
    {
        var targetId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var assignedAt = DateTimeOffset.UtcNow;

        var assignment = new OccurrenceTargetAssignment(targetId, linkId, masterId, assignedAt);

        Assert.Equal(targetId, assignment.OccurrenceTargetId);
        Assert.Equal(linkId, assignment.MasterSubaccountId);
        Assert.Equal(masterId, assignment.AssignedByMasterUserId);
        Assert.Equal(assignedAt, assignment.AssignedAt);
    }

    [Fact]
    public void Assignment_can_be_reassigned_to_another_subaccount_link()
    {
        var assignment = new OccurrenceTargetAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var nextLinkId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var reassignedAt = DateTimeOffset.UtcNow;

        assignment.Reassign(nextLinkId, masterId, reassignedAt);

        Assert.Equal(nextLinkId, assignment.MasterSubaccountId);
        Assert.Equal(masterId, assignment.AssignedByMasterUserId);
        Assert.Equal(reassignedAt, assignment.AssignedAt);
    }

    [Fact]
    public void Assignment_rejects_empty_references()
    {
        Assert.Throws<DomainException>(() => new OccurrenceTargetAssignment(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }
}
