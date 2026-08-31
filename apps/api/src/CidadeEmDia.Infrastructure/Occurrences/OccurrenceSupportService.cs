using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceSupportService(AppDbContext dbContext)
    : IOccurrenceSupportService
{
    public async Task<OccurrenceSupportResult> GetAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceSupportResult.Failure("invalid_support_request");

        if (!await OccurrenceExistsAsync(occurrenceId, cancellationToken))
            return OccurrenceSupportResult.Failure("occurrence_not_found");

        return OccurrenceSupportResult.Success(
            await BuildItemAsync(requesterUserId, occurrenceId, cancellationToken));
    }

    public async Task<OccurrenceSupportResult> SupportAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceSupportResult.Failure("invalid_support_request");

        if (!await OccurrenceExistsAsync(occurrenceId, cancellationToken))
            return OccurrenceSupportResult.Failure("occurrence_not_found");

        var requesterIsActive = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == requesterUserId && user.Status == UserStatus.Active,
                cancellationToken);

        if (!requesterIsActive)
            return OccurrenceSupportResult.Failure("support_not_allowed");

        var existingSupport = await dbContext.Set<OccurrenceSupport>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                support => support.OccurrenceId == occurrenceId
                    && support.UserId == requesterUserId,
                cancellationToken);

        if (existingSupport is not null)
        {
            return OccurrenceSupportResult.Success(
                await BuildItemAsync(requesterUserId, occurrenceId, cancellationToken));
        }

        var support = new OccurrenceSupport(
            occurrenceId,
            requesterUserId,
            DateTimeOffset.UtcNow);

        dbContext.Set<OccurrenceSupport>().Add(support);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            dbContext.Entry(support).State = EntityState.Detached;

            return OccurrenceSupportResult.Success(
                await BuildItemAsync(requesterUserId, occurrenceId, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return OccurrenceSupportResult.Failure(
                "occurrence_support_conflict",
                "The occurrence support could not be persisted.");
        }

        return OccurrenceSupportResult.Success(
            await BuildItemAsync(requesterUserId, occurrenceId, cancellationToken),
            wasCreated: true);
    }

    private Task<bool> OccurrenceExistsAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken) =>
        dbContext.Occurrences
            .AsNoTracking()
            .AnyAsync(x => x.Id == occurrenceId, cancellationToken);

    private async Task<OccurrenceSupportItem> BuildItemAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        var supports = dbContext.Set<OccurrenceSupport>().AsNoTracking();

        var supportCount = await supports.CountAsync(
            support => support.OccurrenceId == occurrenceId,
            cancellationToken);

        var requesterSupport = await supports
            .Where(support => support.OccurrenceId == occurrenceId
                && support.UserId == requesterUserId)
            .Select(support => new { support.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return new OccurrenceSupportItem(
            occurrenceId,
            supportCount,
            requesterSupport is not null,
            requesterSupport?.CreatedAt);
    }
}
