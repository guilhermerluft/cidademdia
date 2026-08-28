using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceComplement
{
    internal OccurrenceComplement(Guid authorUserId, string content, DateTimeOffset createdAt)
    {
        if (authorUserId == Guid.Empty)
            throw new DomainException("Complement author is required.");
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Occurrence complement content is required.");

        Id = Guid.NewGuid();
        AuthorUserId = authorUserId;
        Content = content.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid AuthorUserId { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
}
