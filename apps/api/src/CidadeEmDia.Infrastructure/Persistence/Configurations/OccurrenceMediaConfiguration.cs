using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceMediaConfiguration
    : IEntityTypeConfiguration<OccurrenceMedia>
{
    public void Configure(EntityTypeBuilder<OccurrenceMedia> builder)
    {
        builder.ToTable("occurrence_media");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.UploaderUserId)
            .HasColumnName("uploader_user_id")
            .IsRequired();

        builder.Property(x => x.OccurrenceId)
            .HasColumnName("occurrence_id");

        builder.Property(x => x.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.ExpectedSizeBytes)
            .HasColumnName("expected_size_bytes")
            .IsRequired();

        builder.Property(x => x.ActualSizeBytes)
            .HasColumnName("actual_size_bytes");

        var statusConverter = new ValueConverter<OccurrenceMediaStatus, string>(
            value => value.Value,
            value => OccurrenceMediaStatus.From(value));

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(statusConverter)
            .IsRequired();

        builder.Property(x => x.ReadyAt)
            .HasColumnName("ready_at");

        builder.Property(x => x.AttachedAt)
            .HasColumnName("attached_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.ObjectKey)
            .IsUnique()
            .HasDatabaseName("ux_occurrence_media_object_key");

        builder.HasIndex(x => x.OccurrenceId)
            .HasDatabaseName("ix_occurrence_media_occurrence_id");

        builder.HasIndex(x => new { x.UploaderUserId, x.Status, x.CreatedAt })
            .HasDatabaseName("ix_occurrence_media_uploader_status_created");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UploaderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Occurrence>()
            .WithMany()
            .HasForeignKey(x => x.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
