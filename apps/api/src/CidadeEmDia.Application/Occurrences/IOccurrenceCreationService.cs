namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceCreationService
{
    Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        Guid masterUserId,
        CreateOccurrenceInput input,
        IReadOnlyCollection<Guid>? mediaIds,
        CancellationToken cancellationToken = default);
}
