namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceService
{
    Task<IReadOnlyList<OccurrenceCategoryItem>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        CreateOccurrenceInput input,
        CancellationToken cancellationToken = default);

    Task<OccurrenceListResult> GetMineAsync(
        Guid authorUserId,
        string? status,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OccurrenceDetails?> GetMineByIdAsync(
        Guid authorUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceDetails?> GetMineByPublicCodeAsync(
        Guid authorUserId,
        string publicCode,
        CancellationToken cancellationToken = default);
}
