using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceTargetConfiguration : IEntityTypeConfiguration<OccurrenceTarget>
{
    public void Configure(EntityTypeBuilder<OccurrenceTarget> builder)
    {
        var statusConverter = new ValueConverter<OccurrenceTargetStatus, string>(
            status => status.Value,
            value => OccurrenceTargetStatus.From(value));

        builder.ToTable("occurrence_targets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OccurrenceId)
            .HasColumnName("occurrence_id")
            .IsRequired();
        builder.Property(x => x.MasterUserId)
            .HasColumnName("master_user_id")
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(1000);
        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(x => x.RejectedAt).HasColumnName("rejected_at");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new { x.OccurrenceId, x.MasterUserId })
            .IsUnique();
        builder.HasIndex(x => new { x.MasterUserId, x.Status });
        builder.HasIndex(x => x.SentAt);

        builder.HasOne(x => x.Occurrence)
            .WithMany(x => x.Targets)
            .HasForeignKey(x => x.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MasterUser)
            .WithMany()
            .HasForeignKey(x => x.MasterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
