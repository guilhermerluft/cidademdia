using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceMediaStatus
{
    public static readonly OccurrenceMediaStatus Pending = new("PENDING");
    public static readonly OccurrenceMediaStatus Ready = new("READY");

    private static readonly OccurrenceMediaStatus[] DefinedStatuses =
    [
        Pending,
        Ready
    ];

    private OccurrenceMediaStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrenceMediaStatus From(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var status = DefinedStatuses.FirstOrDefault(item => item.Value == normalized);

        return status
            ?? throw new DomainException($"Occurrence media status '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
