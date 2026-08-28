using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class SubaccountInvitationConfiguration : IEntityTypeConfiguration<SubaccountInvitation>
{
    public void Configure(EntityTypeBuilder<SubaccountInvitation> builder)
    {
        builder.ToTable("subaccount_invitations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MasterUserId).HasColumnName("master_user_id").IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.PermissionKeysJson).HasColumnName("permission_keys_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.MasterUserId, x.NormalizedEmail });

        builder.HasOne(x => x.MasterUser)
            .WithMany()
            .HasForeignKey(x => x.MasterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
