namespace CidadeEmDia.Application.Occurrences;

public sealed record CreateOccurrenceInput(
    Guid CategoryId,
    string Title,
    string? Description,
    string AddressText,
    decimal Latitude,
    decimal Longitude,
    string? PostalCode,
    Guid? CityId,
    string? StateCode,
    string? ExternalProtocolNumber,
    string? ExternalProtocolAgency);

public sealed record OccurrenceCategoryItem(
    Guid Id,
    string Name,
    string Slug,
    int DisplayOrder);

public sealed record EligibleMasterItem(
    Guid Id,
    string DisplayName);

public sealed record OccurrenceTargetItem(
    Guid Id,
    Guid OccurrenceId,
    Guid MasterUserId,
    string MasterDisplayName,
    string Status,
    DateTimeOffset SentAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? ClosedAt);

public sealed record OccurrenceListItem(
    Guid Id,
    string PublicCode,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string Status,
    string AddressText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OccurrenceStatusHistoryItem(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset CreatedAt,
    string? Reason);

public sealed record OccurrenceComplementItem(
    Guid Id,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record OccurrenceServiceForecastItem(
    Guid Id,
    DateTimeOffset EstimatedFor,
    DateTimeOffset DefinedAt,
    string? Note);

public sealed record OccurrenceDetails(
    Guid Id,
    string PublicCode,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string? Description,
    string Status,
    string AddressText,
    string? PostalCode,
    Guid? CityId,
    string? StateCode,
    decimal Latitude,
    decimal Longitude,
    string? ExternalProtocolNumber,
    string? ExternalProtocolAgency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? CurrentServiceForecast,
    IReadOnlyList<OccurrenceStatusHistoryItem> StatusHistory,
    IReadOnlyList<OccurrenceComplementItem> Complements,
    IReadOnlyList<OccurrenceServiceForecastItem> ServiceForecastHistory);

public sealed record OccurrencePage(
    IReadOnlyList<OccurrenceListItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record CreateOccurrenceResult(
    bool Succeeded,
    OccurrenceDetails? Occurrence,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static CreateOccurrenceResult Success(OccurrenceDetails occurrence) =>
        new(true, occurrence, null, null);

    public static CreateOccurrenceResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record AddOccurrenceTargetResult(
    bool Succeeded,
    OccurrenceTargetItem? Target,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static AddOccurrenceTargetResult Success(OccurrenceTargetItem target) =>
        new(true, target, null, null);

    public static AddOccurrenceTargetResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record OccurrenceListResult(
    bool Succeeded,
    OccurrencePage? Page,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceListResult Success(OccurrencePage page) =>
        new(true, page, null, null);

    public static OccurrenceListResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}
