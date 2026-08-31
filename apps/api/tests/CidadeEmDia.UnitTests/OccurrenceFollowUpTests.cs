using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceFollowUpTests
{
    [Fact]
    public void Complement_preserves_author_content_and_original_occurrence_data()
    {
        var occurrence = CreateOccurrence();
        var authorUserId = occurrence.AuthorUserId;
        var originalTitle = occurrence.Title;
        var originalDescription = occurrence.Description;
        var createdAt = occurrence.CreatedAt.AddMinutes(5);

        var complement = occurrence.AddComplement(
            authorUserId,
            "  Nova informação enviada pelo cidadão.  ",
            createdAt);

        Assert.Equal(authorUserId, complement.AuthorUserId);
        Assert.Equal("Nova informação enviada pelo cidadão.", complement.Content);
        Assert.Equal(createdAt, complement.CreatedAt);
        Assert.Equal(originalTitle, occurrence.Title);
        Assert.Equal(originalDescription, occurrence.Description);
        Assert.Single(occurrence.Complements);
    }

    [Fact]
    public void Forecast_revision_preserves_actor_timestamp_and_history()
    {
        var occurrence = CreateOccurrence();
        var firstActor = Guid.NewGuid();
        var secondActor = Guid.NewGuid();
        var firstDefinedAt = occurrence.CreatedAt.AddMinutes(10);
        var secondDefinedAt = occurrence.CreatedAt.AddMinutes(20);

        var first = occurrence.SetServiceForecast(
            firstDefinedAt.AddDays(2),
            firstActor,
            firstDefinedAt,
            "Primeira previsão");

        var second = occurrence.SetServiceForecast(
            secondDefinedAt.AddDays(4),
            secondActor,
            secondDefinedAt,
            "  Previsão revisada  ");

        Assert.Equal(firstActor, first.DefinedByUserId);
        Assert.Equal(firstDefinedAt, first.DefinedAt);
        Assert.Equal(secondActor, second.DefinedByUserId);
        Assert.Equal(secondDefinedAt, second.DefinedAt);
        Assert.Equal("Previsão revisada", second.Note);
        Assert.Equal(second.EstimatedFor, occurrence.CurrentServiceForecast);
        Assert.Equal(2, occurrence.ServiceForecastHistory.Count);
    }

    [Fact]
    public void Forecast_must_be_future_relative_to_definition_time()
    {
        var occurrence = CreateOccurrence();
        var definedAt = occurrence.CreatedAt.AddMinutes(10);

        Assert.Throws<DomainException>(() => occurrence.SetServiceForecast(
            definedAt,
            Guid.NewGuid(),
            definedAt,
            null));
    }

    private static Occurrence CreateOccurrence() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Buraco na via",
            "Descrição original",
            "Rua A, 100",
            new OccurrenceLocation(-30.0346m, -51.2177m),
            postalCode: "90010000",
            stateCode: "RS");
}
