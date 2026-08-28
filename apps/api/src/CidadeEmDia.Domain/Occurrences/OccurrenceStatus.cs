using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceStatus
{
    public static readonly OccurrenceStatus Open = new("OPEN");
    public static readonly OccurrenceStatus InProgress = new("IN_PROGRESS");
    public static readonly OccurrenceStatus Resolved = new("RESOLVED");
    public static readonly OccurrenceStatus Cancelled = new("CANCELLED");

    private static readonly OccurrenceStatus[] DefinedStatuses =
    [
        Open,
        InProgress,
        Resolved,
        Cancelled
    ];

    private OccurrenceStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrenceStatus From(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var status = DefinedStatuses.FirstOrDefault(item => item.Value == normalized);

        return status
            ?? throw new DomainException($"Occurrence status '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
