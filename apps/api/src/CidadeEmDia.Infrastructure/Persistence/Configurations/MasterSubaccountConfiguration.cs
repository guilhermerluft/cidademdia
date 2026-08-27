using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class MasterSubaccountConfiguration : IEntityTypeConfiguration<MasterSubaccount>
{
    public void Configure(EntityTypeBuilder<MasterSubaccount> builder)
    {
        builder.ToTable("master_subaccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MasterUserId).HasColumnName("master_user_id").IsRequired();
        builder.Property(x => x.SubaccountUserId).HasColumnName("subaccount_user_id").IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.MasterUserId, x.SubaccountUserId }).IsUnique();
        builder.HasIndex(x => new { x.MasterUserId, x.Status });

        builder.HasOne(x => x.MasterUser)
            .WithMany()
            .HasForeignKey(x => x.MasterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubaccountUser)
            .WithMany()
            .HasForeignKey(x => x.SubaccountUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
