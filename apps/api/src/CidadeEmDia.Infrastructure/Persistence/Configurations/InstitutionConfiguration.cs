using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Institutions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(180).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ScopeLevel).HasColumnName("scope_level").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Cnpj).HasColumnName("cnpj").HasMaxLength(14);
        builder.Property(x => x.OfficialEmail).HasColumnName("official_email").HasMaxLength(320);
        builder.Property(x => x.OfficialDomain).HasColumnName("official_domain").HasMaxLength(255);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(5000);
        builder.Property(x => x.LogoMediaId).HasColumnName("logo_media_id");
        builder.Property(x => x.CityId).HasColumnName("city_id");
        builder.Property(x => x.StateCode).HasColumnName("state_code").HasMaxLength(2);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique()
            .HasDatabaseName("ux_institutions_slug");

        builder.HasIndex(x => x.Cnpj)
            .IsUnique()
            .HasFilter("cnpj IS NOT NULL")
            .HasDatabaseName("ux_institutions_cnpj");

        builder.HasIndex(x => new { x.Status, x.Type, x.StateCode })
            .HasDatabaseName("ix_institutions_status_type_state");

        builder.HasMany(x => x.Jurisdictions)
            .WithOne(x => x.Institution)
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Representatives)
            .WithOne(x => x.Institution)
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Memberships)
            .WithOne(x => x.Institution)
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Invites)
            .WithOne(x => x.Institution)
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InstitutionJurisdictionConfiguration : IEntityTypeConfiguration<InstitutionJurisdiction>
{
    public void Configure(EntityTypeBuilder<InstitutionJurisdiction> builder)
    {
        builder.ToTable("institution_jurisdictions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstitutionId).HasColumnName("institution_id").IsRequired();
        builder.Property(x => x.JurisdictionType).HasColumnName("jurisdiction_type").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CityId).HasColumnName("city_id");
        builder.Property(x => x.StateCode).HasColumnName("state_code").HasMaxLength(2);
        builder.Property(x => x.CustomAreaLabel).HasColumnName("custom_area_label").HasMaxLength(180);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.InstitutionId, x.JurisdictionType })
            .HasDatabaseName("ix_institution_jurisdictions_institution_type");
        builder.HasIndex(x => new { x.StateCode, x.CityId })
            .HasDatabaseName("ix_institution_jurisdictions_state_city");
    }
}

internal sealed class InstitutionRepresentativeConfiguration : IEntityTypeConfiguration<InstitutionRepresentative>
{
    public void Configure(EntityTypeBuilder<InstitutionRepresentative> builder)
    {
        builder.ToTable("representatives");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstitutionId).HasColumnName("institution_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(180).IsRequired();
        builder.Property(x => x.PublicRole).HasColumnName("public_role").HasMaxLength(120).IsRequired();
        builder.Property(x => x.OfficialEmail).HasColumnName("official_email").HasMaxLength(320);
        builder.Property(x => x.PhotoMediaId).HasColumnName("photo_media_id");
        builder.Property(x => x.MandateStart).HasColumnName("mandate_start").HasColumnType("date");
        builder.Property(x => x.MandateEnd).HasColumnName("mandate_end").HasColumnType("date");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.ProfileStatus).HasColumnName("profile_status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique()
            .HasDatabaseName("ux_representatives_slug");

        builder.HasIndex(x => x.AccountId)
            .IsUnique()
            .HasFilter("account_id IS NOT NULL")
            .HasDatabaseName("ux_representatives_account");

        builder.HasIndex(x => new { x.InstitutionId, x.ProfileStatus, x.DisplayOrder })
            .HasDatabaseName("ix_representatives_institution_status_order");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InstitutionMembershipConfiguration : IEntityTypeConfiguration<InstitutionMembership>
{
    public void Configure(EntityTypeBuilder<InstitutionMembership> builder)
    {
        builder.ToTable("institution_memberships");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstitutionId).HasColumnName("institution_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.RepresentativeId).HasColumnName("representative_id");
        builder.Property(x => x.MembershipRole).HasColumnName("membership_role").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.InstitutionId, x.UserId, x.MembershipRole })
            .IsUnique()
            .HasDatabaseName("ux_institution_membership_user_role");

        builder.HasIndex(x => new { x.UserId, x.Status })
            .HasDatabaseName("ix_institution_membership_user_status");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Representative)
            .WithMany()
            .HasForeignKey(x => x.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InstitutionInviteConfiguration : IEntityTypeConfiguration<InstitutionInvite>
{
    public void Configure(EntityTypeBuilder<InstitutionInvite> builder)
    {
        builder.ToTable("institution_invites");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstitutionId).HasColumnName("institution_id").IsRequired();
        builder.Property(x => x.RepresentativeId).HasColumnName("representative_id");
        builder.Property(x => x.ExpectedEmail).HasColumnName("expected_email").HasMaxLength(320);
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_institution_invites_token_hash");

        builder.HasIndex(x => new { x.InstitutionId, x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_institution_invites_institution_status_expiry");

        builder.HasIndex(x => new { x.RepresentativeId, x.Status })
            .HasDatabaseName("ix_institution_invites_representative_status");

        builder.HasOne(x => x.Representative)
            .WithMany()
            .HasForeignKey(x => x.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
