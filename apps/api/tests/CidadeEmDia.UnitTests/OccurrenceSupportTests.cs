using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceSupportTests
{
    [Fact]
    public void Constructor_should_preserve_occurrence_user_and_timestamp()
    {
        var occurrenceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var support = new OccurrenceSupport(
            occurrenceId,
            userId,
            createdAt);

        Assert.Equal(occurrenceId, support.OccurrenceId);
        Assert.Equal(userId, support.UserId);
        Assert.Equal(createdAt, support.CreatedAt);
        Assert.Equal(createdAt, support.UpdatedAt);
    }

    [Fact]
    public void Constructor_should_reject_empty_occurrence()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new OccurrenceSupport(
                Guid.Empty,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));

        Assert.Equal("Occurrence is required for support.", exception.Message);
    }

    [Fact]
    public void Constructor_should_reject_empty_user()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new OccurrenceSupport(
                Guid.NewGuid(),
                Guid.Empty,
                DateTimeOffset.UtcNow));

        Assert.Equal("Support user is required.", exception.Message);
    }
}
