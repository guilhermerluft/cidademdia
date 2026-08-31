using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceSupport : BaseEntity
{
    private OccurrenceSupport()
    {
    }

    public OccurrenceSupport(
        Guid occurrenceId,
        Guid userId,
        DateTimeOffset createdAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Occurrence is required for support.");
        if (userId == Guid.Empty)
            throw new DomainException("Support user is required.");

        OccurrenceId = occurrenceId;
        UserId = userId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OccurrenceId { get; private set; }
    public Guid UserId { get; private set; }
}
