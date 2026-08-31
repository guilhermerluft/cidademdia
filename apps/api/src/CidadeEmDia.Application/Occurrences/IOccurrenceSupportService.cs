namespace CidadeEmDia.Application.Occurrences;

public interface IOccurrenceSupportService
{
    Task<OccurrenceSupportResult> GetAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);

    Task<OccurrenceSupportResult> SupportAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default);
}

public sealed record OccurrenceSupportItem(
    Guid OccurrenceId,
    int SupportCount,
    bool SupportedByRequester,
    DateTimeOffset? SupportedAt);

public sealed record OccurrenceSupportResult(
    bool Succeeded,
    bool WasCreated,
    OccurrenceSupportItem? Support,
    string? ErrorCode = null,
    string? ErrorDetail = null)
{
    public static OccurrenceSupportResult Success(
        OccurrenceSupportItem support,
        bool wasCreated = false) =>
        new(true, wasCreated, support);

    public static OccurrenceSupportResult Failure(
        string errorCode,
        string? errorDetail = null) =>
        new(false, false, null, errorCode, errorDetail);
}
