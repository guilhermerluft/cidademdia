using System.Text.Json.Serialization;

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
    int Page,
    int PageSize);

public sealed record PublicOccurrenceMediaItem(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    Uri ReadUrl,
    DateTimeOffset ReadUrlExpiresAt);

public sealed record PublicOccurrenceItem(
    Guid Id,
    string PublicCode,
    string CategoryName,
    string CategorySlug,
    string Title,
    string? Description,
    string Status,
    string AddressText,
    string? ExternalProtocolNumber,
    int SupportCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PublicOccurrenceMediaItem? CoverMedia);

public sealed record PublicOccurrenceDetails(
    Guid Id,
    string PublicCode,
    string CategoryName,
    string CategorySlug,
    string Title,
    string? Description,
    string Status,
    string AddressText,
    [property: JsonIgnore] string? PostalCode,
    [property: JsonIgnore] string? StateCode,
    [property: JsonIgnore] decimal Latitude,
    [property: JsonIgnore] decimal Longitude,
    string? ExternalProtocolNumber,
    [property: JsonIgnore] string? ExternalProtocolAgency,
    int SupportCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PublicOccurrenceMediaItem> Media);

public sealed record PublicOccurrencePage(
    IReadOnlyList<PublicOccurrenceItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record PublicOccurrenceSearchResult(
    bool Succeeded,
    PublicOccurrencePage? Page,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static PublicOccurrenceSearchResult Success(PublicOccurrencePage page) =>
        new(true, page);

    public static PublicOccurrenceSearchResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
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

    Task<PublicOccurrenceDetails?> GetPublicDetailsAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default);
}
