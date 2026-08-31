using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CidadeEmDia.Infrastructure.Billing;

internal sealed class BillingCatalogSeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<BillingCatalogSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plans = (await dbContext.Plans.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var definition in BillingSeedCatalog.Plans)
        {
            if (plans.ContainsKey(definition.Key))
                continue;

            var plan = new Plan(definition.Key, definition.Name);
            dbContext.Plans.Add(plan);
            plans.Add(plan.Key, plan);
        }

        var categories = (await dbContext.PlanCategories.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var definition in BillingSeedCatalog.Categories)
        {
            if (categories.ContainsKey(definition.Key))
                continue;

            var category = new PlanCategory(definition.Key, definition.Name, definition.BillingIntervalMonths);
            dbContext.PlanCategories.Add(category);
            categories.Add(category.Key, category);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var offers = (await dbContext.PlanOffers.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var definition in BillingSeedCatalog.Offers)
        {
            var offerKey = BillingSeedCatalog.OfferKey(definition.PlanKey, definition.CategoryKey);
            if (!offers.TryGetValue(offerKey, out var offer))
            {
                offer = new PlanOffer(
                    plans[definition.PlanKey].Id,
                    categories[definition.CategoryKey].Id,
                    offerKey);
                dbContext.PlanOffers.Add(offer);
                offers.Add(offer.Key, offer);
            }

            var hasVersionOne = await dbContext.PlanVersions
                .AnyAsync(x => x.PlanOfferId == offer.Id && x.Version == 1, cancellationToken);
            if (hasVersionOne)
                continue;

            dbContext.PlanVersions.Add(new PlanVersion(
                offer.Id,
                1,
                definition.PriceCents,
                definition.SignupFeeCents,
                definition.SubaccountLimit,
                definition.MonthlyPublicationLimit,
                BillingSeedCatalog.EffectiveFrom,
                definition.MarketingReferencePriceCents));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing catalog seed ensured with {PlanCount} plans, {CategoryCount} categories and {OfferCount} offers.",
            BillingSeedCatalog.Plans.Count,
            BillingSeedCatalog.Categories.Count,
            BillingSeedCatalog.Offers.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
