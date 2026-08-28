using CidadeEmDia.Application.Subaccounts;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class SubaccountAccessStateService(AppDbContext dbContext) : ISubaccountAccessStateService
{
    public async Task<IReadOnlyCollection<SubaccountContext>> ListActiveContextsAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MasterSubaccounts
            .AsNoTracking()
            .Where(x =>
                x.SubaccountUserId == subaccountUserId &&
                x.Status == MasterSubaccountStatus.Active)
            .OrderBy(x => x.MasterUserId)
            .Select(x => new SubaccountContext(
                x.Id,
                x.MasterUserId,
                x.Permissions
                    .Select(permission => permission.Permission.Key)
                    .OrderBy(key => key)
                    .ToArray()))
            .ToArrayAsync(cancellationToken);
    }

    public async Task RemoveGlobalRoleIfNoActiveLinksAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default)
    {
        var hasActiveLink = await dbContext.MasterSubaccounts
            .AnyAsync(
                x => x.SubaccountUserId == subaccountUserId && x.Status == MasterSubaccountStatus.Active,
                cancellationToken);

        if (hasActiveLink)
            return;

        var subaccountRoleId = await dbContext.Roles
            .Where(x => x.Key == IdentityRoleKeys.Subaccount)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (subaccountRoleId is null)
            return;

        var roleLink = await dbContext.UserRoles
            .FirstOrDefaultAsync(
                x => x.UserId == subaccountUserId && x.RoleId == subaccountRoleId.Value,
                cancellationToken);

        if (roleLink is null)
            return;

        dbContext.UserRoles.Remove(roleLink);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
