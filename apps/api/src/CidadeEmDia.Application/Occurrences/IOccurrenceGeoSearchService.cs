namespace CidadeEmDia.Application.Occurrences;

public sealed record OccurrenceGeoSearchInput(
    string? Status,
    Guid? CategoryId,
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    decimal? RadiusKm,
    int Page,
    int PageSize);

public interface IOccurrenceGeoSearchService
{
    Task<OccurrenceListResult> SearchMineAsync(
        Guid authorUserId,
        OccurrenceGeoSearchInput input,
        CancellationToken cancellationToken = default);
}
