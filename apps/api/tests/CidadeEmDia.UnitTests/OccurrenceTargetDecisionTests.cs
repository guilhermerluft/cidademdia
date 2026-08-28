using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceTargetDecisionTests
{
    [Fact]
    public void First_master_acceptance_moves_occurrence_from_new_to_received()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var acceptedAt = sentAt.AddMinutes(1);
        var target = occurrence.AddMasterTarget(masterUserId, sentAt);

        var acceptedTarget = occurrence.AcceptMasterTarget(
            target.Id,
            masterUserId,
            acceptedAt);

        Assert.Equal(OccurrenceTargetStatus.Accepted, acceptedTarget.Status);
        Assert.Equal(acceptedAt, acceptedTarget.AcceptedAt);
        Assert.Null(acceptedTarget.RejectedAt);
        Assert.Null(acceptedTarget.RejectionReason);
        Assert.Equal(OccurrenceStatus.Received, occurrence.Status);
        Assert.Equal(2, occurrence.StatusHistory.Count);
        Assert.Equal(OccurrenceStatus.New, occurrence.StatusHistory[1].FromStatus);
        Assert.Equal(OccurrenceStatus.Received, occurrence.StatusHistory[1].ToStatus);
        Assert.Equal(masterUserId, occurrence.StatusHistory[1].ChangedByUserId);
        Assert.Equal(acceptedAt, occurrence.StatusHistory[1].CreatedAt);
    }

    [Fact]
    public void Later_master_acceptance_does_not_repeat_received_transition()
    {
        var occurrence = CreateOccurrence();
        var firstMaster = Guid.NewGuid();
        var secondMaster = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var firstTarget = occurrence.AddMasterTarget(firstMaster, sentAt);
        var secondTarget = occurrence.AddMasterTarget(secondMaster, sentAt.AddSeconds(1));

        occurrence.AcceptMasterTarget(firstTarget.Id, firstMaster, sentAt.AddMinutes(1));
        occurrence.AcceptMasterTarget(secondTarget.Id, secondMaster, sentAt.AddMinutes(2));

        Assert.Equal(OccurrenceTargetStatus.Accepted, firstTarget.Status);
        Assert.Equal(OccurrenceTargetStatus.Accepted, secondTarget.Status);
        Assert.Equal(OccurrenceStatus.Received, occurrence.Status);
        Assert.Equal(2, occurrence.StatusHistory.Count);
    }

    [Fact]
    public void Rejection_keeps_occurrence_new_and_records_required_reason()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var rejectedAt = sentAt.AddMinutes(1);
        var target = occurrence.AddMasterTarget(masterUserId, sentAt);

        var rejectedTarget = occurrence.RejectMasterTarget(
            target.Id,
            masterUserId,
            "  Fora da área de atendimento.  ",
            rejectedAt);

        Assert.Equal(OccurrenceTargetStatus.Rejected, rejectedTarget.Status);
        Assert.Equal("Fora da área de atendimento.", rejectedTarget.RejectionReason);
        Assert.Equal(rejectedAt, rejectedTarget.RejectedAt);
        Assert.Null(rejectedTarget.AcceptedAt);
        Assert.Equal(OccurrenceStatus.New, occurrence.Status);
        Assert.Single(occurrence.StatusHistory);
    }

    [Fact]
    public void Rejection_without_reason_is_blocked()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddMasterTarget(masterUserId, sentAt);

        Assert.Throws<DomainException>(() => occurrence.RejectMasterTarget(
            target.Id,
            masterUserId,
            "   ",
            sentAt.AddMinutes(1)));

        Assert.Equal(OccurrenceTargetStatus.Pending, target.Status);
        Assert.Equal(OccurrenceStatus.New, occurrence.Status);
    }

    [Fact]
    public void Target_decision_is_immutable_after_acceptance_or_rejection()
    {
        var acceptedOccurrence = CreateOccurrence();
        var acceptedMaster = Guid.NewGuid();
        var acceptedSentAt = acceptedOccurrence.CreatedAt.AddMinutes(1);
        var acceptedTarget = acceptedOccurrence.AddMasterTarget(acceptedMaster, acceptedSentAt);
        acceptedOccurrence.AcceptMasterTarget(
            acceptedTarget.Id,
            acceptedMaster,
            acceptedSentAt.AddMinutes(1));

        Assert.Throws<DomainException>(() => acceptedOccurrence.RejectMasterTarget(
            acceptedTarget.Id,
            acceptedMaster,
            "Mudança indevida",
            acceptedSentAt.AddMinutes(2)));

        var rejectedOccurrence = CreateOccurrence();
        var rejectedMaster = Guid.NewGuid();
        var rejectedSentAt = rejectedOccurrence.CreatedAt.AddMinutes(1);
        var rejectedTarget = rejectedOccurrence.AddMasterTarget(rejectedMaster, rejectedSentAt);
        rejectedOccurrence.RejectMasterTarget(
            rejectedTarget.Id,
            rejectedMaster,
            "Sem competência",
            rejectedSentAt.AddMinutes(1));

        Assert.Throws<DomainException>(() => rejectedOccurrence.AcceptMasterTarget(
            rejectedTarget.Id,
            rejectedMaster,
            rejectedSentAt.AddMinutes(2)));
    }

    [Fact]
    public void Master_cannot_decide_another_masters_target()
    {
        var occurrence = CreateOccurrence();
        var assignedMaster = Guid.NewGuid();
        var otherMaster = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddMasterTarget(assignedMaster, sentAt);

        Assert.Throws<DomainException>(() => occurrence.AcceptMasterTarget(
            target.Id,
            otherMaster,
            sentAt.AddMinutes(1)));

        Assert.Equal(OccurrenceTargetStatus.Pending, target.Status);
        Assert.Equal(OccurrenceStatus.New, occurrence.Status);
    }

    [Fact]
    public void Target_decision_cannot_predate_assignment()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddMasterTarget(masterUserId, sentAt);

        Assert.Throws<DomainException>(() => occurrence.AcceptMasterTarget(
            target.Id,
            masterUserId,
            sentAt.AddSeconds(-1)));
    }

    private static Occurrence CreateOccurrence() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Buraco na via",
            "Próximo ao cruzamento.",
            "Rua A, 100",
            new OccurrenceLocation(-30.0346m, -51.2177m),
            postalCode: "90010000",
            stateCode: "RS");
}
