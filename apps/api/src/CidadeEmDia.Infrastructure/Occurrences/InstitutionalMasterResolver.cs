using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Institutions;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed record InstitutionalMasterDestination(
    Guid InstitutionId,
    Guid MasterUserId,
    string DisplayName,
    string Type,
    string ScopeLevel,
    Guid? CityId,
    string? StateCode);

internal static class InstitutionalMasterResolver
{
    public static async Task<IReadOnlyList<InstitutionalMasterDestination>> GetEligibleAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var candidates = await dbContext.InstitutionMemberships
            .AsNoTracking()
            .Where(link =>
                link.Status == InstitutionMembershipStatusKeys.Active
                && link.MembershipRole == InstitutionMembershipRoleKeys.InstitutionAdmin
                && link.Institution.Status == InstitutionStatusKeys.Active
                && dbContext.Users.Any(user =>
                    user.Id == link.UserId
                    && user.Status == UserStatus.Active
                    && user.Roles.Any(role => role.Role.Key == IdentityRoleKeys.Master)))
            .Select(link => new InstitutionalMasterDestination(
                link.InstitutionId,
                link.UserId,
                link.Institution.Name,
                link.Institution.Type,
                link.Institution.ScopeLevel,
                link.Institution.CityId,
                link.Institution.StateCode))
            .ToListAsync(cancellationToken);

        var mastersPerInstitution = candidates
            .GroupBy(item => item.InstitutionId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.MasterUserId).Distinct().Count());

        var institutionsPerMaster = candidates
            .GroupBy(item => item.MasterUserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.InstitutionId).Distinct().Count());

        return candidates
            .Where(item =>
                mastersPerInstitution[item.InstitutionId] == 1
                && institutionsPerMaster[item.MasterUserId] == 1)
            .GroupBy(item => new { item.InstitutionId, item.MasterUserId })
            .Select(group => group.First())
            .ToArray();
    }

    public static async Task<InstitutionalMasterDestination?> ResolveAsync(
        AppDbContext dbContext,
        Guid institutionId,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty)
            return null;

        var eligible = await GetEligibleAsync(dbContext, cancellationToken);
        return eligible.SingleOrDefault(item => item.InstitutionId == institutionId);
    }
}
