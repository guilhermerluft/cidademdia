using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
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
        CreateOccurrenceInput input,
        IReadOnlyCollection<Guid>? mediaIds,
        CancellationToken cancellationToken = default)
    {
        var requestedMediaIds = mediaIds?
            .Where(id => id != Guid.Empty)
            .ToArray()
            ?? [];

        if (requestedMediaIds.Length == 0)
            return await occurrenceService.CreateAsync(authorUserId, input, cancellationToken);

        if (requestedMediaIds.Length != mediaIds!.Count
            || requestedMediaIds.Distinct().Count() != requestedMediaIds.Length)
        {
            return CreateOccurrenceResult.Failure(
                "invalid_media_selection",
                "Occurrence media ids must be non-empty and unique.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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

        var occurrenceResult = await occurrenceService.CreateAsync(
            authorUserId,
            input,
            cancellationToken);

        if (!occurrenceResult.Succeeded || occurrenceResult.Occurrence is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return occurrenceResult;
        }

        try
        {
            var attachedAt = DateTimeOffset.UtcNow;
            foreach (var item in media)
                item.AttachToOccurrence(occurrenceResult.Occurrence.Id, attachedAt);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure("media_attach_not_allowed", exception.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "media_persistence_conflict",
                "Occurrence media could not be attached atomically to the new occurrence.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            await transaction.RollbackAsync(cancellationToken);
            return CreateOccurrenceResult.Failure(
                "media_persistence_conflict",
                "Occurrence media changed concurrently. Retry the operation.");
        }

        return occurrenceResult;
    }
}
