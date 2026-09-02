using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CidadeEmDia.IntegrationTests;

public sealed class AdministrationPersistenceModelTests
{
    [Fact]
    public void Admin_audit_model_uses_expected_table_and_indexes()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(AdminAuditLog))!;

        Assert.Equal("admin_audit_logs", entity.GetTableName());
        Assert.Contains(
            entity.GetIndexes(),
            index => HasProperties(index, nameof(AdminAuditLog.ActorUserId)));
        Assert.Contains(
            entity.GetIndexes(),
            index => HasProperties(index, nameof(AdminAuditLog.OccurredAt)));
        Assert.Contains(
            entity.GetIndexes(),
            index => HasProperties(index, nameof(AdminAuditLog.EntityType), nameof(AdminAuditLog.EntityId)));
    }

    [Fact]
    public void Admin_audit_actor_delete_is_restricted()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(AdminAuditLog))!;
        var actorForeignKey = Assert.Single(
            entity.GetForeignKeys(),
            fk => fk.Properties.Any(property => property.Name == nameof(AdminAuditLog.ActorUserId)));

        Assert.Equal(DeleteBehavior.Restrict, actorForeignKey.DeleteBehavior);
    }

    [Fact]
    public void Admin_audit_recent_query_is_translatable_by_npgsql()
    {
        using var context = CreateContext();

        var query = context.AdminAuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Take(20);

        var sql = query.ToQueryString();

        Assert.Contains("admin_audit_logs", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
    }

    private static bool HasProperties(
        Microsoft.EntityFrameworkCore.Metadata.IReadOnlyIndex index,
        params string[] propertyNames) =>
        index.Properties.Select(property => property.Name)
            .SequenceEqual(propertyNames);

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
