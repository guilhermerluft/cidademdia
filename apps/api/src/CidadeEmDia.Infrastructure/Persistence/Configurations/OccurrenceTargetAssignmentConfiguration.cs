using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceTargetAssignmentConfiguration : IEntityTypeConfiguration<OccurrenceTargetAssignment>
{
    public void Configure(EntityTypeBuilder<OccurrenceTargetAssignment> builder)
    {
        builder.ToTable("occurrence_target_assignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OccurrenceTargetId)
            .HasColumnName("occurrence_target_id")
            .IsRequired();
        builder.Property(x => x.MasterSubaccountId)
            .HasColumnName("master_subaccount_id")
            .IsRequired();
        builder.Property(x => x.AssignedByMasterUserId)
            .HasColumnName("assigned_by_master_user_id")
            .IsRequired();
        builder.Property(x => x.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.OccurrenceTargetId).IsUnique();
        builder.HasIndex(x => x.MasterSubaccountId);
        builder.HasIndex(x => new { x.MasterSubaccountId, x.AssignedAt });

        builder.HasOne(x => x.OccurrenceTarget)
            .WithMany()
            .HasForeignKey(x => x.OccurrenceTargetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MasterSubaccount)
            .WithMany()
            .HasForeignKey(x => x.MasterSubaccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedByMasterUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedByMasterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
