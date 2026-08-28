using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceCategoryConfiguration : IEntityTypeConfiguration<OccurrenceCategory>
{
    public void Configure(EntityTypeBuilder<OccurrenceCategory> builder)
    {
        var statusConverter = new ValueConverter<OccurrenceCategoryStatus, string>(
            status => status.Value,
            value => OccurrenceCategoryStatus.From(value));

        builder.ToTable("occurrence_categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(x => x.Slug)
            .HasColumnName("slug")
            .HasMaxLength(180)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Status, x.DisplayOrder });
    }
}
