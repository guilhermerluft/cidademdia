using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceTests
{
    private static readonly DateTimeOffset RegisteredAt =
        new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Occurrence_starts_open_with_public_code_and_initial_history()
    {
        var authorUserId = Guid.NewGuid();
        var occurrence = CreateOccurrence(authorUserId);

        Assert.Equal(authorUserId, occurrence.AuthorUserId);
        Assert.Equal("Buraco na via", occurrence.Title);
        Assert.Equal("Próximo ao cruzamento.", occurrence.Description);
        Assert.Equal(OccurrenceType.Pothole, occurrence.Type);
        Assert.Equal(OccurrenceStatus.Open, occurrence.Status);
        Assert.Equal(20, occurrence.PublicCode.Value.Length);
        Assert.Single(occurrence.StatusHistory);
        Assert.Equal(OccurrenceStatus.Open, occurrence.StatusHistory[0].Status);
        Assert.Equal(authorUserId, occurrence.StatusHistory[0].ChangedByUserId);
        Assert.Equal(RegisteredAt, occurrence.StatusHistory[0].ChangedAt);
    }

    [Fact]
    public void Occurrence_requires_author()
    {
        Assert.Throws<DomainException>(() => CreateOccurrence(Guid.Empty));
    }

    [Fact]
    public void Occurrence_requires_title()
    {
        Assert.Throws<DomainException>(() =>
            new Occurrence(
                Guid.NewGuid(),
                "   ",
                null,
                OccurrenceType.Pothole,
                CreateLocation(),
                RegisteredAt));
    }

    [Fact]
    public void Occurrence_description_is_optional()
    {
        var occurrence = new Occurrence(
            Guid.NewGuid(),
            "Poste apagado",
            "   ",
            OccurrenceType.Streetlight,
            CreateLocation(),
            RegisteredAt);

        Assert.Null(occurrence.Description);
    }

    [Fact]
    public void Public_code_is_opaque_and_validated()
    {
        var code = OccurrencePublicCode.New();

        Assert.Equal(20, code.Value.Length);
        Assert.Equal(code, OccurrencePublicCode.From(code.Value.ToLowerInvariant()));
        Assert.Throws<DomainException>(() => OccurrencePublicCode.From("0001"));
        Assert.Throws<DomainException>(() => OccurrencePublicCode.From("ZZZZZZZZZZZZZZZZZZZZ"));
    }

    [Theory]
    [InlineData("OPEN")]
    [InlineData("open")]
    [InlineData(" IN_PROGRESS ")]
    [InlineData("RESOLVED")]
    [InlineData("CANCELLED")]
    public void Occurrence_status_accepts_only_defined_values(string value)
    {
        Assert.NotNull(OccurrenceStatus.From(value));
    }

    [Fact]
    public void Occurrence_status_rejects_unknown_value()
    {
        Assert.Throws<DomainException>(() => OccurrenceStatus.From("ARCHIVED"));
    }

    [Theory]
    [InlineData("POTHOLE")]
    [InlineData("pothole")]
    [InlineData(" STREETLIGHT ")]
    [InlineData("FLOODING")]
    public void Occurrence_type_accepts_only_defined_values(string value)
    {
        Assert.NotNull(OccurrenceType.From(value));
    }

    [Fact]
    public void Occurrence_type_rejects_unknown_value()
    {
        Assert.Throws<DomainException>(() => OccurrenceType.From("OTHER"));
    }

    [Fact]
    public void Location_requires_address_and_valid_coordinates()
    {
        Assert.Throws<DomainException>(() => new OccurrenceLocation(" ", -30m, -51m));
        Assert.Throws<DomainException>(() => new OccurrenceLocation("Rua A", -91m, -51m));
        Assert.Throws<DomainException>(() => new OccurrenceLocation("Rua A", -30m, 181m));

        var location = CreateLocation();

        Assert.Equal("Rua A, 100", location.Address);
        Assert.Equal(-30.0346m, location.Latitude);
        Assert.Equal(-51.2177m, location.Longitude);
    }

    [Fact]
    public void Open_occurrence_can_move_to_in_progress_and_records_history()
    {
        var occurrence = CreateOccurrence();
        var actorUserId = Guid.NewGuid();
        var changedAt = RegisteredAt.AddMinutes(10);

        occurrence.TransitionTo(
            OccurrenceStatus.InProgress,
            actorUserId,
            changedAt,
            "Equipe acionada");

        Assert.Equal(OccurrenceStatus.InProgress, occurrence.Status);
        Assert.Equal(2, occurrence.StatusHistory.Count);
        Assert.Equal(OccurrenceStatus.InProgress, occurrence.StatusHistory[1].Status);
        Assert.Equal(actorUserId, occurrence.StatusHistory[1].ChangedByUserId);
        Assert.Equal(changedAt, occurrence.StatusHistory[1].ChangedAt);
        Assert.Equal("Equipe acionada", occurrence.StatusHistory[1].Note);
    }

    [Fact]
    public void In_progress_occurrence_can_be_resolved()
    {
        var occurrence = CreateOccurrence();
        occurrence.TransitionTo(
            OccurrenceStatus.InProgress,
            Guid.NewGuid(),
            RegisteredAt.AddMinutes(5));

        occurrence.TransitionTo(
            OccurrenceStatus.Resolved,
            Guid.NewGuid(),
            RegisteredAt.AddHours(1));

        Assert.Equal(OccurrenceStatus.Resolved, occurrence.Status);
        Assert.Equal(3, occurrence.StatusHistory.Count);
    }

    [Fact]
    public void Open_occurrence_can_be_cancelled()
    {
        var occurrence = CreateOccurrence();

        occurrence.TransitionTo(
            OccurrenceStatus.Cancelled,
            Guid.NewGuid(),
            RegisteredAt.AddMinutes(5));

        Assert.Equal(OccurrenceStatus.Cancelled, occurrence.Status);
    }

    [Fact]
    public void Invalid_status_transitions_are_blocked()
    {
        var openOccurrence = CreateOccurrence();
        Assert.Throws<DomainException>(() =>
            openOccurrence.TransitionTo(
                OccurrenceStatus.Resolved,
                Guid.NewGuid(),
                RegisteredAt.AddMinutes(5)));

        var inProgressOccurrence = CreateOccurrence();
        inProgressOccurrence.TransitionTo(
            OccurrenceStatus.InProgress,
            Guid.NewGuid(),
            RegisteredAt.AddMinutes(5));
        Assert.Throws<DomainException>(() =>
            inProgressOccurrence.TransitionTo(
                OccurrenceStatus.Cancelled,
                Guid.NewGuid(),
                RegisteredAt.AddMinutes(10)));

        var resolvedOccurrence = CreateOccurrence();
        resolvedOccurrence.TransitionTo(
            OccurrenceStatus.InProgress,
            Guid.NewGuid(),
            RegisteredAt.AddMinutes(5));
        resolvedOccurrence.TransitionTo(
            OccurrenceStatus.Resolved,
            Guid.NewGuid(),
            RegisteredAt.AddMinutes(10));
        Assert.Throws<DomainException>(() =>
            resolvedOccurrence.TransitionTo(
                OccurrenceStatus.Open,
                Guid.NewGuid(),
                RegisteredAt.AddMinutes(15)));
    }

    [Fact]
    public void Status_history_is_read_only_from_outside_the_aggregate()
    {
        var occurrence = CreateOccurrence();
        var collection = Assert.IsAssignableFrom<ICollection<OccurrenceStatusChange>>(
            occurrence.StatusHistory);

        Assert.Throws<NotSupportedException>(() =>
            collection.Add(occurrence.StatusHistory[0]));
    }

    [Fact]
    public void Complement_is_separate_and_traceable()
    {
        var occurrence = CreateOccurrence();
        var authorUserId = Guid.NewGuid();
        var createdAt = RegisteredAt.AddMinutes(20);

        var complement = occurrence.AddComplement(
            authorUserId,
            "  O buraco aumentou após a chuva.  ",
            createdAt);

        Assert.Single(occurrence.Complements);
        Assert.Equal(authorUserId, complement.AuthorUserId);
        Assert.Equal("O buraco aumentou após a chuva.", complement.Content);
        Assert.Equal(createdAt, complement.CreatedAt);
    }

    [Fact]
    public void Blank_complement_is_rejected()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() =>
            occurrence.AddComplement(
                Guid.NewGuid(),
                "   ",
                RegisteredAt.AddMinutes(1)));
    }

    [Fact]
    public void Service_forecast_keeps_an_auditable_revision_history()
    {
        var occurrence = CreateOccurrence();
        var firstActor = Guid.NewGuid();
        var secondActor = Guid.NewGuid();

        occurrence.SetServiceForecast(
            RegisteredAt.AddDays(3),
            firstActor,
            RegisteredAt.AddMinutes(30),
            "Previsão inicial");

        occurrence.SetServiceForecast(
            RegisteredAt.AddDays(5),
            secondActor,
            RegisteredAt.AddHours(2),
            "Reprogramado por indisponibilidade da equipe");

        Assert.Equal(2, occurrence.ServiceForecastHistory.Count);
        Assert.Equal(RegisteredAt.AddDays(5), occurrence.CurrentServiceForecast);
        Assert.Equal(firstActor, occurrence.ServiceForecastHistory[0].DefinedByUserId);
        Assert.Equal(secondActor, occurrence.ServiceForecastHistory[1].DefinedByUserId);
        Assert.Equal(
            "Reprogramado por indisponibilidade da equipe",
            occurrence.ServiceForecastHistory[1].Note);
    }

    [Fact]
    public void Service_forecast_must_be_future_relative_to_revision()
    {
        var occurrence = CreateOccurrence();
        var definedAt = RegisteredAt.AddMinutes(30);

        Assert.Throws<DomainException>(() =>
            occurrence.SetServiceForecast(
                definedAt,
                Guid.NewGuid(),
                definedAt));
    }

    [Fact]
    public void Occurrence_events_cannot_predate_registration()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() =>
            occurrence.AddComplement(
                Guid.NewGuid(),
                "Informação",
                RegisteredAt.AddSeconds(-1)));

        Assert.Throws<DomainException>(() =>
            occurrence.TransitionTo(
                OccurrenceStatus.InProgress,
                Guid.NewGuid(),
                RegisteredAt.AddSeconds(-1)));
    }

    private static Occurrence CreateOccurrence(Guid? authorUserId = null) =>
        new(
            authorUserId ?? Guid.NewGuid(),
            "Buraco na via",
            "Próximo ao cruzamento.",
            OccurrenceType.Pothole,
            CreateLocation(),
            RegisteredAt);

    private static OccurrenceLocation CreateLocation() =>
        new("Rua A, 100", -30.0346m, -51.2177m);
}
