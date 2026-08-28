using System.Data;
using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceService(AppDbContext dbContext) : IOccurrenceService
{
    private const int MaxPageSize = 50;

    public async Task<IReadOnlyList<OccurrenceCategoryItem>> GetActiveCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.OccurrenceCategories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return categories
            .Where(x => x.Status == OccurrenceCategoryStatus.Active)
            .Select(x => new OccurrenceCategoryItem(x.Id, x.Name, x.Slug, x.DisplayOrder))
            .ToArray();
    }

    public async Task<IReadOnlyList<EligibleMasterItem>> GetEligibleMastersAsync(
        CancellationToken cancellationToken = default)
    {
        var masters = await dbContext.Users
            .AsNoTracking()
            .Where(x =>
                x.Status == UserStatus.Active
                && x.Roles.Any(userRole => userRole.Role.Key == IdentityRoleKeys.Master))
            .Select(x => new
            {
                x.Id,
                DisplayName = x.Profile != null ? x.Profile.DisplayName : null
            })
            .ToListAsync(cancellationToken);

        return masters
            .Select(x => new EligibleMasterItem(
                x.Id,
                string.IsNullOrWhiteSpace(x.DisplayName) ? "Master" : x.DisplayName))
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Id)
            .ToArray();
    }

    public async Task<CreateOccurrenceResult> CreateAsync(
        Guid authorUserId,
        CreateOccurrenceInput input,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty)
            return CreateOccurrenceResult.Failure("invalid_author");

        if (!IsCreateInputShapeValid(input, out var inputError))
            return CreateOccurrenceResult.Failure("invalid_input", inputError);

        var authorExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == authorUserId && x.Status == UserStatus.Active,
                cancellationToken);

        if (!authorExists)
            return CreateOccurrenceResult.Failure("author_not_found");

        var category = await dbContext.OccurrenceCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == input.CategoryId, cancellationToken);

        if (category is null)
            return CreateOccurrenceResult.Failure("category_not_found");

        if (category.Status != OccurrenceCategoryStatus.Active)
            return CreateOccurrenceResult.Failure("category_inactive");

        Occurrence occurrence;
        try
        {
            occurrence = new Occurrence(
                authorUserId,
                input.CategoryId,
                input.Title,
                input.Description,
                input.AddressText,
                new OccurrenceLocation(input.Latitude, input.Longitude),
                input.PostalCode,
                input.CityId,
                input.StateCode,
                input.ExternalProtocolNumber,
                input.ExternalProtocolAgency);
        }
        catch (DomainException exception)
        {
            return CreateOccurrenceResult.Failure("invalid_input", exception.Message);
        }

        dbContext.Occurrences.Add(occurrence);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return CreateOccurrenceResult.Failure(
                "occurrence_persistence_conflict",
                "The occurrence could not be persisted with the supplied references.");
        }

        return CreateOccurrenceResult.Success(ToDetails(occurrence, category.Name));
    }

    public async Task<OccurrenceListResult> GetMineAsync(
        Guid authorUserId,
        string? status,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty)
            return OccurrenceListResult.Failure("invalid_author");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = dbContext.Occurrences
            .AsNoTracking()
            .Where(x => x.AuthorUserId == authorUserId);

        if (categoryId.HasValue)
        {
            if (categoryId.Value == Guid.Empty)
                return OccurrenceListResult.Failure("invalid_category");

            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            OccurrenceStatus parsedStatus;
            try
            {
                parsedStatus = OccurrenceStatus.From(status);
            }
            catch (DomainException exception)
            {
                return OccurrenceListResult.Failure("invalid_status", exception.Message);
            }

            query = query.Where(x => x.Status == parsedStatus);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OccurrenceListProjection(
                x.Id,
                x.PublicCode,
                x.CategoryId,
                x.Title,
                x.Status,
                x.AddressText,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var categoryIds = rows
            .Select(x => x.CategoryId)
            .Distinct()
            .ToArray();

        var categoryNames = categoryIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.OccurrenceCategories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var items = rows
            .Select(x => new OccurrenceListItem(
                x.Id,
                x.PublicCode.Value,
                x.CategoryId,
                categoryNames.GetValueOrDefault(x.CategoryId, string.Empty),
                x.Title,
                x.Status.Value,
                x.AddressText,
                x.CreatedAt,
                x.UpdatedAt))
            .ToArray();

        return OccurrenceListResult.Success(new OccurrencePage(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }

    public async Task<OccurrenceDetails?> GetMineByIdAsync(
        Guid authorUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return null;

        var occurrence = await LoadOwnedOccurrenceAsync(
            x => x.AuthorUserId == authorUserId && x.Id == occurrenceId,
            cancellationToken);

        return occurrence is null
            ? null
            : await ToDetailsAsync(occurrence, cancellationToken);
    }

    public async Task<OccurrenceDetails?> GetMineByPublicCodeAsync(
        Guid authorUserId,
        string publicCode,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty)
            return null;

        OccurrencePublicCode parsedCode;
        try
        {
            parsedCode = OccurrencePublicCode.From(publicCode);
        }
        catch (DomainException)
        {
            return null;
        }

        var occurrence = await LoadOwnedOccurrenceAsync(
            x => x.AuthorUserId == authorUserId && x.PublicCode == parsedCode,
            cancellationToken);

        return occurrence is null
            ? null
            : await ToDetailsAsync(occurrence, cancellationToken);
    }

    public async Task<AddOccurrenceTargetResult> AddMasterTargetAsync(
        Guid authorUserId,
        Guid occurrenceId,
        Guid masterUserId,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty || occurrenceId == Guid.Empty || masterUserId == Guid.Empty)
            return AddOccurrenceTargetResult.Failure("invalid_target_input");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var occurrence = await dbContext.Occurrences
            .Include(x => x.Targets)
            .FirstOrDefaultAsync(
                x => x.Id == occurrenceId && x.AuthorUserId == authorUserId,
                cancellationToken);

        if (occurrence is null)
            return AddOccurrenceTargetResult.Failure("occurrence_not_found");

        var master = await dbContext.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == masterUserId
                && x.Status == UserStatus.Active
                && x.Roles.Any(userRole => userRole.Role.Key == IdentityRoleKeys.Master))
            .Select(x => new
            {
                x.Id,
                DisplayName = x.Profile != null ? x.Profile.DisplayName : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (master is null)
            return AddOccurrenceTargetResult.Failure(
                "master_not_eligible",
                "The selected user is not an active Master.");

        if (occurrence.Targets.Any(target => target.MasterUserId == masterUserId))
            return AddOccurrenceTargetResult.Failure(
                "duplicate_target",
                "This occurrence is already shared with the selected Master.");

        if (occurrence.Targets.Count >= Occurrence.MaxTargetsPerOccurrence)
            return AddOccurrenceTargetResult.Failure(
                "target_limit_reached",
                $"An occurrence can be shared with at most {Occurrence.MaxTargetsPerOccurrence} Masters.");

        OccurrenceTarget target;
        try
        {
            target = occurrence.AddMasterTarget(masterUserId, DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return AddOccurrenceTargetResult.Failure("invalid_target", exception.Message);
        }

        dbContext.OccurrenceTargets.Add(target);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return AddOccurrenceTargetResult.Failure(
                "target_persistence_conflict",
                "The target could not be persisted because the occurrence changed concurrently or the destination is duplicated.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return AddOccurrenceTargetResult.Failure(
                "target_persistence_conflict",
                "The occurrence changed concurrently. Retry the operation.");
        }

        return AddOccurrenceTargetResult.Success(ToTargetItem(
            target,
            string.IsNullOrWhiteSpace(master.DisplayName) ? "Master" : master.DisplayName));
    }

    public async Task<IReadOnlyList<OccurrenceTargetItem>?> GetTargetsAsync(
        Guid requesterUserId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || occurrenceId == Guid.Empty)
            return null;

        var occurrenceAuthorUserId = await dbContext.Occurrences
            .AsNoTracking()
            .Where(x => x.Id == occurrenceId)
            .Select(x => (Guid?)x.AuthorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!occurrenceAuthorUserId.HasValue)
            return null;

        var requesterIsAuthor = occurrenceAuthorUserId.Value == requesterUserId;
        var requesterIsMaster = requesterIsAuthor || await dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == requesterUserId
                    && x.Role.Key == IdentityRoleKeys.Master
                    && x.User.Status == UserStatus.Active,
                cancellationToken);

        if (!requesterIsMaster)
            return null;

        var query = dbContext.OccurrenceTargets
            .AsNoTracking()
            .Where(x => x.OccurrenceId == occurrenceId);

        if (!requesterIsAuthor)
            query = query.Where(x => x.MasterUserId == requesterUserId);

        var rows = await query
            .OrderBy(x => x.SentAt)
            .ThenBy(x => x.Id)
            .Select(x => new OccurrenceTargetProjection(
                x.Id,
                x.OccurrenceId,
                x.MasterUserId,
                x.Status,
                x.SentAt,
                x.AcceptedAt,
                x.RejectedAt,
                x.ClosedAt))
            .ToListAsync(cancellationToken);

        if (!requesterIsAuthor && rows.Count == 0)
            return null;

        var masterUserIds = rows
            .Select(x => x.MasterUserId)
            .Distinct()
            .ToArray();

        var masterNames = masterUserIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users
                .AsNoTracking()
                .Where(x => masterUserIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    DisplayName = x.Profile != null ? x.Profile.DisplayName : null
                })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => string.IsNullOrWhiteSpace(x.DisplayName) ? "Master" : x.DisplayName!,
                    cancellationToken);

        return rows
            .Select(x => new OccurrenceTargetItem(
                x.Id,
                x.OccurrenceId,
                x.MasterUserId,
                masterNames.GetValueOrDefault(x.MasterUserId, "Master"),
                x.Status.Value,
                x.SentAt,
                x.AcceptedAt,
                x.RejectedAt,
                x.ClosedAt))
            .ToArray();
    }

    private async Task<Occurrence?> LoadOwnedOccurrenceAsync(
        System.Linq.Expressions.Expression<Func<Occurrence, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await dbContext.Occurrences
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.StatusHistory)
            .Include(x => x.Complements)
            .Include(x => x.ServiceForecastHistory)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private async Task<OccurrenceDetails> ToDetailsAsync(
        Occurrence occurrence,
        CancellationToken cancellationToken)
    {
        var categoryName = await dbContext.OccurrenceCategories
            .AsNoTracking()
            .Where(x => x.Id == occurrence.CategoryId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? string.Empty;

        return ToDetails(occurrence, categoryName);
    }

    private static OccurrenceTargetItem ToTargetItem(OccurrenceTarget target, string masterDisplayName) =>
        new(
            target.Id,
            target.OccurrenceId,
            target.MasterUserId,
            masterDisplayName,
            target.Status.Value,
            target.SentAt,
            target.AcceptedAt,
            target.RejectedAt,
            target.ClosedAt);

    private static OccurrenceDetails ToDetails(Occurrence occurrence, string categoryName) =>
        new(
            occurrence.Id,
            occurrence.PublicCode.Value,
            occurrence.CategoryId,
            categoryName,
            occurrence.Title,
            occurrence.Description,
            occurrence.Status.Value,
            occurrence.AddressText,
            occurrence.PostalCode,
            occurrence.CityId,
            occurrence.StateCode,
            occurrence.Location.Latitude,
            occurrence.Location.Longitude,
            occurrence.ExternalProtocolNumber,
            occurrence.ExternalProtocolAgency,
            occurrence.CreatedAt,
            occurrence.UpdatedAt,
            occurrence.ClosedAt,
            occurrence.CancelledAt,
            occurrence.CurrentServiceForecast,
            occurrence.StatusHistory
                .OrderBy(x => x.CreatedAt)
                .Select(x => new OccurrenceStatusHistoryItem(
                    x.Id,
                    x.FromStatus?.Value,
                    x.ToStatus.Value,
                    x.CreatedAt,
                    x.Reason))
                .ToArray(),
            occurrence.Complements
                .OrderBy(x => x.CreatedAt)
                .Select(x => new OccurrenceComplementItem(x.Id, x.Content, x.CreatedAt))
                .ToArray(),
            occurrence.ServiceForecastHistory
                .OrderBy(x => x.DefinedAt)
                .Select(x => new OccurrenceServiceForecastItem(
                    x.Id,
                    x.EstimatedFor,
                    x.DefinedAt,
                    x.Note))
                .ToArray());

    private static bool IsCreateInputShapeValid(CreateOccurrenceInput input, out string? error)
    {
        if (input.CategoryId == Guid.Empty)
        {
            error = "Occurrence category is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 240)
        {
            error = "Occurrence title is required and must contain at most 240 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.AddressText) || input.AddressText.Trim().Length > 500)
        {
            error = "Occurrence address is required and must contain at most 500 characters.";
            return false;
        }

        if (input.ExternalProtocolNumber?.Trim().Length > 160)
        {
            error = "External protocol number must contain at most 160 characters.";
            return false;
        }

        if (input.ExternalProtocolAgency?.Trim().Length > 200)
        {
            error = "External protocol agency must contain at most 200 characters.";
            return false;
        }

        if (input.CityId == Guid.Empty)
        {
            error = "City id cannot be empty when supplied.";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record OccurrenceListProjection(
        Guid Id,
        OccurrencePublicCode PublicCode,
        Guid CategoryId,
        string Title,
        OccurrenceStatus Status,
        string AddressText,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record OccurrenceTargetProjection(
        Guid Id,
        Guid OccurrenceId,
        Guid MasterUserId,
        OccurrenceTargetStatus Status,
        DateTimeOffset SentAt,
        DateTimeOffset? AcceptedAt,
        DateTimeOffset? RejectedAt,
        DateTimeOffset? ClosedAt);
}
