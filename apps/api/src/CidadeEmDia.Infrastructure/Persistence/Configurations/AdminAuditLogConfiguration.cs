using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("admin_audit_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired();
        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id");
        builder.Property(x => x.PreviousValue)
            .HasColumnName("previous_value")
            .HasMaxLength(120);
        builder.Property(x => x.NewValue)
            .HasColumnName("new_value")
            .HasMaxLength(120);
        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
