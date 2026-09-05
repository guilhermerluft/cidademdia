using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceAssignmentService(
    AppDbContext dbContext,
    R2ObjectStorage storage)
    : IOccurrenceAssignmentService
{
    private const int MaxItems = 100;

    public async Task<IReadOnlyList<MasterOccurrenceTargetItem>?> ListMasterTargetsAsync(
        Guid masterUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsActiveMasterAsync(masterUserId, cancellationToken))
            return null;

        var targets = await dbContext.OccurrenceTargets
            .AsNoTracking()
            .Include(x => x.Occurrence)
            .Where(x => x.MasterUserId == masterUserId)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        var targetIds = targets.Select(x => x.Id).ToArray();
        var occurrenceIds = targets.Select(x => x.OccurrenceId).Distinct().ToArray();
        var covers = await LoadCoverMediaAsync(occurrenceIds, cancellationToken);
        var assignments = targetIds.Length == 0
            ? []
            : await dbContext.OccurrenceTargetAssignments
                .AsNoTracking()
                .Include(x => x.MasterSubaccount)
                    .ThenInclude(x => x.SubaccountUser)
                        .ThenInclude(x => x.Profile)
                .Where(x => targetIds.Contains(x.OccurrenceTargetId))
                .ToListAsync(cancellationToken);

        var assignmentByTarget = assignments.ToDictionary(x => x.OccurrenceTargetId);

        return targets
            .Select(target => new MasterOccurrenceTargetItem(
                target.Id,
                target.OccurrenceId,
                target.Occurrence.PublicCode.Value,
                target.Occurrence.Title,
                target.Occurrence.AddressText,
                target.Occurrence.Status.Value,
                target.Status.Value,
                target.UpdatedAt,
                covers.GetValueOrDefault(target.OccurrenceId),
                assignmentByTarget.TryGetValue(target.Id, out var assignment)
                    ? ToItem(assignment, target, assignment.MasterSubaccount)
                    : null))
            .ToArray();
    }

    public async Task<IReadOnlyList<AssignedOccurrenceItem>?> ListAssignedAsync(
        Guid subaccountUserId,
        CancellationToken cancellationToken = default)
    {
        if (subaccountUserId == Guid.Empty)
            return null;

        var userIsActive = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == subaccountUserId && x.Status == UserStatus.Active, cancellationToken);

        if (!userIsActive)
            return null;

        var assignments = await dbContext.OccurrenceTargetAssignments
            .AsNoTracking()
            .Include(x => x.OccurrenceTarget)
                .ThenInclude(x => x.Occurrence)
            .Include(x => x.MasterSubaccount)
                .ThenInclude(x => x.Permissions)
                    .ThenInclude(x => x.Permission)
            .Where(x =>
                x.MasterSubaccount.SubaccountUserId == subaccountUserId
                && x.MasterSubaccount.Status == MasterSubaccountStatus.Active
                && x.OccurrenceTarget.Status == OccurrenceTargetStatus.Accepted
                && x.MasterSubaccount.Permissions.Any(permission =>
                    permission.Permission.Key == SubaccountPermissionKeys.OccurrenceReadTargeted))
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        return assignments
            .Select(assignment => new AssignedOccurrenceItem(
                assignment.Id,
                assignment.OccurrenceTargetId,
                assignment.OccurrenceTarget.OccurrenceId,
                assignment.MasterSubaccount.MasterUserId,
                assignment.MasterSubaccountId,
                assignment.OccurrenceTarget.Occurrence.PublicCode.Value,
                assignment.OccurrenceTarget.Occurrence.Title,
                assignment.OccurrenceTarget.Occurrence.AddressText,
                assignment.OccurrenceTarget.Occurrence.Status.Value,
                assignment.OccurrenceTarget.Status.Value,
                assignment.MasterSubaccount.Permissions.Any(permission =>
                    permission.Permission.Key == SubaccountPermissionKeys.OccurrenceStatusChange),
                assignment.AssignedAt,
                assignment.OccurrenceTarget.Occurrence.UpdatedAt))
            .ToArray();
    }

    public async Task<OccurrenceAssignmentResult> AssignAsync(
        Guid masterUserId,
        Guid targetId,
        Guid masterSubaccountId,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty || targetId == Guid.Empty || masterSubaccountId == Guid.Empty)
            return OccurrenceAssignmentResult.Failure("invalid_assignment");

        if (!await IsActiveMasterAsync(masterUserId, cancellationToken))
            return OccurrenceAssignmentResult.Failure("master_required");

        var target = await dbContext.OccurrenceTargets
            .Include(x => x.Occurrence)
            .FirstOrDefaultAsync(
                x => x.Id == targetId && x.MasterUserId == masterUserId,
                cancellationToken);

        if (target is null)
            return OccurrenceAssignmentResult.Failure("target_not_found");

        if (target.Status != OccurrenceTargetStatus.Accepted)
        {
            return OccurrenceAssignmentResult.Failure(
                "target_not_accepted",
                "Only an accepted target can be assigned to a subaccount.");
        }

        var link = await dbContext.MasterSubaccounts
            .Include(x => x.SubaccountUser)
                .ThenInclude(x => x.Profile)
            .FirstOrDefaultAsync(
                x => x.Id == masterSubaccountId
                    && x.MasterUserId == masterUserId
                    && x.Status == MasterSubaccountStatus.Active
                    && x.SubaccountUser.Status == UserStatus.Active,
                cancellationToken);

        if (link is null)
            return OccurrenceAssignmentResult.Failure("subaccount_link_not_found");

        var assignment = await dbContext.OccurrenceTargetAssignments
            .FirstOrDefaultAsync(x => x.OccurrenceTargetId == targetId, cancellationToken);

        if (assignment is null)
        {
            assignment = new OccurrenceTargetAssignment(
                targetId,
                link.Id,
                masterUserId,
                DateTimeOffset.UtcNow);
            dbContext.OccurrenceTargetAssignments.Add(assignment);
        }
        else if (assignment.MasterSubaccountId != link.Id)
        {
            assignment.Reassign(link.Id, masterUserId, DateTimeOffset.UtcNow);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceAssignmentResult.Failure(
                "assignment_conflict",
                "The occurrence target assignment changed concurrently.");
        }

        return OccurrenceAssignmentResult.Success(ToItem(assignment, target, link));
    }

    public async Task<OccurrenceAssignmentResult> UnassignAsync(
        Guid masterUserId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty || targetId == Guid.Empty)
            return OccurrenceAssignmentResult.Failure("invalid_assignment");

        if (!await IsActiveMasterAsync(masterUserId, cancellationToken))
            return OccurrenceAssignmentResult.Failure("master_required");

        var targetExists = await dbContext.OccurrenceTargets
            .AsNoTracking()
            .AnyAsync(x => x.Id == targetId && x.MasterUserId == masterUserId, cancellationToken);

        if (!targetExists)
            return OccurrenceAssignmentResult.Failure("target_not_found");

        var assignment = await dbContext.OccurrenceTargetAssignments
            .FirstOrDefaultAsync(x => x.OccurrenceTargetId == targetId, cancellationToken);

        if (assignment is null)
            return OccurrenceAssignmentResult.Success(null);

        dbContext.OccurrenceTargetAssignments.Remove(assignment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceAssignmentResult.Failure(
                "assignment_conflict",
                "The occurrence target assignment changed concurrently.");
        }

        return OccurrenceAssignmentResult.Success(null);
    }

    private async Task<Dictionary<Guid, PublicOccurrenceMediaItem>> LoadCoverMediaAsync(
        Guid[] occurrenceIds,
        CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured || occurrenceIds.Length == 0)
            return new Dictionary<Guid, PublicOccurrenceMediaItem>();

        var mediaRows = await dbContext.OccurrenceMedia
            .AsNoTracking()
            .Where(media => media.OccurrenceId.HasValue
                && occurrenceIds.Contains(media.OccurrenceId.Value)
                && media.Status == OccurrenceMediaStatus.Ready
                && media.ContentType.StartsWith("image/"))
            .OrderBy(media => media.CreatedAt)
            .ThenBy(media => media.Id)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, PublicOccurrenceMediaItem>();
        foreach (var media in mediaRows)
        {
            var occurrenceId = media.OccurrenceId!.Value;
            if (result.ContainsKey(occurrenceId))
                continue;

            var now = DateTimeOffset.UtcNow;
            var readUrl = storage.CreateReadUrl(media.ObjectKey, now, out var expiresAt);
            result[occurrenceId] = new PublicOccurrenceMediaItem(
                media.Id,
                media.OriginalFileName,
                media.ContentType,
                readUrl,
                expiresAt);
        }

        return result;
    }

    private Task<bool> IsActiveMasterAsync(Guid masterUserId, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == masterUserId
                    && x.Status == UserStatus.Active
                    && x.Roles.Any(role => role.Role.Key == IdentityRoleKeys.Master),
                cancellationToken);

    private static OccurrenceAssignmentItem ToItem(
        OccurrenceTargetAssignment assignment,
        OccurrenceTarget target,
        MasterSubaccount link) =>
        new(
            assignment.Id,
            target.Id,
            target.OccurrenceId,
            target.MasterUserId,
            link.Id,
            link.SubaccountUserId,
            string.IsNullOrWhiteSpace(link.SubaccountUser.Profile?.DisplayName)
                ? link.SubaccountUser.Email
                : link.SubaccountUser.Profile.DisplayName,
            assignment.AssignedAt);
}
