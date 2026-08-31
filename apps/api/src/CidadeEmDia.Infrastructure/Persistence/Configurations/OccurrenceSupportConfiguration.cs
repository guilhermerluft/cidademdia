using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceSupportConfiguration
    : IEntityTypeConfiguration<OccurrenceSupport>
{
    public void Configure(EntityTypeBuilder<OccurrenceSupport> builder)
    {
        builder.ToTable("occurrence_supports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.OccurrenceId)
            .HasColumnName("occurrence_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new { x.OccurrenceId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_occurrence_supports_occurrence_user");

        builder.HasIndex(x => x.OccurrenceId)
            .HasDatabaseName("ix_occurrence_supports_occurrence_id");

        builder.HasOne<Occurrence>()
            .WithMany()
            .HasForeignKey(x => x.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
