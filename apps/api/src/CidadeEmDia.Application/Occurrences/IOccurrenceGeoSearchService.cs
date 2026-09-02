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

public sealed record PublicOccurrenceSearchInput(
    string? City,
    decimal? Latitude,
    decimal? Longitude,
    decimal? RadiusKm,
    int Limit);

public sealed record PublicOccurrenceItem(
    Guid Id,
    string PublicCode,
    string CategoryName,
    string CategorySlug,
    string Title,
    string? Description,
    string Status,
    string AddressText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PublicOccurrenceSearchResult(
    bool Succeeded,
    IReadOnlyList<PublicOccurrenceItem> Items,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static PublicOccurrenceSearchResult Success(IReadOnlyList<PublicOccurrenceItem> items) =>
        new(true, items);

    public static PublicOccurrenceSearchResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, Array.Empty<PublicOccurrenceItem>(), errorCode, errorDetail);
}

public interface IOccurrenceGeoSearchService
{
    Task<OccurrenceListResult> SearchMineAsync(
        Guid authorUserId,
        OccurrenceGeoSearchInput input,
        CancellationToken cancellationToken = default);

    Task<PublicOccurrenceSearchResult> SearchPublicAsync(
        PublicOccurrenceSearchInput input,
        CancellationToken cancellationToken = default);
}
