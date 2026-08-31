using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceLifecycleService(AppDbContext dbContext)
    : IOccurrenceLifecycleService
{
    public async Task<OccurrenceLifecycleResult> ChangeStatusAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        string targetStatus,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceLifecycleResult.Failure("invalid_status_change");

        OccurrenceStatus parsedStatus;
        try
        {
            parsedStatus = OccurrenceStatus.From(targetStatus);
        }
        catch (DomainException exception)
        {
            return OccurrenceLifecycleResult.Failure("invalid_status", exception.Message);
        }

        if (parsedStatus == OccurrenceStatus.New
            || parsedStatus == OccurrenceStatus.Received
            || parsedStatus == OccurrenceStatus.Cancelled)
        {
            return OccurrenceLifecycleResult.Failure(
                "status_managed_by_system",
                "NOVA, RECEBIDA and CANCELADA are managed by occurrence creation, target acceptance and author cancellation.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Targets)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == occurrenceId, cancellationToken);

        if (occurrence is null)
            return OccurrenceLifecycleResult.Failure("occurrence_not_found");

        if (parsedStatus == OccurrenceStatus.Closed)
        {
            if (!await IsActiveRoleAsync(requesterUserId, IdentityRoleKeys.Admin, cancellationToken))
            {
                return OccurrenceLifecycleResult.Failure(
                    "admin_required",
                    "Only a CIDADEMDIA administrator can close an occurrence.");
            }
        }
        else
        {
            var requesterIsAcceptedMaster = occurrence.Targets.Any(target =>
                target.MasterUserId == requesterUserId
                && target.Status == OccurrenceTargetStatus.Accepted);

            if (!requesterIsAcceptedMaster
                || !await IsActiveRoleAsync(requesterUserId, IdentityRoleKeys.Master, cancellationToken))
            {
                return OccurrenceLifecycleResult.Failure(
                    "accepted_master_required",
                    "Only an active Master with an accepted target can update the occurrence status.");
            }
        }

        var historyCountBefore = occurrence.StatusHistory.Count;
        var changedAt = DateTimeOffset.UtcNow;

        try
        {
            occurrence.TransitionTo(
                parsedStatus,
                requesterUserId,
                changedAt,
                reason);
        }
        catch (DomainException exception)
        {
            return OccurrenceLifecycleResult.Failure("status_transition_not_allowed", exception.Message);
        }

        MarkNewHistoryAsAdded(occurrence, historyCountBefore);

        if (parsedStatus == OccurrenceStatus.Closed)
        {
            var conversations = await dbContext.ChatConversations
                .Where(x =>
                    x.OccurrenceId == occurrenceId
                    && x.Status == ChatConversationStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var conversation in conversations)
                conversation.Close(changedAt);
        }

        var persistenceFailure = await SaveAsync(transaction, cancellationToken);
        if (persistenceFailure is not null)
            return persistenceFailure;

        return OccurrenceLifecycleResult.Success(ToItem(occurrence));
    }

    public async Task<OccurrenceLifecycleResult> CancelAsync(
        Guid authorUserId,
        Guid occurrenceId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceLifecycleResult.Failure("invalid_cancellation");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Targets)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(
                x => x.Id == occurrenceId && x.AuthorUserId == authorUserId,
                cancellationToken);

        if (occurrence is null)
            return OccurrenceLifecycleResult.Failure("occurrence_not_found");

        if (occurrence.Targets.Count > 0)
        {
            return OccurrenceLifecycleResult.Failure(
                "occurrence_already_assigned",
                "The occurrence can no longer be cancelled because it has already been assigned to a Master.");
        }

        var historyCountBefore = occurrence.StatusHistory.Count;

        try
        {
            occurrence.CancelByAuthor(authorUserId, DateTimeOffset.UtcNow, reason);
        }
        catch (DomainException exception)
        {
            return OccurrenceLifecycleResult.Failure("cancellation_not_allowed", exception.Message);
        }

        MarkNewHistoryAsAdded(occurrence, historyCountBefore);

        var persistenceFailure = await SaveAsync(transaction, cancellationToken);
        if (persistenceFailure is not null)
            return persistenceFailure;

        return OccurrenceLifecycleResult.Success(ToItem(occurrence));
    }

    public async Task<OccurrenceDeleteResult> DeleteAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceDeleteResult.Failure("invalid_delete");

        if (!await IsActiveRoleAsync(requesterUserId, IdentityRoleKeys.Admin, cancellationToken))
        {
            return OccurrenceDeleteResult.Failure(
                "admin_required",
                "Only a CIDADEMDIA administrator can permanently delete an occurrence.");
        }

        var occurrence = await dbContext.Occurrences
            .FirstOrDefaultAsync(x => x.Id == occurrenceId, cancellationToken);

        if (occurrence is null)
            return OccurrenceDeleteResult.Failure("occurrence_not_found");

        dbContext.Occurrences.Remove(occurrence);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceDeleteResult.Failure(
                "occurrence_delete_conflict",
                "The occurrence could not be permanently deleted because related data changed concurrently.");
        }

        return OccurrenceDeleteResult.Success();
    }

    private Task<bool> IsActiveRoleAsync(
        Guid userId,
        string roleKey,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == userId
                    && x.Status == UserStatus.Active
                    && x.Roles.Any(userRole => userRole.Role.Key == roleKey),
                cancellationToken);

    private void MarkNewHistoryAsAdded(Occurrence occurrence, int historyCountBefore)
    {
        foreach (var statusChange in occurrence.StatusHistory.Skip(historyCountBefore))
            dbContext.Entry(statusChange).State = EntityState.Added;
    }

    private async Task<OccurrenceLifecycleResult?> SaveAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            return OccurrenceLifecycleResult.Failure(
                "occurrence_lifecycle_conflict",
                "The occurrence lifecycle change could not be persisted because data changed concurrently.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return OccurrenceLifecycleResult.Failure(
                "occurrence_lifecycle_conflict",
                "The occurrence changed concurrently. Retry the operation.");
        }
    }

    private static OccurrenceLifecycleItem ToItem(Occurrence occurrence) =>
        new(
            occurrence.Id,
            occurrence.Status.Value,
            occurrence.UpdatedAt,
            occurrence.ClosedAt,
            occurrence.CancelledAt);
}
