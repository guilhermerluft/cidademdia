namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceCreationService
{
    Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        Guid? masterUserId,
        Guid? institutionId,
        string? addressee,
        CreateOccurrenceInput input,
        IReadOnlyCollection<Guid>? mediaIds,
        CancellationToken cancellationToken = default);
}
