using CidadeEmDia.Application.Billing;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Billing.MercadoPago;

internal sealed class MercadoPagoCheckoutService(
    AppDbContext dbContext,
    IMercadoPagoClient mercadoPagoClient,
    MercadoPagoOptions options,
    ILogger<MercadoPagoCheckoutService> logger)
    : IBillingCheckoutService
{
    public async Task<BillingCheckoutResult> CreateCheckoutAsync(
        Guid masterUserId,
        Guid planVersionId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty ||
            planVersionId == Guid.Empty)
        {
            return BillingCheckoutResult.Failure(
                "invalid_input");
        }

        if (!options.HasAccessToken ||
            !options.HasBackUrl)
        {
            return BillingCheckoutResult.Failure(
                "provider_not_configured");
        }

        var now = at ?? DateTimeOffset.UtcNow;

        var master = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == masterUserId,
                cancellationToken);

        if (master is null)
        {
            return BillingCheckoutResult.Failure(
                "master_not_found");
        }

        var existingSubscription = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(x =>
                x.MasterUserId == masterUserId &&
                x.Status != SubscriptionStatus.Canceled)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSubscription is not null)
        {
            if (existingSubscription.Status ==
                SubscriptionStatus.Pending)
            {
                var existingProvider =
                    await dbContext.BillingProviderSubscriptions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.IsCurrent &&
                                x.SubscriptionId ==
                                    existingSubscription.Id &&
                                x.Provider ==
                                    BillingProviders.MercadoPago,
                            cancellationToken);

                if (existingProvider is not null)
                {
                    return BillingCheckoutResult.Success(
                        existingSubscription.Id,
                        existingSubscription.PlanVersionId,
                        existingProvider.CheckoutUrl,
                        existingProvider.ProviderStatus);
                }
            }

            return BillingCheckoutResult.Failure(
                "subscription_already_exists");
        }

        var version = await dbContext.PlanVersions
            .AsNoTracking()
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Plan)
            .Include(x => x.PlanOffer)
                .ThenInclude(x => x.Category)
            .SingleOrDefaultAsync(
                x => x.Id == planVersionId,
                cancellationToken);

        if (version is null)
        {
            return BillingCheckoutResult.Failure(
                "plan_version_not_found");
        }

        var offer = version.PlanOffer;
        var category = offer.Category;

        var versionIsEffective =
            offer.IsActive &&
            offer.Plan.IsActive &&
            category.IsActive &&
            version.EffectiveFrom <= now &&
            (
                version.EffectiveTo == null ||
                now < version.EffectiveTo
            );

        if (!versionIsEffective)
        {
            return BillingCheckoutResult.Failure(
                "plan_version_not_current");
        }

        var newerEffectiveVersionExists =
            await dbContext.PlanVersions
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.PlanOfferId == version.PlanOfferId &&
                        x.Version > version.Version &&
                        x.EffectiveFrom <= now &&
                        (
                            x.EffectiveTo == null ||
                            now < x.EffectiveTo
                        ),
                    cancellationToken);

        if (newerEffectiveVersionExists)
        {
            return BillingCheckoutResult.Failure(
                "plan_version_not_current");
        }

        var customer = await dbContext.BillingCustomers
            .FirstOrDefaultAsync(
                x => x.MasterUserId == masterUserId,
                cancellationToken);

        if (customer is null)
        {
            customer = new BillingCustomer(masterUserId);
            dbContext.BillingCustomers.Add(customer);
        }

        var amounts = MercadoPagoCheckoutAmounts.Calculate(
            version.PriceCents,
            version.SignupFeeCents,
            customer.HasPaidSignupFee);

        var subscription = new Subscription(
            masterUserId,
            version.Id,
            now,
            now.AddMonths(
                category.BillingIntervalMonths));

        dbContext.Subscriptions.Add(subscription);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to persist pending billing subscription for master {MasterUserId}.",
                masterUserId);

            return BillingCheckoutResult.Failure(
                "billing_persistence_failed");
        }

        var externalReference =
            MercadoPagoExternalReference.Create(
                subscription.Id);

        MercadoPagoPreapprovalResponse providerResponse;

        try
        {
            providerResponse =
                await mercadoPagoClient.CreatePreapprovalAsync(
                    new MercadoPagoCreatePreapprovalRequest(
                        Reason:
                            $"CidadeEmDia - {offer.Plan.Name} - {category.Name}",
                        ExternalReference:
                            externalReference,
                        PayerEmail:
                            master.Email,
                        Frequency:
                            category.BillingIntervalMonths,
                        FrequencyType:
                            "months",
                        TransactionAmount:
                            ToCurrency(
                                amounts.InitialAmountCents),
                        CurrencyId:
                            "BRL",
                        BackUrl:
                            options.BackUrl),
                    cancellationToken);
        }
        catch (MercadoPagoConfigurationException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago is not configured.");

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "provider_not_configured");
        }
        catch (MercadoPagoApiException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago rejected checkout for subscription {SubscriptionId}. Status={StatusCode}.",
                subscription.Id,
                exception.StatusCode);

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "provider_rejected");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago unavailable while creating checkout for subscription {SubscriptionId}.",
                subscription.Id);

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "provider_unavailable");
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Mercado Pago timed out while creating checkout for subscription {SubscriptionId}.",
                subscription.Id);

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "provider_unavailable");
        }

        if (string.IsNullOrWhiteSpace(
                providerResponse.Id) ||
            string.IsNullOrWhiteSpace(
                providerResponse.InitPoint) ||
            !Uri.TryCreate(
                providerResponse.InitPoint,
                UriKind.Absolute,
                out _) ||
            (
                !string.IsNullOrWhiteSpace(
                    providerResponse.ExternalReference) &&
                !string.Equals(
                    providerResponse.ExternalReference,
                    externalReference,
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            logger.LogWarning(
                "Mercado Pago returned an invalid checkout response for subscription {SubscriptionId}. ProviderId={ProviderId}.",
                subscription.Id,
                providerResponse.Id);

            if (!string.IsNullOrWhiteSpace(
                    providerResponse.Id))
            {
                await TryCancelProviderAsync(
                    providerResponse.Id,
                    cancellationToken);
            }

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "provider_invalid_response");
        }

        var providerSubscription =
            new BillingProviderSubscription(
                subscription.Id,
                BillingProviders.MercadoPago,
                providerResponse.Id,
                externalReference,
                providerResponse.InitPoint,
                string.IsNullOrWhiteSpace(
                    providerResponse.Status)
                    ? "pending"
                    : providerResponse.Status,
                amounts.RecurringAmountCents,
                amounts.InitialAmountCents,
                amounts.SignupFeeIncluded);

        dbContext.BillingProviderSubscriptions.Add(
            providerSubscription);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to persist Mercado Pago binding for subscription {SubscriptionId}.",
                subscription.Id);

            dbContext.Entry(providerSubscription)
                .State = EntityState.Detached;

            await TryCancelProviderAsync(
                providerResponse.Id,
                cancellationToken);

            await RemovePendingSubscriptionAsync(
                subscription,
                cancellationToken);

            return BillingCheckoutResult.Failure(
                "billing_persistence_failed");
        }

        return BillingCheckoutResult.Success(
            subscription.Id,
            version.Id,
            providerResponse.InitPoint,
            providerSubscription.ProviderStatus);
    }

    private async Task RemovePendingSubscriptionAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            if (dbContext.Entry(subscription).State ==
                EntityState.Detached)
            {
                subscription =
                    await dbContext.Subscriptions
                        .SingleOrDefaultAsync(
                            x => x.Id == subscription.Id,
                            cancellationToken)
                    ?? subscription;
            }

            if (subscription.Status !=
                SubscriptionStatus.Pending)
            {
                return;
            }

            dbContext.Subscriptions.Remove(
                subscription);

            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to clean pending subscription {SubscriptionId} after checkout failure.",
                subscription.Id);
        }
    }

    private async Task TryCancelProviderAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
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
                "Failed to compensate Mercado Pago subscription {ProviderSubscriptionId}.",
                providerSubscriptionId);
        }
    }

    private static decimal ToCurrency(
        long amountCents) =>
        amountCents / 100m;
}
