using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceStatus
{
    public static readonly OccurrenceStatus New = new("NOVA");
    public static readonly OccurrenceStatus Received = new("RECEBIDA");
    public static readonly OccurrenceStatus UnderReview = new("EM_ANALISE");
    public static readonly OccurrenceStatus InProgress = new("EM_ANDAMENTO");
    public static readonly OccurrenceStatus AwaitingInformation = new("AGUARDANDO_INFORMACAO");
    public static readonly OccurrenceStatus Resolved = new("RESOLVIDA");
    public static readonly OccurrenceStatus Closed = new("ENCERRADA");
    public static readonly OccurrenceStatus Cancelled = new("CANCELADA");

    private static readonly OccurrenceStatus[] DefinedStatuses =
    [
        New,
        Received,
        UnderReview,
        InProgress,
        AwaitingInformation,
        Resolved,
        Closed,
        Cancelled
    ];

    private OccurrenceStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }
    public bool IsTerminal => this == Closed || this == Cancelled;

    public static OccurrenceStatus From(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        var status = DefinedStatuses.FirstOrDefault(item => item.Value == normalized);

        return status
            ?? throw new DomainException($"Occurrence status '{value}' is not supported.");
    }

    public override string ToString() => Value;
}
