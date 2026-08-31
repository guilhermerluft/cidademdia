using CidadeEmDia.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class PlanCategoryConfiguration : IEntityTypeConfiguration<PlanCategory>
{
    public void Configure(EntityTypeBuilder<PlanCategory> builder)
    {
        builder.ToTable("plan_categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.BillingIntervalMonths).HasColumnName("billing_interval_months").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

internal sealed class PlanOfferConfiguration : IEntityTypeConfiguration<PlanOffer>
{
    public void Configure(EntityTypeBuilder<PlanOffer> builder)
    {
        builder.ToTable("plan_offers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => new { x.PlanId, x.CategoryId }).IsUnique();
        builder.HasOne(x => x.Plan).WithMany(x => x.Offers).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Category).WithMany(x => x.Offers).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.ToTable("plan_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanOfferId).HasColumnName("plan_offer_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.PriceCents).HasColumnName("price_cents").IsRequired();
        builder.Property(x => x.SignupFeeCents).HasColumnName("signup_fee_cents").IsRequired();
        builder.Property(x => x.SubaccountLimit).HasColumnName("subaccount_limit").IsRequired();
        builder.Property(x => x.MonthlyPublicationLimit).HasColumnName("monthly_publication_limit").IsRequired();
        builder.Property(x => x.MarketingReferencePriceCents).HasColumnName("marketing_reference_price_cents");
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => new { x.PlanOfferId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.PlanOfferId, x.EffectiveFrom });
        builder.HasOne(x => x.PlanOffer).WithMany(x => x.Versions).HasForeignKey(x => x.PlanOfferId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BillingCustomerConfiguration : IEntityTypeConfiguration<BillingCustomer>
{
    public void Configure(EntityTypeBuilder<BillingCustomer> builder)
    {
        builder.ToTable("billing_customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MasterUserId).HasColumnName("master_user_id").IsRequired();
        builder.Property(x => x.SignupFeePaidAt).HasColumnName("signup_fee_paid_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.MasterUserId).IsUnique();
        builder.HasOne(x => x.MasterUser).WithMany().HasForeignKey(x => x.MasterUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MasterUserId).HasColumnName("master_user_id").IsRequired();
        builder.Property(x => x.PlanVersionId).HasColumnName("plan_version_id").IsRequired();
        builder.Property(x => x.PendingPlanVersionId).HasColumnName("pending_plan_version_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CurrentPeriodStart).HasColumnName("current_period_start").IsRequired();
        builder.Property(x => x.CurrentPeriodEnd).HasColumnName("current_period_end").IsRequired();
        builder.Property(x => x.PastDueAt).HasColumnName("past_due_at");
        builder.Property(x => x.GracePeriodEndsAt).HasColumnName("grace_period_ends_at");
        builder.Property(x => x.CancelAtPeriodEnd).HasColumnName("cancel_at_period_end").IsRequired();
        builder.Property(x => x.CanceledAt).HasColumnName("canceled_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => new { x.MasterUserId, x.Status });
        builder.HasIndex(x => x.CurrentPeriodEnd);
        builder.HasOne(x => x.MasterUser).WithMany().HasForeignKey(x => x.MasterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PlanVersion).WithMany().HasForeignKey(x => x.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PendingPlanVersion).WithMany().HasForeignKey(x => x.PendingPlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
{
    public void Configure(EntityTypeBuilder<UsageCounter> builder)
    {
        builder.ToTable("usage_counters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        builder.Property(x => x.WindowStart).HasColumnName("window_start").IsRequired();
        builder.Property(x => x.WindowEnd).HasColumnName("window_end").IsRequired();
        builder.Property(x => x.PublicationCount).HasColumnName("publication_count").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => new { x.SubscriptionId, x.WindowStart }).IsUnique();
        builder.HasOne(x => x.Subscription).WithMany(x => x.UsageCounters).HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
