using CidadeEmDia.Domain.Content;
using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PublisherUserId).HasColumnName("publisher_user_id").IsRequired();
        builder.Property(x => x.MasterUserId).HasColumnName("master_user_id");
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(5000);
        builder.Property(x => x.LinkUrl).HasColumnName("link_url").HasMaxLength(2048);
        builder.Property(x => x.PublishedAt).HasColumnName("published_at");
        builder.Property(x => x.ArchivedAt).HasColumnName("archived_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.Status, x.PublishedAt })
            .HasDatabaseName("ix_posts_status_published_at");
        builder.HasIndex(x => new { x.MasterUserId, x.Status, x.CreatedAt })
            .HasDatabaseName("ix_posts_master_status_created");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.PublisherUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.MasterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Media)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Placements)
            .WithOne(x => x.Post)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("post_media");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.UploaderUserId).HasColumnName("uploader_user_id").IsRequired();
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ExpectedSizeBytes).HasColumnName("expected_size_bytes").IsRequired();
        builder.Property(x => x.ActualSizeBytes).HasColumnName("actual_size_bytes");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(x => x.ReadyAt).HasColumnName("ready_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.ObjectKey)
            .IsUnique()
            .HasDatabaseName("ux_post_media_object_key");
        builder.HasIndex(x => new { x.PostId, x.SortOrder })
            .HasDatabaseName("ix_post_media_post_sort_order");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UploaderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PostPlacementConfiguration : IEntityTypeConfiguration<PostPlacement>
{
    public void Configure(EntityTypeBuilder<PostPlacement> builder)
    {
        builder.ToTable("post_placements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.PlacementKey).HasColumnName("placement_key").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.PostId, x.PlacementKey })
            .IsUnique()
            .HasDatabaseName("ux_post_placements_post_key");

        builder.HasIndex(x => new { x.PlacementKey, x.Priority, x.DisplayOrder })
            .HasDatabaseName("ix_post_placements_key_priority_order");
    }
}
