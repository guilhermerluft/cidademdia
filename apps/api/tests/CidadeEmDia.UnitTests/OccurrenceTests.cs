using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceTests
{
    [Fact]
    public void Category_starts_active_and_normalizes_slug()
    {
        var category = new OccurrenceCategory("Iluminação pública", " ILUMINACAO-PUBLICA ", 2);

        Assert.Equal("Iluminação pública", category.Name);
        Assert.Equal("iluminacao-publica", category.Slug);
        Assert.Equal(OccurrenceCategoryStatus.Active, category.Status);
        Assert.Equal(2, category.DisplayOrder);
    }

    [Fact]
    public void Category_rejects_invalid_data()
    {
        Assert.Throws<DomainException>(() => new OccurrenceCategory(" ", "buracos"));
        Assert.Throws<DomainException>(() => new OccurrenceCategory("Buracos", " "));
        Assert.Throws<DomainException>(() => new OccurrenceCategory("Buracos", "buracos urbanos"));
        Assert.Throws<DomainException>(() => new OccurrenceCategory("Buracos", "buracos", -1));
    }

    [Fact]
    public void Occurrence_starts_new_with_opaque_public_code_and_initial_history()
    {
        var authorUserId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var occurrence = CreateOccurrence(authorUserId, categoryId);

        Assert.Equal(authorUserId, occurrence.AuthorUserId);
        Assert.Equal(categoryId, occurrence.CategoryId);
        Assert.Equal("Buraco na via", occurrence.Title);
        Assert.Equal("Próximo ao cruzamento.", occurrence.Description);
        Assert.Equal(OccurrenceStatus.New, occurrence.Status);
        Assert.Equal(20, occurrence.PublicCode.Value.Length);
        Assert.Single(occurrence.StatusHistory);
        Assert.Null(occurrence.StatusHistory[0].FromStatus);
        Assert.Equal(OccurrenceStatus.New, occurrence.StatusHistory[0].ToStatus);
        Assert.Equal(authorUserId, occurrence.StatusHistory[0].ChangedByUserId);
        Assert.Equal(occurrence.CreatedAt, occurrence.StatusHistory[0].CreatedAt);
    }

    [Fact]
    public void Occurrence_requires_author_category_title_address_and_location()
    {
        var categoryId = Guid.NewGuid();

        Assert.Throws<DomainException>(() => CreateOccurrence(Guid.Empty, categoryId));
        Assert.Throws<DomainException>(() => CreateOccurrence(Guid.NewGuid(), Guid.Empty));
        Assert.Throws<DomainException>(() => new Occurrence(
            Guid.NewGuid(), categoryId, " ", null, "Rua A", CreateLocation()));
        Assert.Throws<DomainException>(() => new Occurrence(
            Guid.NewGuid(), categoryId, "Buraco", null, " ", CreateLocation()));
        Assert.Throws<DomainException>(() => new Occurrence(
            Guid.NewGuid(), categoryId, "Buraco", null, "Rua A", null!));
    }

    [Fact]
    public void Occurrence_keeps_external_protocol_separate_from_public_code()
    {
        var occurrence = new Occurrence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Poste apagado",
            null,
            "Rua A, 100",
            CreateLocation(),
            postalCode: "90.010-000",
            stateCode: "rs",
            externalProtocolNumber: " 2026-00123 ",
            externalProtocolAgency: " Prefeitura ");

        Assert.Equal("90010000", occurrence.PostalCode);
        Assert.Equal("RS", occurrence.StateCode);
        Assert.Equal("2026-00123", occurrence.ExternalProtocolNumber);
        Assert.Equal("Prefeitura", occurrence.ExternalProtocolAgency);
        Assert.NotEqual(occurrence.ExternalProtocolNumber, occurrence.PublicCode.Value);
    }

    [Fact]
    public void Occurrence_rejects_invalid_postal_code_and_state_code()
    {
        Assert.Throws<DomainException>(() => new Occurrence(
            Guid.NewGuid(), Guid.NewGuid(), "Buraco", null, "Rua A", CreateLocation(), postalCode: "123"));

        Assert.Throws<DomainException>(() => new Occurrence(
            Guid.NewGuid(), Guid.NewGuid(), "Buraco", null, "Rua A", CreateLocation(), stateCode: "R1"));
    }

    [Fact]
    public void Occurrence_allows_up_to_three_distinct_master_targets()
    {
        var occurrence = CreateOccurrence();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        var first = occurrence.AddMasterTarget(Guid.NewGuid(), sentAt);
        occurrence.AddMasterTarget(Guid.NewGuid(), sentAt.AddMinutes(1));
        occurrence.AddMasterTarget(Guid.NewGuid(), sentAt.AddMinutes(2));

        Assert.Equal(Occurrence.MaxTargetsPerOccurrence, occurrence.Targets.Count);
        Assert.Equal(OccurrenceTargetStatus.Pending, first.Status);
        Assert.Equal(sentAt, first.SentAt);
    }

    [Fact]
    public void Occurrence_rejects_fourth_master_target()
    {
        var occurrence = CreateOccurrence();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.AddMasterTarget(Guid.NewGuid(), sentAt);
        occurrence.AddMasterTarget(Guid.NewGuid(), sentAt.AddMinutes(1));
        occurrence.AddMasterTarget(Guid.NewGuid(), sentAt.AddMinutes(2));

        Assert.Throws<DomainException>(() => occurrence.AddMasterTarget(
            Guid.NewGuid(),
            sentAt.AddMinutes(3)));
    }

    [Fact]
    public void Occurrence_rejects_duplicate_master_target()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.AddMasterTarget(masterUserId, sentAt);

        Assert.Throws<DomainException>(() => occurrence.AddMasterTarget(
            masterUserId,
            sentAt.AddMinutes(1)));
    }

    [Fact]
    public void Occurrence_target_cannot_predate_occurrence_creation()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() => occurrence.AddMasterTarget(
            Guid.NewGuid(),
            occurrence.CreatedAt.AddSeconds(-1)));
    }

    [Theory]
    [InlineData("NOVA")]
    [InlineData("recebida")]
    [InlineData(" EM_ANALISE ")]
    [InlineData("EM_ANDAMENTO")]
    [InlineData("AGUARDANDO_INFORMACAO")]
    [InlineData("RESOLVIDA")]
    [InlineData("ENCERRADA")]
    [InlineData("CANCELADA")]
    public void Occurrence_status_accepts_confirmed_values(string value)
    {
        Assert.NotNull(OccurrenceStatus.From(value));
    }

    [Fact]
    public void Occurrence_status_rejects_unknown_value()
    {
        Assert.Throws<DomainException>(() => OccurrenceStatus.From("ARCHIVED"));
    }

    [Fact]
    public void Status_change_records_from_to_actor_reason_and_timestamp()
    {
        var occurrence = CreateOccurrence();
        var actorUserId = Guid.NewGuid();
        var changedAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.TransitionTo(
            OccurrenceStatus.Received,
            actorUserId,
            changedAt,
            "Recebida pelo fluxo de atendimento");

        Assert.Equal(OccurrenceStatus.Received, occurrence.Status);
        Assert.Equal(2, occurrence.StatusHistory.Count);
        Assert.Equal(OccurrenceStatus.New, occurrence.StatusHistory[1].FromStatus);
        Assert.Equal(OccurrenceStatus.Received, occurrence.StatusHistory[1].ToStatus);
        Assert.Equal(actorUserId, occurrence.StatusHistory[1].ChangedByUserId);
        Assert.Equal(changedAt, occurrence.StatusHistory[1].CreatedAt);
        Assert.Equal("Recebida pelo fluxo de atendimento", occurrence.StatusHistory[1].Reason);
    }

    [Fact]
    public void Confirmed_status_flow_can_be_recorded_without_overwriting_history()
    {
        var occurrence = CreateOccurrence();
        var actor = Guid.NewGuid();
        var timestamp = occurrence.CreatedAt;

        occurrence.TransitionTo(OccurrenceStatus.Received, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.UnderReview, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.InProgress, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.AwaitingInformation, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.Resolved, actor, timestamp = timestamp.AddMinutes(1));
        occurrence.TransitionTo(OccurrenceStatus.Closed, actor, timestamp = timestamp.AddMinutes(1));

        Assert.Equal(OccurrenceStatus.Closed, occurrence.Status);
        Assert.Equal(timestamp, occurrence.ClosedAt);
        Assert.Equal(7, occurrence.StatusHistory.Count);
    }

    [Fact]
    public void Same_status_and_transition_after_terminal_status_are_blocked()
    {
        var occurrence = CreateOccurrence();
        var actor = Guid.NewGuid();

        Assert.Throws<DomainException>(() => occurrence.TransitionTo(
            OccurrenceStatus.New,
            actor,
            occurrence.CreatedAt.AddMinutes(1)));

        occurrence.TransitionTo(
            OccurrenceStatus.Cancelled,
            actor,
            occurrence.CreatedAt.AddMinutes(2));

        Assert.NotNull(occurrence.CancelledAt);
        Assert.Throws<DomainException>(() => occurrence.TransitionTo(
            OccurrenceStatus.Received,
            actor,
            occurrence.CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void Original_content_remains_unchanged_when_complement_is_added()
    {
        var occurrence = CreateOccurrence();
        var originalTitle = occurrence.Title;
        var originalDescription = occurrence.Description;
        var originalAddress = occurrence.AddressText;
        var createdAt = occurrence.CreatedAt.AddMinutes(10);

        var complement = occurrence.AddComplement(
            Guid.NewGuid(),
            "  O buraco aumentou após a chuva.  ",
            createdAt);

        Assert.Single(occurrence.Complements);
        Assert.Equal("O buraco aumentou após a chuva.", complement.Content);
        Assert.Equal(createdAt, complement.CreatedAt);
        Assert.Equal(originalTitle, occurrence.Title);
        Assert.Equal(originalDescription, occurrence.Description);
        Assert.Equal(originalAddress, occurrence.AddressText);
    }

    [Fact]
    public void Blank_complement_is_rejected()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() => occurrence.AddComplement(
            Guid.NewGuid(),
            "   ",
            occurrence.CreatedAt.AddMinutes(1)));
    }

    [Fact]
    public void Service_forecast_keeps_auditable_revision_history()
    {
        var occurrence = CreateOccurrence();
        var firstDefinedAt = occurrence.CreatedAt.AddMinutes(30);
        var secondDefinedAt = occurrence.CreatedAt.AddHours(2);
        var firstActor = Guid.NewGuid();
        var secondActor = Guid.NewGuid();

        occurrence.SetServiceForecast(
            occurrence.CreatedAt.AddDays(3),
            firstActor,
            firstDefinedAt,
            "Previsão inicial");

        occurrence.SetServiceForecast(
            occurrence.CreatedAt.AddDays(5),
            secondActor,
            secondDefinedAt,
            "Reprogramado");

        Assert.Equal(2, occurrence.ServiceForecastHistory.Count);
        Assert.Equal(occurrence.CreatedAt.AddDays(5), occurrence.CurrentServiceForecast);
        Assert.Equal(firstActor, occurrence.ServiceForecastHistory[0].DefinedByUserId);
        Assert.Equal(secondActor, occurrence.ServiceForecastHistory[1].DefinedByUserId);
        Assert.Equal("Reprogramado", occurrence.ServiceForecastHistory[1].Note);
    }

    [Fact]
    public void Service_forecast_must_be_future_relative_to_revision()
    {
        var occurrence = CreateOccurrence();
        var definedAt = occurrence.CreatedAt.AddMinutes(30);

        Assert.Throws<DomainException>(() => occurrence.SetServiceForecast(
            definedAt,
            Guid.NewGuid(),
            definedAt));
    }

    [Fact]
    public void Events_cannot_predate_occurrence_creation()
    {
        var occurrence = CreateOccurrence();
        var beforeCreation = occurrence.CreatedAt.AddSeconds(-1);

        Assert.Throws<DomainException>(() => occurrence.AddComplement(
            Guid.NewGuid(),
            "Informação adicional",
            beforeCreation));

        Assert.Throws<DomainException>(() => occurrence.TransitionTo(
            OccurrenceStatus.Received,
            Guid.NewGuid(),
            beforeCreation));
    }

    [Fact]
    public void Location_validates_geographic_coordinate_ranges()
    {
        Assert.Throws<DomainException>(() => new OccurrenceLocation(-91m, -51m));
        Assert.Throws<DomainException>(() => new OccurrenceLocation(-30m, 181m));

        var location = CreateLocation();

        Assert.Equal(-30.0346m, location.Latitude);
        Assert.Equal(-51.2177m, location.Longitude);
    }

    [Fact]
    public void Public_code_is_non_sequential_opaque_and_validated()
    {
        var first = OccurrencePublicCode.New();
        var second = OccurrencePublicCode.New();

        Assert.Equal(20, first.Value.Length);
        Assert.NotEqual(first, second);
        Assert.Equal(first, OccurrencePublicCode.From(first.Value.ToLowerInvariant()));
        Assert.Throws<DomainException>(() => OccurrencePublicCode.From("0001"));
        Assert.Throws<DomainException>(() => OccurrencePublicCode.From("ZZZZZZZZZZZZZZZZZZZZ"));
    }

    private static Occurrence CreateOccurrence(
        Guid? authorUserId = null,
        Guid? categoryId = null) =>
        new(
            authorUserId ?? Guid.NewGuid(),
            categoryId ?? Guid.NewGuid(),
            "Buraco na via",
            "Próximo ao cruzamento.",
            "Rua A, 100",
            CreateLocation(),
            postalCode: "90010000",
            stateCode: "RS");

    private static OccurrenceLocation CreateLocation() =>
        new(-30.0346m, -51.2177m);
}
