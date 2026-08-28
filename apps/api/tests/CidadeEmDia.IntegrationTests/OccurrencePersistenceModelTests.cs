using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace CidadeEmDia.IntegrationTests;

public sealed class OccurrencePersistenceModelTests
{
    [Fact]
    public void Occurrence_model_uses_expected_tables_and_value_conversions()
    {
        using var context = CreateContext();

        var category = context.Model.FindEntityType(typeof(OccurrenceCategory));
        var occurrence = context.Model.FindEntityType(typeof(Occurrence));
        var statusHistory = context.Model.FindEntityType(typeof(OccurrenceStatusChange));
        var complement = context.Model.FindEntityType(typeof(OccurrenceComplement));
        var forecast = context.Model.FindEntityType(typeof(OccurrenceServiceForecast));

        Assert.NotNull(category);
        Assert.NotNull(occurrence);
        Assert.NotNull(statusHistory);
        Assert.NotNull(complement);
        Assert.NotNull(forecast);

        Assert.Equal("occurrence_categories", category!.GetTableName());
        Assert.Equal("occurrences", occurrence!.GetTableName());
        Assert.Equal("occurrence_status_history", statusHistory!.GetTableName());
        Assert.Equal("occurrence_complements", complement!.GetTableName());
        Assert.Equal("occurrence_service_forecasts", forecast!.GetTableName());

        var publicCodeProperty = occurrence.FindProperty(nameof(Occurrence.PublicCode));
        var statusProperty = occurrence.FindProperty(nameof(Occurrence.Status));
        var locationProperty = occurrence.FindProperty(nameof(Occurrence.Location));

        Assert.NotNull(publicCodeProperty);
        Assert.NotNull(statusProperty);
        Assert.NotNull(locationProperty);
        Assert.Equal(typeof(string), publicCodeProperty!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), statusProperty!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(Point), locationProperty!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal("geography (point, 4326)", locationProperty.GetColumnType());
    }

    [Fact]
    public void Occurrence_model_has_required_query_and_spatial_indexes()
    {
        using var context = CreateContext();
        var occurrence = context.Model.FindEntityType(typeof(Occurrence))!;
        var indexes = occurrence.GetIndexes().ToList();

        Assert.Contains(indexes, index =>
            index.IsUnique && HasProperty(index, nameof(Occurrence.PublicCode)));
        Assert.Contains(indexes, index => HasProperty(index, nameof(Occurrence.AuthorUserId)));
        Assert.Contains(indexes, index => HasProperty(index, nameof(Occurrence.Status)));
        Assert.Contains(indexes, index => HasProperty(index, nameof(Occurrence.CategoryId)));
        Assert.Contains(indexes, index => HasProperty(index, nameof(Occurrence.CityId)));
        Assert.Contains(indexes, index => HasProperty(index, nameof(Occurrence.CreatedAt)));

        var spatialIndex = Assert.Single(indexes.Where(index =>
            HasProperty(index, nameof(Occurrence.Location))));

        Assert.Equal("ix_occurrences_location_gist", spatialIndex.GetDatabaseName());
        Assert.Equal("gist", spatialIndex.FindAnnotation("Npgsql:IndexMethod")?.Value);
    }

    [Fact]
    public void Occurrence_author_and_category_are_restrict_foreign_keys()
    {
        using var context = CreateContext();
        var occurrence = context.Model.FindEntityType(typeof(Occurrence))!;

        var authorForeignKey = Assert.Single(occurrence.GetForeignKeys().Where(foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(Occurrence.AuthorUserId))));
        var categoryForeignKey = Assert.Single(occurrence.GetForeignKeys().Where(foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(Occurrence.CategoryId))));

        Assert.Equal(DeleteBehavior.Restrict, authorForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, categoryForeignKey.DeleteBehavior);
    }

    private static bool HasProperty(
        Microsoft.EntityFrameworkCore.Metadata.IReadOnlyIndex index,
        string propertyName) =>
        index.Properties.Any(property => property.Name == propertyName);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=cidademdia_model_tests;Username=postgres;Password=postgres",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }
}
