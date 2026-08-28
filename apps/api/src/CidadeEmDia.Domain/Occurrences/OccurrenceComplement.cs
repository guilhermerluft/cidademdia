using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceComplement
{
    private OccurrenceComplement()
    {
    }

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

    public Guid Id { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
