using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Content;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class ContentDomainTests
{
    [Fact]
    public void TextPost_RequiresBodyToPublish()
    {
        var post = CreatePost(PostTypeKeys.Text, body: null);
        AddFeedPlacement(post);

        var exception = Assert.Throws<DomainException>(() =>
            post.Publish([], DateTimeOffset.UtcNow));

        Assert.Equal("post_body_required", exception.Message);
    }

    [Fact]
    public void LinkPost_RequiresAbsoluteHttpUrl()
    {
        var post = CreatePost(
            PostTypeKeys.Link,
            linkUrl: "javascript:alert(1)");
        AddFeedPlacement(post);

        var exception = Assert.Throws<DomainException>(() =>
            post.Publish([], DateTimeOffset.UtcNow));

        Assert.Equal("post_link_invalid", exception.Message);
    }

    [Fact]
    public void ImagePost_RequiresExactlyOneReadyImage()
    {
        var post = CreatePost(PostTypeKeys.Image);
        AddFeedPlacement(post);

        var exception = Assert.Throws<DomainException>(() =>
            post.Publish(["video/mp4"], DateTimeOffset.UtcNow));

        Assert.Equal("post_image_media_required", exception.Message);
    }

    [Fact]
    public void CarouselPost_RequiresAtLeastTwoReadyMedia()
    {
        var post = CreatePost(PostTypeKeys.Carousel);
        AddFeedPlacement(post);

        var exception = Assert.Throws<DomainException>(() =>
            post.Publish(["image/jpeg"], DateTimeOffset.UtcNow));

        Assert.Equal("post_carousel_media_required", exception.Message);
    }

    [Fact]
    public void PublishedPost_IsIdempotentAndCanBeArchived()
    {
        var publishedAt = new DateTimeOffset(
            2026,
            9,
            2,
            1,
            0,
            0,
            TimeSpan.Zero);
        var post = CreatePost(PostTypeKeys.Text, body: "Conteúdo CidadeEmDia");
        AddFeedPlacement(post);

        post.Publish([], publishedAt);
        post.Publish([], publishedAt.AddMinutes(1));

        Assert.Equal(PostStatusKeys.Published, post.Status);
        Assert.Equal(publishedAt, post.PublishedAt);

        var archivedAt = publishedAt.AddHours(1);
        post.Archive(archivedAt);

        Assert.Equal(PostStatusKeys.Archived, post.Status);
        Assert.Equal(archivedAt, post.ArchivedAt);
    }

    [Fact]
    public void Placement_OnlyAcceptsApprovedKeys()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new PostPlacement(
                Guid.NewGuid(),
                "invented-placement",
                0,
                0));

        Assert.Equal("post_placement_not_supported", exception.Message);
    }

    [Theory]
    [InlineData(PostPlacementKeys.Feed)]
    [InlineData(PostPlacementKeys.Horizontal)]
    [InlineData(PostPlacementKeys.Vertical)]
    [InlineData(PostPlacementKeys.Hero)]
    public void Placement_AcceptsApprovedKeys(string placementKey)
    {
        var placement = new PostPlacement(
            Guid.NewGuid(),
            placementKey,
            10,
            2);

        Assert.Equal(placementKey, placement.PlacementKey);
        Assert.Equal(10, placement.Priority);
        Assert.Equal(2, placement.DisplayOrder);
    }

    private static Post CreatePost(
        string type,
        string? body = null,
        string? linkUrl = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            null,
            body,
            linkUrl);

    private static void AddFeedPlacement(Post post) =>
        post.Placements.Add(
            new PostPlacement(
                post.Id,
                PostPlacementKeys.Feed,
                0,
                0));
}
