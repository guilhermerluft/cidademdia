using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceLifecycleTests
{
    [Fact]
    public void Confirmed_status_flow_requires_each_adjacent_transition()
    {
        var occurrence = CreateOccurrence();
        var actor = Guid.NewGuid();
        var timestamp = occurrence.CreatedAt;

        occurrence.TransitionTo(OccurrenceStatus.Received, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.UnderReview, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.InProgress, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.AwaitingInformation, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.Resolved, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.Closed, actor, timestamp = timestamp.AddMinutes(1));

        Assert.Equal(OccurrenceStatus.Closed, occurrence.Status);
        Assert.Equal(7, occurrence.StatusHistory.Count);
        Assert.Equal(timestamp, occurrence.ClosedAt);
    }

    [Fact]
    public void Status_flow_rejects_skipping_confirmed_steps()
    {
        var occurrence = CreateOccurrence();
        var actor = Guid.NewGuid();

        Assert.Throws<DomainException>(() => occurrence.TransitionTo(
            OccurrenceStatus.UnderReview,
            actor,
            occurrence.CreatedAt.AddMinutes(1)));

        occurrence.TransitionTo(
            OccurrenceStatus.Received,
            actor,
            occurrence.CreatedAt.AddMinutes(2));

        Assert.Throws<DomainException>(() => occurrence.TransitionTo(
            OccurrenceStatus.InProgress,
            actor,
            occurrence.CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void Author_can_cancel_only_before_any_master_assignment()
    {
        var author = Guid.NewGuid();
        var occurrence = CreateOccurrence(author);
        var cancelledAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.CancelByAuthor(author, cancelledAt, "Solicitação retirada pelo cidadão");

        Assert.Equal(OccurrenceStatus.Cancelled, occurrence.Status);
        Assert.Equal(cancelledAt, occurrence.CancelledAt);
        Assert.Equal(2, occurrence.StatusHistory.Count);
        Assert.Equal("Solicitação retirada pelo cidadão", occurrence.StatusHistory[1].Reason);
    }

    [Fact]
    public void Author_cannot_cancel_after_target_assignment_even_if_target_is_still_pending()
    {
        var author = Guid.NewGuid();
        var occurrence = CreateOccurrence(author);
        occurrence.AddMasterTarget(Guid.NewGuid(), occurrence.CreatedAt.AddMinutes(1));

        Assert.Throws<DomainException>(() => occurrence.CancelByAuthor(
            author,
            occurrence.CreatedAt.AddMinutes(2)));

        Assert.Equal(OccurrenceStatus.New, occurrence.Status);
    }

    [Fact]
    public void Non_author_cannot_cancel_occurrence()
    {
        var occurrence = CreateOccurrence(Guid.NewGuid());

        Assert.Throws<DomainException>(() => occurrence.CancelByAuthor(
            Guid.NewGuid(),
            occurrence.CreatedAt.AddMinutes(1)));
    }

    private static Occurrence CreateOccurrence(Guid? authorUserId = null) =>
        new(
            authorUserId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "Buraco na via",
            "Próximo ao cruzamento.",
            "Rua A, 100",
            new OccurrenceLocation(-30.0346m, -51.2177m),
            postalCode: "90010000",
            stateCode: "RS");
}
