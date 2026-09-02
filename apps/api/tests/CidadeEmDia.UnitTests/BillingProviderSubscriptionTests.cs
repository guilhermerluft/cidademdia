using CidadeEmDia.Domain.Billing;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class BillingProviderSubscriptionTests
{
    [Fact]
    public void MarkReplaced_PreservesHistoryAndStopsBeingCurrent()
    {
        var subscriptionId =
            Guid.NewGuid();

        var binding =
            new BillingProviderSubscription(
                subscriptionId,
                BillingProviders.MercadoPago,
                "preapproval-old",
                subscriptionId.ToString("D"),
                "https://checkout.test/old",
                "authorized",
                recurringAmountCents: 80000,
                initialAmountCents: 110000,
                signupFeeIncluded: true);

        var replacedAt =
            new DateTimeOffset(
                2026,
                12,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        Assert.True(
            binding.IsCurrent);

        Assert.Null(
            binding.EndedAt);

        binding.MarkReplaced(
            replacedAt);

        Assert.False(
            binding.IsCurrent);

        Assert.Equal(
            replacedAt,
            binding.EndedAt);

        // Idempotente: retry não troca a data histórica.
        binding.MarkReplaced(
            replacedAt.AddDays(1));

        Assert.Equal(
            replacedAt,
            binding.EndedAt);
    }

    [Fact]
    public void ScheduledReplacement_HasTargetAndFutureDate()
    {
        var subscriptionId =
            Guid.NewGuid();

        var targetPlanVersionId =
            Guid.NewGuid();

        var scheduledFor =
            new DateTimeOffset(
                2026,
                12,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

        var binding =
            CreateBinding(
                subscriptionId,
                "pre-future");

        binding.MarkScheduledReplacement(
            targetPlanVersionId,
            scheduledFor);

        Assert.False(
            binding.IsCurrent);

        Assert.True(
            binding.IsScheduledReplacement);

        Assert.Null(
            binding.EndedAt);

        Assert.Equal(
            targetPlanVersionId,
            binding.TargetPlanVersionId);

        Assert.Equal(
            scheduledFor,
            binding.ScheduledFor);
    }


    [Fact]
    public void PromoteScheduledReplacement_BecomesCurrent()
    {
        var binding =
            CreateBinding(
                Guid.NewGuid(),
                "pre-future");

        binding.MarkScheduledReplacement(
            Guid.NewGuid(),
            new DateTimeOffset(
                2026,
                12,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

        binding.PromoteScheduledReplacement();

        Assert.True(
            binding.IsCurrent);

        Assert.False(
            binding.IsScheduledReplacement);

        Assert.Null(
            binding.EndedAt);
    }


    [Fact]
    public void AbandonScheduledReplacement_ClosesFutureBinding()
    {
        var binding =
            CreateBinding(
                Guid.NewGuid(),
                "pre-future");

        binding.MarkScheduledReplacement(
            Guid.NewGuid(),
            new DateTimeOffset(
                2026,
                12,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));

        var abandonedAt =
            new DateTimeOffset(
                2026,
                9,
                10,
                12,
                0,
                0,
                TimeSpan.Zero);

        binding.AbandonScheduledReplacement(
            abandonedAt);

        Assert.False(
            binding.IsCurrent);

        Assert.False(
            binding.IsScheduledReplacement);

        Assert.Equal(
            abandonedAt,
            binding.EndedAt);
    }


    [Fact]
    public void UpdateRecurringAmount_PreservesInitialAmount()
    {
        var binding =
            new BillingProviderSubscription(
                Guid.NewGuid(),
                BillingProviders.MercadoPago,
                "pre-current",
                Guid.NewGuid().ToString("D"),
                "https://checkout.test/current",
                "authorized",
                recurringAmountCents: 80000,
                initialAmountCents: 110000,
                signupFeeIncluded: true);

        binding.UpdateRecurringAmount(
            147200,
            "authorized");

        Assert.Equal(
            147200,
            binding.RecurringAmountCents);

        Assert.Equal(
            110000,
            binding.InitialAmountCents);

        Assert.Equal(
            "authorized",
            binding.ProviderStatus);
    }

    private static BillingProviderSubscription CreateBinding(
        Guid subscriptionId,
        string providerSubscriptionId) =>
        new(
            subscriptionId,
            BillingProviders.MercadoPago,
            providerSubscriptionId,
            subscriptionId.ToString("D"),
            "https://checkout.test/replacement",
            "pending",
            recurringAmountCents: 55000,
            initialAmountCents: 55000,
            signupFeeIncluded: false);

}
