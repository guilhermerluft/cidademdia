using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceType
{
    public static readonly OccurrenceType Pothole = new("POTHOLE");
    public static readonly OccurrenceType Streetlight = new("STREETLIGHT");
    public static readonly OccurrenceType Flooding = new("FLOODING");

    private static readonly OccurrenceType[] DefinedTypes =
    [
        Pothole,
        Streetlight,
        Flooding
    ];

    private OccurrenceType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrenceType From(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var type = DefinedTypes.FirstOrDefault(item => item.Value == normalized);

        return type
            ?? throw new DomainException($"Occurrence type '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
