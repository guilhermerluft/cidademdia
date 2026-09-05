using System.Data;
using CidadeEmDia.Application.Administration;
using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Administration;

internal sealed class AdminPlanManagementService(AppDbContext dbContext)
    : IAdminPlanManagementService
{
    public async Task<AdminResult<AdminPlanVersionChange>> UpdatePlanAsync(
        Guid requesterUserId,
        Guid currentPlanVersionId,
        AdminPlanUpdateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveAdminAsync(requesterUserId, cancellationToken))
            return AdminResult<AdminPlanVersionChange>.Failure("admin_access_denied");

        if (currentPlanVersionId == Guid.Empty || command is null)
            return AdminResult<AdminPlanVersionChange>.Failure("admin_plan_request_invalid");

        if (command.PriceCents < 0
            || command.SignupFeeCents < 0
            || command.SubaccountLimit < 0
            || command.MonthlyPublicationLimit < 0)
        {
            return AdminResult<AdminPlanVersionChange>.Failure("admin_plan_values_invalid");
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 3 or > 500)
            return AdminResult<AdminPlanVersionChange>.Failure("admin_reason_required");

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var current = await dbContext.PlanVersions
            .Include(version => version.PlanOffer)
                .ThenInclude(offer => offer.Plan)
            .Include(version => version.PlanOffer)
                .ThenInclude(offer => offer.Category)
            .FirstOrDefaultAsync(version => version.Id == currentPlanVersionId, cancellationToken);

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminResult<AdminPlanVersionChange>.Failure("admin_plan_version_not_found");
        }

        var now = DateTimeOffset.UtcNow;
        if (!current.IsEffectiveAt(now)
            || !current.PlanOffer.IsActive
            || !current.PlanOffer.Plan.IsActive
            || !current.PlanOffer.Category.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminResult<AdminPlanVersionChange>.Failure("admin_plan_version_not_current");
        }

        var unchanged = current.PriceCents == command.PriceCents
            && current.SignupFeeCents == command.SignupFeeCents
            && current.SubaccountLimit == command.SubaccountLimit
            && current.MonthlyPublicationLimit == command.MonthlyPublicationLimit;

        if (unchanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminResult<AdminPlanVersionChange>.Success(
                new AdminPlanVersionChange(ToItem(current, current.PlanOffer), false));
        }

        var nextVersionNumber = await dbContext.PlanVersions
            .Where(version => version.PlanOfferId == current.PlanOfferId)
            .MaxAsync(version => version.Version, cancellationToken) + 1;

        var effectiveAt = now <= current.EffectiveFrom
            ? current.EffectiveFrom.AddTicks(1)
            : now;

        current.Close(effectiveAt);

        var next = new PlanVersion(
            current.PlanOfferId,
            nextVersionNumber,
            command.PriceCents,
            command.SignupFeeCents,
            command.SubaccountLimit,
            command.MonthlyPublicationLimit,
            effectiveAt,
            current.MarketingReferencePriceCents);

        dbContext.PlanVersions.Add(next);

        dbContext.AdminAuditLogs.Add(new AdminAuditLog(
            requesterUserId,
            "PLAN_VERSION_CHANGED",
            "PLAN_VERSION",
            next.Id,
            Describe(current),
            Describe(next),
            reason,
            effectiveAt));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AdminResult<AdminPlanVersionChange>.Failure(
                "admin_plan_persistence_conflict",
                exception.InnerException?.Message);
        }

        return AdminResult<AdminPlanVersionChange>.Success(
            new AdminPlanVersionChange(ToItem(next, current.PlanOffer), true));
    }

    private Task<bool> IsActiveAdminAsync(Guid requesterUserId, CancellationToken cancellationToken) =>
        requesterUserId != Guid.Empty
            ? dbContext.Users.AsNoTracking().AnyAsync(
                user => user.Id == requesterUserId
                    && user.Status == UserStatus.Active
                    && user.Roles.Any(link => link.Role.Key == IdentityRoleKeys.Admin),
                cancellationToken)
            : Task.FromResult(false);

    private static string Describe(PlanVersion version) =>
        $"v{version.Version};price={version.PriceCents};signup={version.SignupFeeCents};sub={version.SubaccountLimit};pub={version.MonthlyPublicationLimit}";

    private static AdminPlanItem ToItem(PlanVersion version, PlanOffer offer) => new(
        version.Id,
        offer.Plan.Key,
        offer.Plan.Name,
        offer.Key,
        offer.Category.Key,
        offer.Category.Name,
        offer.Category.BillingIntervalMonths,
        version.Version,
        version.PriceCents,
        version.SignupFeeCents,
        version.SubaccountLimit,
        version.MonthlyPublicationLimit,
        version.EffectiveFrom,
        version.EffectiveTo);
}
