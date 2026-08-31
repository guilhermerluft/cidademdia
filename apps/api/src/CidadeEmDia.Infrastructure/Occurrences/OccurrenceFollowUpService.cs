using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceFollowUpService(AppDbContext dbContext)
    : IOccurrenceFollowUpService
{
    private const int MaxForecastNoteLength = 1000;

    public async Task<OccurrenceComplementCommandResult> AddComplementAsync(
        Guid authorUserId,
        Guid occurrenceId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceComplementCommandResult.Failure("invalid_complement");

        if (string.IsNullOrWhiteSpace(content))
        {
            return OccurrenceComplementCommandResult.Failure(
                "complement_content_required",
                "Occurrence complement content is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Complements)
            .FirstOrDefaultAsync(
                x => x.Id == occurrenceId && x.AuthorUserId == authorUserId,
                cancellationToken);

        if (occurrence is null)
            return OccurrenceComplementCommandResult.Failure("occurrence_not_found");

        OccurrenceComplement complement;
        try
        {
            complement = occurrence.AddComplement(
                authorUserId,
                content,
                DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return OccurrenceComplementCommandResult.Failure(
                "invalid_complement",
                exception.Message);
        }

        dbContext.Entry(complement).State = EntityState.Added;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceComplementCommandResult.Failure(
                "occurrence_follow_up_conflict",
                "The occurrence complement could not be persisted because data changed concurrently.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return OccurrenceComplementCommandResult.Failure(
                "occurrence_follow_up_conflict",
                "The occurrence changed concurrently. Retry the operation.");
        }

        return OccurrenceComplementCommandResult.Success(new OccurrenceComplementCommandItem(
            complement.Id,
            occurrence.Id,
            complement.AuthorUserId,
            complement.Content,
            complement.CreatedAt));
    }

    public async Task<OccurrenceForecastCommandResult> SetServiceForecastAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        DateTimeOffset estimatedFor,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return OccurrenceForecastCommandResult.Failure("invalid_forecast");

        if (note?.Trim().Length > MaxForecastNoteLength)
        {
            return OccurrenceForecastCommandResult.Failure(
                "invalid_forecast",
                $"Forecast note must contain at most {MaxForecastNoteLength} characters.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Targets)
            .Include(x => x.ServiceForecastHistory)
            .FirstOrDefaultAsync(x => x.Id == occurrenceId, cancellationToken);

        if (occurrence is null)
            return OccurrenceForecastCommandResult.Failure("occurrence_not_found");

        var requesterIsAcceptedMaster = occurrence.Targets.Any(target =>
            target.MasterUserId == requesterUserId
            && target.Status == OccurrenceTargetStatus.Accepted);

        if (!requesterIsAcceptedMaster
            || !await IsActiveMasterAsync(requesterUserId, cancellationToken))
        {
            return OccurrenceForecastCommandResult.Failure(
                "accepted_master_required",
                "Only an active Master with an accepted target can define the service forecast.");
        }

        if (occurrence.Status.IsTerminal)
        {
            return OccurrenceForecastCommandResult.Failure(
                "occurrence_terminal",
                "A service forecast cannot be changed after the occurrence is closed or cancelled.");
        }

        var definedAt = DateTimeOffset.UtcNow;
        OccurrenceServiceForecast forecast;
        try
        {
            forecast = occurrence.SetServiceForecast(
                estimatedFor,
                requesterUserId,
                definedAt,
                note);
        }
        catch (DomainException exception)
        {
            return OccurrenceForecastCommandResult.Failure(
                "invalid_forecast",
                exception.Message);
        }

        dbContext.Entry(forecast).State = EntityState.Added;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OccurrenceForecastCommandResult.Failure(
                "occurrence_follow_up_conflict",
                "The service forecast could not be persisted because data changed concurrently.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return OccurrenceForecastCommandResult.Failure(
                "occurrence_follow_up_conflict",
                "The occurrence changed concurrently. Retry the operation.");
        }

        return OccurrenceForecastCommandResult.Success(new OccurrenceForecastCommandItem(
            forecast.Id,
            occurrence.Id,
            forecast.DefinedByUserId,
            forecast.EstimatedFor,
            forecast.DefinedAt,
            forecast.Note));
    }

    private Task<bool> IsActiveMasterAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == userId
                    && x.Status == UserStatus.Active
                    && x.Roles.Any(userRole => userRole.Role.Key == IdentityRoleKeys.Master),
                cancellationToken);
}
