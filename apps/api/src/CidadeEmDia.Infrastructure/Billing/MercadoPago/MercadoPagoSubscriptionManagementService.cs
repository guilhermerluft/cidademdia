using CidadeEmDia.Application.Billing;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

internal sealed class MercadoPagoSubscriptionManagementService(
    AppDbContext dbContext,
    IMercadoPagoClient mercadoPagoClient,
    MercadoPagoOptions options,
    ILogger<MercadoPagoSubscriptionManagementService> logger)
    : IBillingSubscriptionManagementService
{
    public async Task<BillingSubscriptionManagementResult>
        SchedulePlanChangeAsync(
            Guid masterUserId,
            Guid targetPlanVersionId,
            DateTimeOffset? at = null,
            CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty ||
            targetPlanVersionId == Guid.Empty)
        {
            return BillingSubscriptionManagementResult.Failure(
                "invalid_input");
        }

        if (!options.HasAccessToken ||
            !options.HasBackUrl)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_not_configured");
        }

        var now =
            at ?? DateTimeOffset.UtcNow;

        var subscription =
            await FindSubscriptionAsync(
                masterUserId,
                cancellationToken);

        if (subscription is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_found");
        }

        if (subscription.Status !=
            SubscriptionStatus.Active)
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_active");
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return BillingSubscriptionManagementResult.Failure(
                "cancellation_pending");
        }

        var currentBinding =
            await dbContext.BillingProviderSubscriptions
                .FirstOrDefaultAsync(
                    x =>
                        x.SubscriptionId ==
                            subscription.Id &&
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.IsCurrent,
                    cancellationToken);

        if (currentBinding is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_subscription_not_found");
        }

        var target =
            await LoadPlanVersionAsync(
                targetPlanVersionId,
                cancellationToken);

        if (target is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_version_not_found");
        }

        if (!await IsCurrentVersionAsync(
                target,
                now,
                cancellationToken))
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_version_not_current");
        }

        if (target.Id ==
                subscription.PlanVersionId &&
            !subscription.PendingPlanVersionId.HasValue)
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_already_active");
        }

        var scheduled =
            await FindScheduledReplacementAsync(
                subscription.Id,
                cancellationToken);

        if (scheduled is not null)
        {
            if (scheduled.TargetPlanVersionId ==
                target.Id)
            {
                return BillingSubscriptionManagementResult.Success(
                    target.Id,
                    requiresAuthorization:
                        true,
                    checkoutUrl:
                        scheduled.CheckoutUrl,
                    effectiveAt:
                        scheduled.ScheduledFor,
                    providerStatus:
                        scheduled.ProviderStatus);
            }

            return BillingSubscriptionManagementResult.Failure(
                "plan_change_authorization_pending");
        }

        var currentIntervalMonths =
            await dbContext.PlanVersions
                .Where(
                    x =>
                        x.Id ==
                            subscription.PlanVersionId)
                .Select(
                    x =>
                        x.PlanOffer
                            .Category
                            .BillingIntervalMonths)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (currentIntervalMonths <= 0)
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_version_not_found");
        }

        var targetIntervalMonths =
            target.PlanOffer
                .Category
                .BillingIntervalMonths;

        if (currentIntervalMonths ==
            targetIntervalMonths)
        {
            return await ScheduleSameIntervalChangeAsync(
                subscription,
                currentBinding,
                target,
                cancellationToken);
        }

        return await CreateScheduledReplacementAsync(
            subscription,
            target,
            subscription.CurrentPeriodEnd,
            cancellationToken);
    }

    public async Task<BillingSubscriptionManagementResult>
        ReactivateAsync(
            Guid masterUserId,
            DateTimeOffset? at = null,
            CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty)
        {
            return BillingSubscriptionManagementResult.Failure(
                "invalid_input");
        }

        if (!options.HasAccessToken ||
            !options.HasBackUrl)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_not_configured");
        }

        var now =
            at ?? DateTimeOffset.UtcNow;

        var subscription =
            await FindSubscriptionAsync(
                masterUserId,
                cancellationToken);

        if (subscription is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_found");
        }

        if (subscription.Status is not
            (SubscriptionStatus.Active or
             SubscriptionStatus.PastDue))
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_active");
        }

        if (!subscription.CancelAtPeriodEnd)
        {
            return BillingSubscriptionManagementResult.Failure(
                "reactivation_not_required");
        }

        var currentBinding =
            await dbContext.BillingProviderSubscriptions
                .FirstOrDefaultAsync(
                    x =>
                        x.SubscriptionId ==
                            subscription.Id &&
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.IsCurrent,
                    cancellationToken);

        if (currentBinding is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_subscription_not_found");
        }

        var contractedVersion =
            await LoadPlanVersionAsync(
                subscription.PlanVersionId,
                cancellationToken);

        if (contractedVersion is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_version_not_found");
        }

        var target =
            await LoadCurrentVersionForOfferAsync(
                contractedVersion.PlanOfferId,
                now,
                cancellationToken);

        if (target is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "plan_version_not_current");
        }

        var scheduled =
            await FindScheduledReplacementAsync(
                subscription.Id,
                cancellationToken);

        if (scheduled is not null)
        {
            if (scheduled.TargetPlanVersionId ==
                target.Id)
            {
                return BillingSubscriptionManagementResult.Success(
                    target.Id,
                    requiresAuthorization:
                        true,
                    checkoutUrl:
                        scheduled.CheckoutUrl,
                    effectiveAt:
                        scheduled.ScheduledFor,
                    providerStatus:
                        scheduled.ProviderStatus);
            }

            return BillingSubscriptionManagementResult.Failure(
                "reactivation_authorization_pending");
        }

        var scheduledFor =
            now < subscription.CurrentPeriodEnd
                ? subscription.CurrentPeriodEnd
                : now;

        return await CreateScheduledReplacementAsync(
            subscription,
            target,
            scheduledFor,
            cancellationToken);
    }

    public async Task<BillingSubscriptionManagementResult>
        RequestCancellationAsync(
            Guid masterUserId,
            DateTimeOffset? at = null,
            CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty)
        {
            return BillingSubscriptionManagementResult.Failure(
                "invalid_input");
        }

        if (!options.HasAccessToken)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_not_configured");
        }

        var now =
            at ?? DateTimeOffset.UtcNow;

        var subscription =
            await FindSubscriptionAsync(
                masterUserId,
                cancellationToken);

        if (subscription is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_found");
        }

        var scheduled =
            await FindScheduledReplacementAsync(
                subscription.Id,
                cancellationToken);

        if (scheduled is not null)
        {
            var scheduledCancel =
                await CallProviderAsync(
                    () =>
                        mercadoPagoClient.UpdateStatusAsync(
                            scheduled.ProviderSubscriptionId,
                            "canceled",
                            cancellationToken),
                    "cancel scheduled replacement",
                    cancellationToken);

            if (!scheduledCancel.Succeeded)
            {
                return BillingSubscriptionManagementResult.Failure(
                    scheduledCancel.ErrorCode!);
            }

            scheduled.UpdateProviderState(
                ProviderStatusOrCurrent(
                    scheduledCancel.Response,
                    scheduled.ProviderStatus));

            scheduled.AbandonScheduledReplacement(
                now);

            try
            {
                await dbContext.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist abandoned Mercado Pago replacement for subscription {SubscriptionId}.",
                    subscription.Id);

                return BillingSubscriptionManagementResult.Failure(
                    "billing_persistence_failed");
            }
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return BillingSubscriptionManagementResult.Success(
                subscription.PlanVersionId,
                effectiveAt:
                    subscription.CurrentPeriodEnd);
        }

        var currentBinding =
            await dbContext.BillingProviderSubscriptions
                .FirstOrDefaultAsync(
                    x =>
                        x.SubscriptionId ==
                            subscription.Id &&
                        x.Provider ==
                            BillingProviders.MercadoPago &&
                        x.IsCurrent,
                    cancellationToken);

        if (currentBinding is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "provider_subscription_not_found");
        }

        var providerCancel =
            await CallProviderAsync(
                () =>
                    mercadoPagoClient.UpdateStatusAsync(
                        currentBinding.ProviderSubscriptionId,
                        "canceled",
                        cancellationToken),
                "cancel current subscription",
                cancellationToken);

        if (!providerCancel.Succeeded)
        {
            return BillingSubscriptionManagementResult.Failure(
                providerCancel.ErrorCode!);
        }

        currentBinding.UpdateProviderState(
            ProviderStatusOrCurrent(
                providerCancel.Response,
                "canceled"));

        if (subscription.Status ==
            SubscriptionStatus.Pending)
        {
            subscription.Cancel(
                now);
        }
        else if (subscription.Status is
            SubscriptionStatus.Active or
            SubscriptionStatus.PastDue)
        {
            subscription.RequestCancellation();
        }
        else
        {
            return BillingSubscriptionManagementResult.Failure(
                "subscription_not_active");
        }

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to persist Mercado Pago cancellation for subscription {SubscriptionId}.",
                subscription.Id);

            return BillingSubscriptionManagementResult.Failure(
                "billing_persistence_failed");
        }

        return BillingSubscriptionManagementResult.Success(
            subscription.PlanVersionId,
            effectiveAt:
                subscription.Status ==
                    SubscriptionStatus.Canceled
                    ? now
                    : subscription.CurrentPeriodEnd,
            providerStatus:
                currentBinding.ProviderStatus);
    }

    private async Task<BillingSubscriptionManagementResult>
        ScheduleSameIntervalChangeAsync(
            Subscription subscription,
            BillingProviderSubscription currentBinding,
            PlanVersion target,
            CancellationToken cancellationToken)
    {
        var previousAmount =
            currentBinding.RecurringAmountCents;

        var providerUpdate =
            await CallProviderAsync(
                () =>
                    mercadoPagoClient.UpdateRecurringAmountAsync(
                        currentBinding.ProviderSubscriptionId,
                        ToCurrency(
                            target.PriceCents),
                        cancellationToken),
                "update recurring amount",
                cancellationToken);

        if (!providerUpdate.Succeeded)
        {
            return BillingSubscriptionManagementResult.Failure(
                providerUpdate.ErrorCode!);
        }

        currentBinding.UpdateRecurringAmount(
            target.PriceCents,
            ProviderStatusOrCurrent(
                providerUpdate.Response,
                currentBinding.ProviderStatus));

        subscription.ScheduleChange(
            target.Id);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to persist same-interval plan change for subscription {SubscriptionId}.",
                subscription.Id);

            await TryRestoreRecurringAmountAsync(
                currentBinding.ProviderSubscriptionId,
                previousAmount,
                cancellationToken);

            return BillingSubscriptionManagementResult.Failure(
                "billing_persistence_failed");
        }

        return BillingSubscriptionManagementResult.Success(
            target.Id,
            requiresAuthorization:
                false,
            effectiveAt:
                subscription.CurrentPeriodEnd,
            providerStatus:
                currentBinding.ProviderStatus);
    }

    private async Task<BillingSubscriptionManagementResult>
        CreateScheduledReplacementAsync(
            Subscription subscription,
            PlanVersion target,
            DateTimeOffset scheduledFor,
            CancellationToken cancellationToken)
    {
        var master =
            await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            subscription.MasterUserId,
                    cancellationToken);

        if (master is null)
        {
            return BillingSubscriptionManagementResult.Failure(
                "master_not_found");
        }

        var externalReference =
            MercadoPagoExternalReference.Create(
                subscription.Id);

        var providerCreate =
            await CallProviderAsync(
                () =>
                    mercadoPagoClient.CreatePreapprovalAsync(
                        new MercadoPagoCreatePreapprovalRequest(
                            Reason:
                                $"CidadeEmDia - {target.PlanOffer.Plan.Name} - {target.PlanOffer.Category.Name}",
                            ExternalReference:
                                externalReference,
                            PayerEmail:
                                master.Email,
                            Frequency:
                                target.PlanOffer
                                    .Category
                                    .BillingIntervalMonths,
                            FrequencyType:
                                "months",
                            TransactionAmount:
                                ToCurrency(
                                    target.PriceCents),
                            CurrencyId:
                                "BRL",
                            BackUrl:
                                options.BackUrl,
                            StartDate:
                                scheduledFor),
                        cancellationToken),
                "create scheduled replacement",
                cancellationToken);

        if (!providerCreate.Succeeded)
        {
            return BillingSubscriptionManagementResult.Failure(
                providerCreate.ErrorCode!);
        }

        var provider =
            providerCreate.Response!;

        if (string.IsNullOrWhiteSpace(
                provider.Id) ||
            string.IsNullOrWhiteSpace(
                provider.InitPoint) ||
            !Uri.TryCreate(
                provider.InitPoint,
                UriKind.Absolute,
                out _) ||
            (
                !string.IsNullOrWhiteSpace(
                    provider.ExternalReference) &&
                !string.Equals(
                    provider.ExternalReference,
                    externalReference,
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            await TryCancelProviderAsync(
                provider.Id,
                cancellationToken);

            return BillingSubscriptionManagementResult.Failure(
                "provider_invalid_response");
        }

        var replacement =
            new BillingProviderSubscription(
                subscription.Id,
                BillingProviders.MercadoPago,
                provider.Id,
                externalReference,
                provider.InitPoint,
                string.IsNullOrWhiteSpace(
                    provider.Status)
                    ? "pending"
                    : provider.Status,
                target.PriceCents,
                target.PriceCents,
                signupFeeIncluded:
                    false);

        replacement.MarkScheduledReplacement(
            target.Id,
            scheduledFor);

        dbContext.BillingProviderSubscriptions.Add(
            replacement);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to persist scheduled replacement for subscription {SubscriptionId}.",
                subscription.Id);

            await TryCancelProviderAsync(
                provider.Id,
                cancellationToken);

            return BillingSubscriptionManagementResult.Failure(
                "billing_persistence_failed");
        }

        return BillingSubscriptionManagementResult.Success(
            target.Id,
            requiresAuthorization:
                true,
            checkoutUrl:
                provider.InitPoint,
            effectiveAt:
                scheduledFor,
            providerStatus:
                replacement.ProviderStatus);
    }

    private async Task<Subscription?>
        FindSubscriptionAsync(
            Guid masterUserId,
            CancellationToken cancellationToken) =>
        await dbContext.Subscriptions
            .Where(
                x =>
                    x.MasterUserId ==
                        masterUserId &&
                    x.Status !=
                        SubscriptionStatus.Canceled)
            .OrderByDescending(
                x => x.StartedAt)
            .FirstOrDefaultAsync(
                cancellationToken);

    private async Task<BillingProviderSubscription?>
        FindScheduledReplacementAsync(
            Guid subscriptionId,
            CancellationToken cancellationToken) =>
        await dbContext.BillingProviderSubscriptions
            .FirstOrDefaultAsync(
                x =>
                    x.SubscriptionId ==
                        subscriptionId &&
                    x.Provider ==
                        BillingProviders.MercadoPago &&
                    !x.IsCurrent &&
                    x.EndedAt == null &&
                    x.TargetPlanVersionId != null &&
                    x.ScheduledFor != null,
                cancellationToken);

    private async Task<PlanVersion?>
        LoadPlanVersionAsync(
            Guid planVersionId,
            CancellationToken cancellationToken) =>
        await dbContext.PlanVersions
            .AsNoTracking()
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Plan)
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.Id == planVersionId,
                cancellationToken);

    private async Task<PlanVersion?>
        LoadCurrentVersionForOfferAsync(
            Guid planOfferId,
            DateTimeOffset at,
            CancellationToken cancellationToken) =>
        await dbContext.PlanVersions
            .AsNoTracking()
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Plan)
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Category)
            .Where(
                x =>
                    x.PlanOfferId ==
                        planOfferId &&
                    x.PlanOffer.IsActive &&
                    x.PlanOffer.Plan.IsActive &&
                    x.PlanOffer.Category.IsActive &&
                    x.EffectiveFrom <= at &&
                    (
                        x.EffectiveTo == null ||
                        at < x.EffectiveTo
                    ))
            .OrderByDescending(
                x => x.Version)
            .FirstOrDefaultAsync(
                cancellationToken);

    private async Task<bool>
        IsCurrentVersionAsync(
            PlanVersion version,
            DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var offer =
            version.PlanOffer;

        var effective =
            offer.IsActive &&
            offer.Plan.IsActive &&
            offer.Category.IsActive &&
            version.EffectiveFrom <= now &&
            (
                version.EffectiveTo == null ||
                now < version.EffectiveTo
            );

        if (!effective)
        {
            return false;
        }

        return !await dbContext.PlanVersions
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.PlanOfferId ==
                        version.PlanOfferId &&
                    x.Version >
                        version.Version &&
                    x.EffectiveFrom <= now &&
                    (
                        x.EffectiveTo == null ||
                        now < x.EffectiveTo
                    ),
                cancellationToken);
    }

    private async Task<ProviderCallResult>
        CallProviderAsync(
            Func<Task<MercadoPagoPreapprovalResponse>> action,
            string operation,
            CancellationToken cancellationToken)
    {
        try
        {
            return ProviderCallResult.Success(
                await action());
        }
        catch (MercadoPagoConfigurationException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago not configured during {Operation}.",
                operation);

            return ProviderCallResult.Failure(
                "provider_not_configured");
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago rejected {Operation}. Status={StatusCode}.",
                operation,
                exception.StatusCode);

            return ProviderCallResult.Failure(
                "provider_rejected");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago unavailable during {Operation}.",
                operation);

            return ProviderCallResult.Failure(
                "provider_unavailable");
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago timeout during {Operation}.",
                operation);

            return ProviderCallResult.Failure(
                "provider_unavailable");
        }
    }

    private async Task TryCancelProviderAsync(
        string? providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                providerSubscriptionId))
        {
            return;
        }

        try
        {
            await mercadoPagoClient.UpdateStatusAsync(
                providerSubscriptionId,
                "canceled",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to compensate Mercado Pago preapproval {ProviderSubscriptionId}.",
                providerSubscriptionId);
        }
    }

    private async Task TryRestoreRecurringAmountAsync(
        string providerSubscriptionId,
        long amountCents,
        CancellationToken cancellationToken)
    {
        try
        {
            await mercadoPagoClient.UpdateRecurringAmountAsync(
                providerSubscriptionId,
                ToCurrency(
                    amountCents),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to compensate recurring amount for Mercado Pago preapproval {ProviderSubscriptionId}.",
                providerSubscriptionId);
        }
    }

    private static string ProviderStatusOrCurrent(
        MercadoPagoPreapprovalResponse? response,
        string currentStatus) =>
        string.IsNullOrWhiteSpace(
            response?.Status)
            ? currentStatus
            : response.Status;

    private static decimal ToCurrency(
        long amountCents) =>
        amountCents / 100m;

    private sealed record ProviderCallResult(
        bool Succeeded,
        MercadoPagoPreapprovalResponse? Response,
        string? ErrorCode)
    {
        public static ProviderCallResult Success(
            MercadoPagoPreapprovalResponse response) =>
            new(true, response, null);

        public static ProviderCallResult Failure(
            string errorCode) =>
            new(false, null, errorCode);
    }
}
