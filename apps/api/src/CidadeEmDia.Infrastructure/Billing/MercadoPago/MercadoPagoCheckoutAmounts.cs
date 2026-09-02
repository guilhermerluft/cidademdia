namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

public sealed record MercadoPagoCheckoutAmountsResult(
    long RecurringAmountCents,
    long InitialAmountCents,
    bool SignupFeeIncluded);

public static class MercadoPagoCheckoutAmounts
{
    public static MercadoPagoCheckoutAmountsResult Calculate(
        long recurringAmountCents,
        long signupFeeCents,
        bool hasPaidSignupFee)
    {
        if (recurringAmountCents <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(recurringAmountCents));

        if (signupFeeCents < 0)
            throw new ArgumentOutOfRangeException(
                nameof(signupFeeCents));

        var signupFeeIncluded =
            !hasPaidSignupFee &&
            signupFeeCents > 0;

        var initialAmountCents = signupFeeIncluded
            ? checked(recurringAmountCents + signupFeeCents)
            : recurringAmountCents;

        return new MercadoPagoCheckoutAmountsResult(
            recurringAmountCents,
            initialAmountCents,
            signupFeeIncluded);
    }
}
