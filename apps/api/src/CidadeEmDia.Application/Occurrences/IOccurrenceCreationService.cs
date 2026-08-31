namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceCreationService
{
    Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        CreateOccurrenceInput input,
        IReadOnlyCollection<Guid>? mediaIds,
        CancellationToken cancellationToken = default);
}
