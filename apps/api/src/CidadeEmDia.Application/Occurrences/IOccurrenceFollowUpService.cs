namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceFollowUpService
{
    Task<OccurrenceComplementCommandResult> AddComplementAsync(
        Guid authorUserId,
        Guid occurrenceId,
        string content,
        CancellationToken cancellationToken = default);

    Task<OccurrenceForecastCommandResult> SetServiceForecastAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        DateTimeOffset estimatedFor,
        string? note,
        CancellationToken cancellationToken = default);
}

public sealed record OccurrenceComplementCommandItem(
    Guid Id,
    Guid OccurrenceId,
    Guid AuthorUserId,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record OccurrenceComplementCommandResult(
    bool Succeeded,
    OccurrenceComplementCommandItem? Complement,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceComplementCommandResult Success(OccurrenceComplementCommandItem complement) =>
        new(true, complement, null, null);

    public static OccurrenceComplementCommandResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record OccurrenceForecastCommandItem(
    Guid Id,
    Guid OccurrenceId,
    Guid DefinedByUserId,
    DateTimeOffset EstimatedFor,
    DateTimeOffset DefinedAt,
    string? Note);

public sealed record OccurrenceForecastCommandResult(
    bool Succeeded,
    OccurrenceForecastCommandItem? Forecast,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static OccurrenceForecastCommandResult Success(OccurrenceForecastCommandItem forecast) =>
        new(true, forecast, null, null);

    public static OccurrenceForecastCommandResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}
