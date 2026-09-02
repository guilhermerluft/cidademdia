namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceService
{
    Task<IReadOnlyList<OccurrenceCategoryItem>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EligibleMasterItem>> GetEligibleMastersAsync(CancellationToken cancellationToken = default);

    Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        CreateOccurrenceInput input,
        CancellationToken cancellationToken = default);

    Task<OccurrenceListResult> GetMineAsync(
        Guid authorUserId,
        string? status,
        Guid? categoryId,
        string? city,
        decimal? latitude,
        decimal? longitude,
        decimal? radiusKm,
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

    Task<AddOccurrenceTargetResult> AddMasterTargetAsync(
        Guid authorUserId,
        Guid occurrenceId,
        Guid masterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OccurrenceTargetItem>?> GetTargetsAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);
}
