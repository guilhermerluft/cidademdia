using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceTargetStatus
{
    public static readonly OccurrenceTargetStatus Pending = new("PENDING");
    public static readonly OccurrenceTargetStatus Received = new("RECEIVED");
    public static readonly OccurrenceTargetStatus Accepted = new("ACCEPTED");
    public static readonly OccurrenceTargetStatus Rejected = new("REJECTED");
    public static readonly OccurrenceTargetStatus Closed = new("CLOSED");

    private static readonly OccurrenceTargetStatus[] DefinedStatuses =
    [
        Pending,
        Received,
        Accepted,
        Rejected,
        Closed
    ];

    private OccurrenceTargetStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrenceTargetStatus From(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var status = DefinedStatuses.FirstOrDefault(item => item.Value == normalized);

        return status
            ?? throw new DomainException($"Occurrence target status '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
