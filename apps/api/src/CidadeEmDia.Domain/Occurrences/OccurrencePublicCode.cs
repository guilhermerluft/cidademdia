using System.Security.Cryptography;
using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrencePublicCode
{
    private const int ByteLength = 10;
    private const int HexLength = ByteLength * 2;

    private OccurrencePublicCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OccurrencePublicCode New()
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        RandomNumberGenerator.Fill(bytes);
        return new OccurrencePublicCode(Convert.ToHexString(bytes));
    }

    public static OccurrencePublicCode From(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length != HexLength
            || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainException("Occurrence public code is invalid.");
        }

        return new OccurrencePublicCode(normalized);
    }

    public override string ToString() => Value;
}
