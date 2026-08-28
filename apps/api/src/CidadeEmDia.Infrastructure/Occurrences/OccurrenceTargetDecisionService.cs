using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceTargetDecisionService(AppDbContext dbContext)
    : IOccurrenceTargetDecisionService
{
    public async Task<OccurrenceTargetDecisionResult> AcceptAsync(
        Guid masterUserId,
        Guid occurrenceId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        return await DecideAsync(
            masterUserId,
            occurrenceId,
            targetId,
            rejectionReason: null,
            accept: true,
            cancellationToken);
    }

    public async Task<OccurrenceTargetDecisionResult> RejectAsync(
        Guid masterUserId,
        Guid occurrenceId,
        Guid targetId,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        return await DecideAsync(
            masterUserId,
            occurrenceId,
            targetId,
            rejectionReason,
            accept: false,
            cancellationToken);
    }

    public async Task<OccurrenceTargetDecisionItem?> GetAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty || targetId == Guid.Empty)
            return null;

        var target = await dbContext.OccurrenceTargets
            .AsNoTracking()
            .Include(x => x.Occurrence)
            .FirstOrDefaultAsync(
                x => x.Id == targetId && x.OccurrenceId == occurrenceId,
                cancellationToken);

        if (target is null)
            return null;

        if (target.Occurrence.AuthorUserId == requesterUserId)
            return ToItem(target, target.Occurrence.Status);

        if (target.MasterUserId != requesterUserId)
            return null;

        var requesterIsActiveMaster = await IsActiveMasterAsync(requesterUserId, cancellationToken);
        return requesterIsActiveMaster
            ? ToItem(target, target.Occurrence.Status)
            : null;
    }

    private async Task<OccurrenceTargetDecisionResult> DecideAsync(
        Guid masterUserId,
        Guid occurrenceId,
        Guid targetId,
        string? rejectionReason,
        bool accept,
        CancellationToken cancellationToken)
    {
        if (masterUserId == Guid.Empty || occurrenceId == Guid.Empty || targetId == Guid.Empty)
            return OccurrenceTargetDecisionResult.Failure("invalid_target_decision");

        if (!await IsActiveMasterAsync(masterUserId, cancellationToken))
        {
            return OccurrenceTargetDecisionResult.Failure(
                "master_not_eligible",
                "The authenticated user is not an active Master.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Targets)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == occurrenceId, cancellationToken);

        if (occurrence is null)
            return OccurrenceTargetDecisionResult.Failure("target_not_found");

        var target = occurrence.Targets.FirstOrDefault(x =>
            x.Id == targetId && x.MasterUserId == masterUserId);

        if (target is null)
            return OccurrenceTargetDecisionResult.Failure("target_not_found");

        if (target.Status != OccurrenceTargetStatus.Pending)
        {
            return OccurrenceTargetDecisionResult.Failure(
                "target_already_decided",
                "This target has already been accepted or rejected.");
        }

        if (occurrence.Status.IsTerminal)
        {
            return OccurrenceTargetDecisionResult.Failure(
                "occurrence_terminal",
                "Targets cannot be accepted or rejected after the occurrence is closed or cancelled.");
        }

        var decidedAt = DateTimeOffset.UtcNow;

        try
        {
            if (accept)
            {
                target = occurrence.AcceptMasterTarget(targetId, masterUserId, decidedAt);
            }
            else
            {
                target = occurrence.RejectMasterTarget(
                    targetId,
                    masterUserId,
                    rejectionReason ?? string.Empty,
                    decidedAt);
            }
        }
        catch (DomainException exception)
        {
            var errorCode = !accept && string.IsNullOrWhiteSpace(rejectionReason)
                ? "rejection_reason_required"
                : "invalid_target_decision";

            return OccurrenceTargetDecisionResult.Failure(errorCode, exception.Message);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceTargetDecisionResult.Failure(
                "target_decision_conflict",
                "The target decision could not be persisted because it changed concurrently.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return OccurrenceTargetDecisionResult.Failure(
                "target_decision_conflict",
                "The target decision changed concurrently. Retry the operation.");
        }

        return OccurrenceTargetDecisionResult.Success(ToItem(target, occurrence.Status));
    }

    private Task<bool> IsActiveMasterAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == userId
                    && x.Status == UserStatus.Active
                    && x.Roles.Any(userRole => userRole.Role.Key == IdentityRoleKeys.Master),
                cancellationToken);

    private static OccurrenceTargetDecisionItem ToItem(
        OccurrenceTarget target,
        OccurrenceStatus occurrenceStatus) =>
        new(
            target.Id,
            target.OccurrenceId,
            target.MasterUserId,
            occurrenceStatus.Value,
            target.Status.Value,
            target.RejectionReason,
            target.SentAt,
            target.AcceptedAt,
            target.RejectedAt,
            target.ClosedAt);
}
