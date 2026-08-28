using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CidadeEmDia.IntegrationTests;

public sealed class OccurrenceTargetPersistenceModelTests
{
    [Fact]
    public void Occurrence_target_model_has_expected_table_status_conversion_and_indexes()
    {
        using var context = CreateContext();
        var target = context.Model.FindEntityType(typeof(OccurrenceTarget));

        Assert.NotNull(target);
        Assert.Equal("occurrence_targets", target!.GetTableName());

        var status = target.FindProperty(nameof(OccurrenceTarget.Status));
        Assert.NotNull(status);
        Assert.Equal(typeof(string), status!.GetTypeMapping().Converter?.ProviderClrType);

        Assert.Contains(target.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Any(property => property.Name == nameof(OccurrenceTarget.OccurrenceId))
            && index.Properties.Any(property => property.Name == nameof(OccurrenceTarget.MasterUserId)));

        Assert.Contains(target.GetIndexes(), index =>
            index.Properties.Any(property => property.Name == nameof(OccurrenceTarget.MasterUserId))
            && index.Properties.Any(property => property.Name == nameof(OccurrenceTarget.Status)));
    }

    [Fact]
    public void Occurrence_target_has_cascade_occurrence_and_restrict_master_foreign_keys()
    {
        using var context = CreateContext();
        var target = context.Model.FindEntityType(typeof(OccurrenceTarget))!;

        var occurrenceForeignKey = Assert.Single(target.GetForeignKeys().Where(foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(OccurrenceTarget.OccurrenceId))));
        var masterForeignKey = Assert.Single(target.GetForeignKeys().Where(foreignKey =>
            foreignKey.Properties.Any(property => property.Name == nameof(OccurrenceTarget.MasterUserId))));

        Assert.Equal(DeleteBehavior.Cascade, occurrenceForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, masterForeignKey.DeleteBehavior);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=cidademdia_target_model_tests;Username=postgres;Password=postgres",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }
}
