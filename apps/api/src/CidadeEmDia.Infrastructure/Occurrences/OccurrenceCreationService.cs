using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceCreationService(
    AppDbContext dbContext,
    IOccurrenceService occurrenceService)
    : IOccurrenceCreationService
{
    public async Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        Guid masterUserId,
        CreateOccurrenceInput input,
        IReadOnlyCollection<Guid>? mediaIds,
        CancellationToken cancellationToken = default)
    {
        if (masterUserId == Guid.Empty)
        {
            return CreateOccurrenceResult.Failure(
                "master_not_eligible",
                "A valid Master account must be selected before publishing the occurrence.");
        }

        if (string.IsNullOrWhiteSpace(input.ExternalProtocolNumber))
        {
            return CreateOccurrenceResult.Failure(
                "invalid_input",
                "External protocol number is required.");
        }

        var requestedMediaIds = mediaIds?
            .Where(id => id != Guid.Empty)
            .ToArray()
            ?? [];

        if (requestedMediaIds.Length == 0)
        {
            return CreateOccurrenceResult.Failure(
                "photo_required",
                "At least one photo is required to publish an occurrence.");
        }

        if (mediaIds is null
            || requestedMediaIds.Length != mediaIds.Count
            || requestedMediaIds.Distinct().Count() != requestedMediaIds.Length)
        {
            return CreateOccurrenceResult.Failure(
                "invalid_media_selection",
                "Occurrence media ids must be non-empty and unique.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var masterEligible = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == masterUserId
                    && user.Status == UserStatus.Active
                    && user.Roles.Any(userRole => userRole.Role.Key == IdentityRoleKeys.Master),
                cancellationToken);

        if (!masterEligible)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "master_not_eligible",
                "The selected user is not an active Master account.");
        }

        var media = await dbContext.OccurrenceMedia
            .Where(item => requestedMediaIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (media.Count != requestedMediaIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "media_not_ready_or_owned",
                "Every selected media item must exist, belong to the author and be ready.");
        }

        if (media.Any(item =>
            item.UploaderUserId != authorUserId
            || item.Status != OccurrenceMediaStatus.Ready
            || item.OccurrenceId.HasValue))
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "media_not_ready_or_owned",
                "Every selected media item must exist, belong to the author, be ready and not already be attached.");
        }

        if (!media.Any(item => item.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "photo_required",
                "At least one selected media item must be a photo.");
        }

        var occurrenceResult = await occurrenceService.CreateAsync(
            authorUserId,
            input,
            cancellationToken);

        if (!occurrenceResult.Succeeded || occurrenceResult.Occurrence is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return occurrenceResult;
        }

        var occurrence = dbContext.Occurrences.Local
            .FirstOrDefault(item => item.Id == occurrenceResult.Occurrence.Id)
            ?? await dbContext.Occurrences
                .Include(item => item.Targets)
                .FirstOrDefaultAsync(
                    item => item.Id == occurrenceResult.Occurrence.Id,
                    cancellationToken);

        if (occurrence is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "occurrence_persistence_conflict",
                "The new occurrence could not be loaded for its initial assignment.");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var target = occurrence.AddMasterTarget(masterUserId, now);
            dbContext.OccurrenceTargets.Add(target);

            foreach (var item in media)
                item.AttachToOccurrence(occurrenceResult.Occurrence.Id, now);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure("target_persistence_conflict", exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "target_persistence_conflict",
                "Occurrence, media and the initial Master request could not be persisted atomically.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "target_persistence_conflict",
                "Occurrence creation changed concurrently. Retry the operation.");
        }

        return occurrenceResult;
    }
}
