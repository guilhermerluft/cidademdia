using CidadeEmDia.Domain.Content;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CidadeEmDia.IntegrationTests;

public sealed class ContentPersistenceModelTests
{
    [Fact]
    public void Content_model_uses_expected_tables()
    {
        using var context = CreateContext();

        var post = context.Model.FindEntityType(typeof(Post));
        var media = context.Model.FindEntityType(typeof(PostMedia));
        var placement = context.Model.FindEntityType(typeof(PostPlacement));

        Assert.NotNull(post);
        Assert.NotNull(media);
        Assert.NotNull(placement);
        Assert.Equal("posts", post!.GetTableName());
        Assert.Equal("post_media", media!.GetTableName());
        Assert.Equal("post_placements", placement!.GetTableName());
    }

    [Fact]
    public void Content_model_has_required_unique_indexes()
    {
        using var context = CreateContext();

        var media = context.Model.FindEntityType(typeof(PostMedia))!;
        var placement = context.Model.FindEntityType(typeof(PostPlacement))!;

        Assert.Contains(
            media.GetIndexes(),
            index =>
                index.IsUnique
                && index.GetDatabaseName() == "ux_post_media_object_key"
                && HasProperties(index, nameof(PostMedia.ObjectKey)));

        Assert.Contains(
            placement.GetIndexes(),
            index =>
                index.IsUnique
                && index.GetDatabaseName() == "ux_post_placements_post_key"
                && HasProperties(
                    index,
                    nameof(PostPlacement.PostId),
                    nameof(PostPlacement.PlacementKey)));
    }

    [Fact]
    public void Content_foreign_keys_keep_users_and_cascade_post_children()
    {
        using var context = CreateContext();

        var post = context.Model.FindEntityType(typeof(Post))!;
        var media = context.Model.FindEntityType(typeof(PostMedia))!;
        var placement = context.Model.FindEntityType(typeof(PostPlacement))!;

        var postPublisher = Assert.Single(
            post.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(
                property => property.Name == nameof(Post.PublisherUserId)));
        var postMaster = Assert.Single(
            post.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(
                property => property.Name == nameof(Post.MasterUserId)));
        var mediaPost = Assert.Single(
            media.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(
                property => property.Name == nameof(PostMedia.PostId)));
        var placementPost = Assert.Single(
            placement.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(
                property => property.Name == nameof(PostPlacement.PostId)));

        Assert.Equal(DeleteBehavior.Restrict, postPublisher.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, postMaster.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, mediaPost.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, placementPost.DeleteBehavior);
    }

    [Fact]
    public void Feed_cursor_query_is_translatable_by_npgsql()
    {
        using var context = CreateContext();

        const string placementKey = PostPlacementKeys.Feed;
        const int cursorPriority = 10;
        const int cursorDisplayOrder = 2;
        var cursorPublishedAt = new DateTimeOffset(2026, 9, 2, 1, 0, 0, TimeSpan.Zero);
        var cursorPostId = Guid.Parse("f66e2833-5148-4f38-9e1a-e6abc449847f");

        var query = context.Posts
            .AsNoTracking()
            .Where(post =>
                post.Status == PostStatusKeys.Published &&
                post.PublishedAt != null &&
                post.Placements.Any(p => p.PlacementKey == placementKey))
            .Where(post =>
                post.Placements
                    .Where(p => p.PlacementKey == placementKey)
                    .Select(p => p.Priority)
                    .First() < cursorPriority
                || (post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.Priority)
                        .First() == cursorPriority
                    && post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.DisplayOrder)
                        .First() > cursorDisplayOrder)
                || (post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.Priority)
                        .First() == cursorPriority
                    && post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.DisplayOrder)
                        .First() == cursorDisplayOrder
                    && post.PublishedAt < cursorPublishedAt)
                || (post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.Priority)
                        .First() == cursorPriority
                    && post.Placements
                        .Where(p => p.PlacementKey == placementKey)
                        .Select(p => p.DisplayOrder)
                        .First() == cursorDisplayOrder
                    && post.PublishedAt == cursorPublishedAt
                    && post.Id.CompareTo(cursorPostId) < 0))
            .OrderByDescending(post =>
                post.Placements
                    .Where(p => p.PlacementKey == placementKey)
                    .Select(p => p.Priority)
                    .First())
            .ThenBy(post =>
                post.Placements
                    .Where(p => p.PlacementKey == placementKey)
                    .Select(p => p.DisplayOrder)
                    .First())
            .ThenByDescending(post => post.PublishedAt)
            .ThenByDescending(post => post.Id)
            .Take(21);

        var sql = query.ToQueryString();

        Assert.Contains("post_placements", sql);
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
