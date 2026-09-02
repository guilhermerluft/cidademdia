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

internal sealed class BillingProviderSubscriptionConfiguration
    : IEntityTypeConfiguration<BillingProviderSubscription>
{
    public void Configure(EntityTypeBuilder<BillingProviderSubscription> builder)
    {
        builder.ToTable("billing_provider_subscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionId)
            .HasColumnName("subscription_id")
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ProviderSubscriptionId)
            .HasColumnName("provider_subscription_id")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.CheckoutUrl)
            .HasColumnName("checkout_url")
            .HasMaxLength(1200)
            .IsRequired();

        builder.Property(x => x.ProviderStatus)
            .HasColumnName("provider_status")
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(x => x.RecurringAmountCents)
            .HasColumnName("recurring_amount_cents")
            .IsRequired();

        builder.Property(x => x.InitialAmountCents)
            .HasColumnName("initial_amount_cents")
            .IsRequired();

        builder.Property(x => x.SignupFeeIncluded)
            .HasColumnName("signup_fee_included")
            .IsRequired();

        builder.Property(x => x.FirstApprovedPaymentAt)
            .HasColumnName("first_approved_payment_at");

        builder.Property(x => x.RecurringAmountSynchronizedAt)
            .HasColumnName("recurring_amount_synchronized_at");

        builder.Property(x => x.IsCurrent)
            .HasColumnName("is_current")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(x => x.TargetPlanVersionId)
            .HasColumnName("target_plan_version_id");

        builder.Property(x => x.ScheduledFor)
            .HasColumnName("scheduled_for");


        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

                builder.HasIndex(
                x => new
                {
                    x.SubscriptionId,
                    x.IsCurrent
                })
            .HasDatabaseName(
                "UX_mp_binding_scheduled")
            .HasFilter(
                "is_current = false AND ended_at IS NULL AND target_plan_version_id IS NOT NULL")
            .IsUnique();

builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName(
                "UX_mp_binding_subscription_current")
            .HasFilter("is_current = true")
            .IsUnique();

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderSubscriptionId
            })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.Provider,
                x.ExternalReference
            })
            .HasDatabaseName(
                "UX_mp_binding_externalref")
            .IsUnique();

                builder.HasIndex(
                x => x.TargetPlanVersionId)
            .HasDatabaseName(
                "IX_mp_binding_target_plan");

        builder.HasOne<PlanVersion>()
            .WithMany()
            .HasForeignKey(
                x => x.TargetPlanVersionId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_mp_binding_target_plan");

builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BillingPaymentConfiguration
    : IEntityTypeConfiguration<BillingPayment>
{
    public void Configure(EntityTypeBuilder<BillingPayment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionId)
            .HasColumnName("subscription_id")
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ProviderPaymentId)
            .HasColumnName("provider_payment_id")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.ProviderAuthorizedPaymentId)
            .HasColumnName("provider_authorized_payment_id")
            .HasMaxLength(160);

        builder.Property(x => x.AmountCents)
            .HasColumnName("amount_cents")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(x => x.StatusDetail)
            .HasColumnName("status_detail")
            .HasMaxLength(120);

        builder.Property(x => x.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderPaymentId
            })
            .IsUnique();

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderAuthorizedPaymentId
            });

        builder.HasIndex(x => x.SubscriptionId);

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BillingPaymentEventConfiguration
    : IEntityTypeConfiguration<BillingPaymentEvent>
{
    public void Configure(EntityTypeBuilder<BillingPaymentEvent> builder)
    {
        builder.ToTable("payment_events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ProviderEventId)
            .HasColumnName("provider_event_id")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(120);

        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.LiveMode)
            .HasColumnName("live_mode")
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(x => x.ProcessingError)
            .HasColumnName("processing_error")
            .HasMaxLength(240);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.Provider,
                x.ProviderEventId
            })
            .IsUnique();

        builder.HasIndex(x => new
            {
                x.Provider,
                x.Type,
                x.ResourceId
            });

        builder.HasIndex(x => x.ProcessedAt);
    }
}
