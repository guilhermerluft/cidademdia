using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceMediaTests
{
    [Fact]
    public void New_media_should_start_pending()
    {
        var now = DateTimeOffset.UtcNow;
        var media = NewMedia(now);

        Assert.Equal(OccurrenceMediaStatus.Pending, media.Status);
        Assert.Null(media.OccurrenceId);
        Assert.Null(media.ReadyAt);
        Assert.Null(media.ActualSizeBytes);
    }

    [Fact]
    public void MarkReady_should_preserve_verified_metadata()
    {
        var now = DateTimeOffset.UtcNow;
        var media = NewMedia(now);
        var readyAt = now.AddMinutes(1);

        media.MarkReady(1234, "image/jpeg", readyAt);

        Assert.Equal(OccurrenceMediaStatus.Ready, media.Status);
        Assert.Equal(1234, media.ActualSizeBytes);
        Assert.Equal(readyAt, media.ReadyAt);
    }

    [Fact]
    public void MarkReady_should_reject_size_mismatch()
    {
        var media = NewMedia(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(() =>
            media.MarkReady(1235, "image/jpeg", DateTimeOffset.UtcNow));

        Assert.Equal("Uploaded occurrence media size does not match the declared size.", exception.Message);
    }

    [Fact]
    public void Attach_should_require_ready_media()
    {
        var media = NewMedia(DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainException>(() =>
            media.AttachToOccurrence(Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal("Only ready occurrence media can be attached.", exception.Message);
    }

    [Fact]
    public void Ready_media_can_be_attached_only_once()
    {
        var now = DateTimeOffset.UtcNow;
        var media = NewMedia(now);
        var occurrenceId = Guid.NewGuid();

        media.MarkReady(1234, "image/jpeg", now.AddMinutes(1));
        media.AttachToOccurrence(occurrenceId, now.AddMinutes(2));
        media.AttachToOccurrence(occurrenceId, now.AddMinutes(3));

        Assert.Equal(occurrenceId, media.OccurrenceId);

        var exception = Assert.Throws<DomainException>(() =>
            media.AttachToOccurrence(Guid.NewGuid(), now.AddMinutes(4)));

        Assert.Equal("Occurrence media is already attached to another occurrence.", exception.Message);
    }

    private static OccurrenceMedia NewMedia(DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "occurrences/uploads/2026/08/file.jpg",
            "foto.jpg",
            "image/jpeg",
            1234,
            now);
}
