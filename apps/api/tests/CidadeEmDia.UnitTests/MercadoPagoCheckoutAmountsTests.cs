using CidadeEmDia.Infrastructure.Billing.MercadoPago;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class MercadoPagoCheckoutAmountsTests
{
    [Fact]
    public void Calculate_IncludesSignupFeeWhenStillDue()
    {
        var result =
            MercadoPagoCheckoutAmounts.Calculate(
                recurringAmountCents: 80000,
                signupFeeCents: 30000,
                hasPaidSignupFee: false);

        Assert.Equal(
            80000,
            result.RecurringAmountCents);

        Assert.Equal(
            110000,
            result.InitialAmountCents);

        Assert.True(
            result.SignupFeeIncluded);
    }

    [Fact]
    public void Calculate_DoesNotChargeSignupFeeTwice()
    {
        var result =
            MercadoPagoCheckoutAmounts.Calculate(
                recurringAmountCents: 80000,
                signupFeeCents: 30000,
                hasPaidSignupFee: true);

        Assert.Equal(
            80000,
            result.RecurringAmountCents);

        Assert.Equal(
            80000,
            result.InitialAmountCents);

        Assert.False(
            result.SignupFeeIncluded);
    }

    [Fact]
    public void Calculate_ZeroSignupFeeKeepsRecurringAmount()
    {
        var result =
            MercadoPagoCheckoutAmounts.Calculate(
                recurringAmountCents: 220000,
                signupFeeCents: 0,
                hasPaidSignupFee: false);

        Assert.Equal(
            220000,
            result.RecurringAmountCents);

        Assert.Equal(
            220000,
            result.InitialAmountCents);

        Assert.False(
            result.SignupFeeIncluded);
    }
}
