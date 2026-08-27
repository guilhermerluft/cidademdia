using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class MasterSubaccountPermissionConfiguration : IEntityTypeConfiguration<MasterSubaccountPermission>
{
    public void Configure(EntityTypeBuilder<MasterSubaccountPermission> builder)
    {
        builder.ToTable("master_subaccount_permissions");
        builder.HasKey(x => new { x.MasterSubaccountId, x.PermissionId });

        builder.Property(x => x.MasterSubaccountId).HasColumnName("master_subaccount_id");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.MasterSubaccount)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.MasterSubaccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
