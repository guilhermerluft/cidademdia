using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceCategoryStatus
{
    public static readonly OccurrenceCategoryStatus Active = new("ACTIVE");
    public static readonly OccurrenceCategoryStatus Inactive = new("INACTIVE");

    private static readonly OccurrenceCategoryStatus[] DefinedStatuses =
    [
        Active,
        Inactive
    ];

    private OccurrenceCategoryStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrenceCategoryStatus From(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var status = DefinedStatuses.FirstOrDefault(item => item.Value == normalized);

        return status
            ?? throw new DomainException($"Occurrence category status '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
