using System.Globalization;
using CidadeEmDia.Application.Billing;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

internal sealed class MercadoPagoWebhookService(
    AppDbContext dbContext,
    IMercadoPagoClient mercadoPagoClient,
    IBillingSubscriptionService billingSubscriptionService,
    ILogger<MercadoPagoWebhookService> logger)
    : IBillingProviderWebhookService
{
    public async Task<BillingProviderWebhookResult> ProcessAsync(
        string providerEventId,
        string type,
        string? action,
        string resourceId,
        string requestId,
        bool liveMode,
        string payloadJson,
        DateTimeOffset? receivedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerEventId) ||
            string.IsNullOrWhiteSpace(type) ||
            string.IsNullOrWhiteSpace(resourceId) ||
            string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(payloadJson))
        {
            return BillingProviderWebhookResult.Failure(
                "invalid_webhook");
        }

        var now =
            receivedAt ??
            DateTimeOffset.UtcNow;

        var paymentEvent =
            await dbContext.BillingPaymentEvents
                .FirstOrDefaultAsync(
                    x =>
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.ProviderEventId ==
                            providerEventId,
                    cancellationToken);

        if (paymentEvent?.IsProcessed == true)
        {
            return BillingProviderWebhookResult
                .DuplicateEvent();
        }

        if (paymentEvent is null)
        {
            paymentEvent =
                new BillingPaymentEvent(
                    BillingProviders.MercadoPago,
                    providerEventId,
                    type,
                    action,
                    resourceId,
                    requestId,
                    liveMode,
                    payloadJson,
                    now);

            dbContext.BillingPaymentEvents.Add(
                paymentEvent);

            try
            {
                await dbContext.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                dbContext.Entry(paymentEvent)
                    .State = EntityState.Detached;

                var concurrent =
                    await dbContext.BillingPaymentEvents
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.Provider ==
                                    BillingProviders.MercadoPago &&
                                x.ProviderEventId ==
                                    providerEventId,
                            cancellationToken);

                if (concurrent?.IsProcessed == true)
                {
                    return BillingProviderWebhookResult
                        .DuplicateEvent();
                }

                logger.LogWarning(
                    exception,
                    "Concurrent Mercado Pago webhook event {ProviderEventId}.",
                    providerEventId);

                return BillingProviderWebhookResult.Retry(
                    "event_in_progress");
            }
        }

        try
        {
            await ProcessResourceAsync(
                paymentEvent.Type,
                paymentEvent.ResourceId,
                now,
                cancellationToken);

            paymentEvent.MarkProcessed(now);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            return BillingProviderWebhookResult.Success();
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago API failed while processing event {ProviderEventId}. Status={StatusCode}.",
                providerEventId,
                exception.StatusCode);

            await MarkFailedAsync(
                paymentEvent,
                "provider_api_failure",
                cancellationToken);

            return BillingProviderWebhookResult.Retry(
                "provider_api_failure");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago network failure on event {ProviderEventId}.",
                providerEventId);

            await MarkFailedAsync(
                paymentEvent,
                "provider_unavailable",
                cancellationToken);

            return BillingProviderWebhookResult.Retry(
                "provider_unavailable");
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago timeout on event {ProviderEventId}.",
                providerEventId);

            await MarkFailedAsync(
                paymentEvent,
                "provider_timeout",
                cancellationToken);

            return BillingProviderWebhookResult.Retry(
                "provider_timeout");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected Mercado Pago webhook processing error {ProviderEventId}.",
                providerEventId);

            await MarkFailedAsync(
                paymentEvent,
                "webhook_processing_failed",
                cancellationToken);

            return BillingProviderWebhookResult.Retry(
                "webhook_processing_failed");
        }
    }

    private async Task ProcessResourceAsync(
        string type,
        string resourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (type.Trim().ToLowerInvariant())
        {
            case "subscription_preapproval":
                await ProcessPreapprovalAsync(
                    resourceId,
                    now,
                    cancellationToken);
                break;

            case "subscription_authorized_payment":
                await ProcessAuthorizedPaymentAsync(
                    resourceId,
                    now,
                    cancellationToken);
                break;

            case "payment":
                var payment =
                    await mercadoPagoClient
                        .GetPaymentAsync(
                            resourceId,
                            cancellationToken);

                await ProcessPaymentAsync(
                    payment,
                    null,
                    null,
                    now,
                    cancellationToken);
                break;

            default:
                logger.LogInformation(
                    "Ignoring unsupported Mercado Pago webhook type {Type}.",
                    type);
                break;
        }
    }

    private async Task ProcessPreapprovalAsync(
        string resourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var provider =
            await mercadoPagoClient
                .GetPreapprovalAsync(
                    resourceId,
                    cancellationToken);

        var binding =
            await dbContext.BillingProviderSubscriptions
                .Include(x => x.Subscription)
                .FirstOrDefaultAsync(
                    x =>
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.ProviderSubscriptionId ==
                            provider.Id,
                    cancellationToken);

        if (binding is null)
        {
            logger.LogInformation(
                "Mercado Pago preapproval {ProviderSubscriptionId} does not belong to CidadeEmDia.",
                provider.Id);

            return;
        }

        if (!string.IsNullOrWhiteSpace(
                provider.ExternalReference) &&
            !string.Equals(
                provider.ExternalReference,
                binding.ExternalReference,
                StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Mercado Pago external_reference mismatch for {ProviderSubscriptionId}.",
                provider.Id);

            return;
        }

        binding.UpdateProviderState(
            string.IsNullOrWhiteSpace(
                provider.Status)
                ? binding.ProviderStatus
                : provider.Status,
            provider.InitPoint);

        if (binding.IsScheduledReplacement)
        {
            if (string.Equals(
                    provider.Status,
                    "canceled",
                    StringComparison.OrdinalIgnoreCase))
            {
                binding.AbandonScheduledReplacement(
                    now);

                return;
            }

            if (string.Equals(
                    provider.Status,
                    "authorized",
                    StringComparison.OrdinalIgnoreCase))
            {
                var delayedReactivation =
                    binding.Subscription.CancelAtPeriodEnd &&
                    now >=
                        binding.Subscription.CurrentPeriodEnd;

                if (!delayedReactivation)
                {
                    await PromoteScheduledReplacementAsync(
                        binding,
                        now,
                        cancellationToken);
                }
            }

            return;
        }

        if (!binding.IsCurrent)
        {
            return;
        }

        if (string.Equals(
                provider.Status,
                "canceled",
                StringComparison.OrdinalIgnoreCase))
        {
            if (binding.Subscription.Status ==
                SubscriptionStatus.Pending)
            {
                await EnsureSucceededAsync(
                    billingSubscriptionService
                        .CancelAsync(
                            binding.Subscription.MasterUserId,
                            now,
                            cancellationToken));

                return;
            }

            if (binding.Subscription.Status is
                SubscriptionStatus.Active or
                SubscriptionStatus.PastDue)
            {
                await EnsureSucceededAsync(
                    billingSubscriptionService
                        .RequestCancellationAsync(
                            binding.Subscription.MasterUserId,
                            cancellationToken));
            }

            return;
        }

        if (string.Equals(
                provider.Status,
                "paused",
                StringComparison.OrdinalIgnoreCase) &&
            binding.Subscription.Status is
                SubscriptionStatus.Active or
                SubscriptionStatus.PastDue)
        {
            await EnsureSucceededAsync(
                billingSubscriptionService
                    .MarkPastDueAsync(
                        binding.Subscription.MasterUserId,
                        now,
                        cancellationToken));
        }

        // "authorized" sozinho não ativa benefício.
        // A ativação exige pagamento aprovado.
    }

    private async Task ProcessAuthorizedPaymentAsync(
        string resourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invoice =
            await mercadoPagoClient
                .GetAuthorizedPaymentAsync(
                    resourceId,
                    cancellationToken);

        if (invoice.Payment is null ||
            invoice.Payment.Id <= 0)
        {
            logger.LogInformation(
                "Authorized payment {AuthorizedPaymentId} has no payment yet.",
                invoice.Id);

            return;
        }

        var payment =
            await mercadoPagoClient
                .GetPaymentAsync(
                    invoice.Payment.Id.ToString(
                        CultureInfo.InvariantCulture),
                    cancellationToken);

        await ProcessPaymentAsync(
            payment,
            invoice.Id.ToString(
                CultureInfo.InvariantCulture),
            invoice.PreapprovalId,
            now,
            cancellationToken);
    }

    private async Task ProcessPaymentAsync(
        MercadoPagoPaymentResponse providerPayment,
        string? providerAuthorizedPaymentId,
        string? providerSubscriptionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var providerPaymentId =
            providerPayment.Id.ToString(
                CultureInfo.InvariantCulture);

        BillingProviderSubscription? binding =
            null;

        if (!string.IsNullOrWhiteSpace(
                providerPayment.ExternalReference))
        {
            binding =
                await dbContext
                    .BillingProviderSubscriptions
                    .Include(x => x.Subscription)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Provider ==
                                BillingProviders.MercadoPago &&
                            x.ExternalReference ==
                                providerPayment.ExternalReference,
                        cancellationToken);
        }

        if (binding is null &&
            !string.IsNullOrWhiteSpace(
                providerSubscriptionId))
        {
            binding =
                await dbContext
                    .BillingProviderSubscriptions
                    .Include(x => x.Subscription)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Provider ==
                                BillingProviders.MercadoPago &&
                            x.ProviderSubscriptionId ==
                                providerSubscriptionId,
                        cancellationToken);
        }

        if (binding is null)
        {
            logger.LogInformation(
                "Mercado Pago payment {ProviderPaymentId} is not linked to CidadeEmDia.",
                providerPaymentId);

            return;
        }

        var incomingAmountCents =
            ToCents(
                providerPayment.TransactionAmount);

        var reactivationAfterBoundary =
            false;

        if (!binding.IsCurrent &&
            binding.IsScheduledReplacement &&
            MercadoPagoPaymentStatuses.IsApproved(
                providerPayment.Status))
        {
            if (!providerPayment.CurrencyId.Equals(
                    "BRL",
                    StringComparison.OrdinalIgnoreCase) ||
                incomingAmountCents !=
                    binding.RecurringAmountCents)
            {
                logger.LogWarning(
                    "Scheduled replacement payment {ProviderPaymentId} has unexpected amount/currency. Replacement not promoted.",
                    providerPaymentId);
            }
            else
            {
                reactivationAfterBoundary =
                    binding.Subscription.CancelAtPeriodEnd &&
                    now >=
                        binding.Subscription.CurrentPeriodEnd;

                await PromoteScheduledReplacementAsync(
                    binding,
                    now,
                    cancellationToken,
                    clearCancellation:
                        !reactivationAfterBoundary);
            }
        }

        var existingPayment =
            await dbContext.BillingPayments
                .FirstOrDefaultAsync(
                    x =>
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.ProviderPaymentId ==
                            providerPaymentId,
                    cancellationToken);

        var wasAlreadyApproved =
            existingPayment is not null &&
            MercadoPagoPaymentStatuses
                .IsApproved(existingPayment.Status);

        var amountCents =
            incomingAmountCents;

        if (existingPayment is null)
        {
            existingPayment =
                new BillingPayment(
                    binding.SubscriptionId,
                    BillingProviders.MercadoPago,
                    providerPaymentId,
                    providerAuthorizedPaymentId,
                    amountCents,
                    providerPayment.CurrencyId,
                    providerPayment.Status,
                    providerPayment.StatusDetail,
                    providerPayment.DateApproved);

            dbContext.BillingPayments.Add(
                existingPayment);
        }
        else
        {
            existingPayment.UpdateFromProvider(
                amountCents,
                providerPayment.CurrencyId,
                providerPayment.Status,
                providerPayment.StatusDetail,
                providerPayment.DateApproved,
                providerAuthorizedPaymentId);
        }

        if (!binding.IsCurrent)
        {
            // Historical/scheduled provider payments are
            // persisted for audit but cannot mutate entitlement.
            return;
        }

        if (MercadoPagoPaymentStatuses.IsApproved(
                providerPayment.Status))
        {
            if (wasAlreadyApproved)
            {
                // Outro evento para o mesmo pagamento aprovado
                // não pode renovar/ativar novamente.
                return;
            }

            var subscription =
                binding.Subscription;

            var expectedAmount =
                subscription.Status ==
                    SubscriptionStatus.Pending
                    ? binding.InitialAmountCents
                    : binding.RecurringAmountCents;

            if (!providerPayment.CurrencyId.Equals(
                    "BRL",
                    StringComparison.OrdinalIgnoreCase) ||
                amountCents != expectedAmount)
            {
                logger.LogWarning(
                    "Approved payment {ProviderPaymentId} has unexpected amount/currency. Expected={ExpectedAmountCents} Actual={ActualAmountCents} Currency={Currency}. No entitlement change.",
                    providerPaymentId,
                    expectedAmount,
                    amountCents,
                    providerPayment.CurrencyId);

                return;
            }

            var approvedAt =
                providerPayment.DateApproved ??
                now;

            if (subscription.Status ==
                SubscriptionStatus.Pending)
            {
                if (binding.RequiresRecurringAmountSynchronization)
                {
                    var updated =
                        await mercadoPagoClient
                            .UpdateRecurringAmountAsync(
                                binding.ProviderSubscriptionId,
                                ToCurrency(
                                    binding.RecurringAmountCents),
                                cancellationToken);

                    binding.UpdateProviderState(
                        string.IsNullOrWhiteSpace(
                            updated.Status)
                            ? binding.ProviderStatus
                            : updated.Status,
                        updated.InitPoint);

                    binding.MarkRecurringAmountSynchronized(
                        approvedAt);
                }

                binding.MarkFirstApprovedPayment(
                    approvedAt);

                await EnsureSucceededAsync(
                    billingSubscriptionService
                        .ActivateAsync(
                            subscription.MasterUserId,
                            binding.SignupFeeIncluded,
                            approvedAt,
                            cancellationToken));

                return;
            }

            if (subscription.Status is
                SubscriptionStatus.Active or
                SubscriptionStatus.PastDue)
            {
                var renewalPlanVersionId =
                    subscription.PendingPlanVersionId ??
                    subscription.PlanVersionId;

                var intervalMonths =
                    await dbContext.PlanVersions
                        .Where(
                            x =>
                                x.Id ==
                                    renewalPlanVersionId)
                        .Select(
                            x =>
                                x.PlanOffer
                                    .Category
                                    .BillingIntervalMonths)
                        .SingleAsync(
                            cancellationToken);

                var periodStart =
                    reactivationAfterBoundary
                        ? approvedAt
                        : subscription.CurrentPeriodEnd;

                var periodEnd =
                    periodStart.AddMonths(
                        intervalMonths);

                await EnsureSucceededAsync(
                    billingSubscriptionService
                        .ApplyRenewalAsync(
                            subscription.MasterUserId,
                            periodStart,
                            periodEnd,
                            cancellationToken));

                if (reactivationAfterBoundary &&
                    subscription.CancelAtPeriodEnd)
                {
                    subscription.ClearCancellationRequest();

                    await dbContext.SaveChangesAsync(
                        cancellationToken);
                }
            }

            return;
        }

        if (MercadoPagoPaymentStatuses.IsFailure(
                providerPayment.Status) &&
            binding.Subscription.Status ==
                SubscriptionStatus.Active)
        {
            await EnsureSucceededAsync(
                billingSubscriptionService
                    .MarkPastDueAsync(
                        binding.Subscription.MasterUserId,
                        now,
                        cancellationToken));
        }
    }

    private async Task PromoteScheduledReplacementAsync(
        BillingProviderSubscription replacement,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool clearCancellation = true)
    {
        if (!replacement.IsScheduledReplacement ||
            !replacement.TargetPlanVersionId.HasValue)
        {
            return;
        }

        var current =
            await dbContext.BillingProviderSubscriptions
                .FirstOrDefaultAsync(
                    x =>
                        x.SubscriptionId ==
                            replacement.SubscriptionId &&
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.IsCurrent &&
                        x.Id !=
                            replacement.Id,
                    cancellationToken);

        if (current is null)
        {
            throw new InvalidOperationException(
                "Current Mercado Pago binding was not found for scheduled replacement.");
        }

        MercadoPagoPreapprovalResponse canceled;

        try
        {
            canceled =
                await mercadoPagoClient.UpdateStatusAsync(
                    current.ProviderSubscriptionId,
                    "canceled",
                    cancellationToken);
        }
        catch (MercadoPagoApiException)
        {
            var providerState =
                await mercadoPagoClient.GetPreapprovalAsync(
                    current.ProviderSubscriptionId,
                    cancellationToken);

            if (!string.Equals(
                    providerState.Status,
                    "canceled",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }

            canceled =
                providerState;
        }

        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        current.UpdateProviderState(
            string.IsNullOrWhiteSpace(
                canceled.Status)
                ? "canceled"
                : canceled.Status,
            canceled.InitPoint);

        current.MarkReplaced(
            now);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        replacement.PromoteScheduledReplacement();

        replacement.Subscription.ScheduleChange(
            replacement.TargetPlanVersionId.Value);

        if (clearCancellation &&
            replacement.Subscription.CancelAtPeriodEnd)
        {
            replacement.Subscription
                .ClearCancellationRequest();
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        logger.LogInformation(
            "Mercado Pago scheduled replacement promoted for subscription {SubscriptionId}.",
            replacement.SubscriptionId);
    }

    private async Task MarkFailedAsync(
        BillingPaymentEvent paymentEvent,
        string error,
        CancellationToken cancellationToken)
    {
        try
        {
            paymentEvent.MarkFailed(error);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to persist Mercado Pago webhook processing error for event {ProviderEventId}.",
                paymentEvent.ProviderEventId);
        }
    }

    private static async Task EnsureSucceededAsync(
        Task<BillingSubscriptionOperationResult> operation)
    {
        var result =
            await operation;

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Billing operation failed: {result.ErrorCode}");
        }
    }

    private static long ToCents(
        decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount));

        return checked(
            (long)Math.Round(
                amount * 100m,
                0,
                MidpointRounding.AwayFromZero));
    }

    private static decimal ToCurrency(
        long amountCents) =>
        amountCents / 100m;
}
