using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceGeoSearchService(
    AppDbContext dbContext,
    R2ObjectStorage storage) : IOccurrenceGeoSearchService
{
    private const int MaxPageSize = 50;
    private const int DefaultPublicPageSize = 12;
    private const decimal DefaultPublicRadiusKm = 25m;
    private const decimal MaxRadiusKm = 100m;
    private const string DefaultPublicCity = "São Paulo";

    public async Task<OccurrenceListResult> SearchMineAsync(
        Guid authorUserId,
        OccurrenceGeoSearchInput input,
        CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty)
            return OccurrenceListResult.Failure("invalid_author");

        if (!TryValidateLocationFilter(input, out var validationError))
            return OccurrenceListResult.Failure("invalid_geo_filter", validationError);

        var page = Math.Max(input.Page, 1);
        var pageSize = Math.Clamp(input.PageSize, 1, MaxPageSize);

        IQueryable<Occurrence> query;
        if (input.Latitude.HasValue && input.Longitude.HasValue && input.RadiusKm.HasValue)
        {
            query = CreateRadiusQuery(
                input.Latitude.Value,
                input.Longitude.Value,
                input.RadiusKm.Value);
        }
        else
        {
            query = dbContext.Occurrences.AsNoTracking();
        }

        query = query.Where(x => x.AuthorUserId == authorUserId);

        if (input.CategoryId.HasValue)
        {
            if (input.CategoryId.Value == Guid.Empty)
                return OccurrenceListResult.Failure("invalid_category");

            query = query.Where(x => x.CategoryId == input.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            OccurrenceStatus parsedStatus;
            try
            {
                parsedStatus = OccurrenceStatus.From(input.Status);
            }
            catch (DomainException exception)
            {
                return OccurrenceListResult.Failure("invalid_status", exception.Message);
            }

            query = query.Where(x => x.Status == parsedStatus);
        }

        var city = input.City?.Trim();
        if (!string.IsNullOrWhiteSpace(city))
        {
            if (city.Length > 120)
                return OccurrenceListResult.Failure("invalid_city", "City filter cannot exceed 120 characters.");

            query = query.Where(x => EF.Functions.ILike(x.AddressText, $"%{city}%"));
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

    public async Task<PublicOccurrenceSearchResult> SearchPublicAsync(
        PublicOccurrenceSearchInput input,
        CancellationToken cancellationToken = default)
    {
        var hasLatitude = input.Latitude.HasValue;
        var hasLongitude = input.Longitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            return PublicOccurrenceSearchResult.Failure(
                "invalid_geo_filter",
                "Latitude and longitude must be supplied together.");
        }

        if (input.Latitude is < -90m or > 90m)
            return PublicOccurrenceSearchResult.Failure("invalid_geo_filter", "Latitude must be between -90 and 90.");

        if (input.Longitude is < -180m or > 180m)
            return PublicOccurrenceSearchResult.Failure("invalid_geo_filter", "Longitude must be between -180 and 180.");

        var radiusKm = input.RadiusKm ?? DefaultPublicRadiusKm;
        if ((hasLatitude || input.RadiusKm.HasValue) && radiusKm is <= 0m or > MaxRadiusKm)
        {
            return PublicOccurrenceSearchResult.Failure(
                "invalid_geo_filter",
                $"Radius must be greater than zero and at most {MaxRadiusKm:0} km.");
        }

        if (!hasLatitude && input.RadiusKm.HasValue)
        {
            return PublicOccurrenceSearchResult.Failure(
                "invalid_geo_filter",
                "Radius requires latitude and longitude.");
        }

        var city = input.City?.Trim();
        if (!string.IsNullOrWhiteSpace(city) && city.Length > 120)
            return PublicOccurrenceSearchResult.Failure("invalid_city", "City filter cannot exceed 120 characters.");

        if (!hasLatitude && string.IsNullOrWhiteSpace(city))
            city = DefaultPublicCity;

        IQueryable<Occurrence> query = hasLatitude
            ? CreateRadiusQuery(input.Latitude!.Value, input.Longitude!.Value, radiusKm)
            : dbContext.Occurrences.AsNoTracking();

        query = ApplyPublicVisibility(query);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(x => EF.Functions.ILike(x.AddressText, $"%{city}%"));

        var page = Math.Max(input.Page, 1);
        var pageSize = Math.Clamp(input.PageSize <= 0 ? DefaultPublicPageSize : input.PageSize, 1, MaxPageSize);
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = rows
            .Select(x => x.CategoryId)
            .Distinct()
            .ToArray();

        var categories = categoryIds.Length == 0
            ? new Dictionary<Guid, CategoryInfo>()
            : await dbContext.OccurrenceCategories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .Select(x => new CategoryInfo(x.Id, x.Name, x.Slug))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var occurrenceIds = rows.Select(x => x.Id).ToArray();
        var covers = await LoadPublicCoverMediaAsync(occurrenceIds, cancellationToken);
        var supportCounts = occurrenceIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.OccurrenceSupports
                .AsNoTracking()
                .Where(support => occurrenceIds.Contains(support.OccurrenceId))
                .GroupBy(support => support.OccurrenceId)
                .Select(group => new { OccurrenceId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.OccurrenceId, x => x.Count, cancellationToken);

        var items = rows
            .Select(x =>
            {
                var category = categories.GetValueOrDefault(x.CategoryId);
                return new PublicOccurrenceItem(
                    x.Id,
                    x.PublicCode.Value,
                    category?.Name ?? string.Empty,
                    category?.Slug ?? string.Empty,
                    x.Title,
                    x.Description,
                    x.Status.Value,
                    x.AddressText,
                    x.ExternalProtocolNumber,
                    supportCounts.GetValueOrDefault(x.Id),
                    x.CreatedAt,
                    x.UpdatedAt,
                    covers.GetValueOrDefault(x.Id));
            })
            .ToArray();

        return PublicOccurrenceSearchResult.Success(new PublicOccurrencePage(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }

    public async Task<PublicOccurrenceDetails?> GetPublicDetailsAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (occurrenceId == Guid.Empty)
            return null;

        var occurrence = await ApplyPublicVisibility(dbContext.Occurrences.AsNoTracking())
            .FirstOrDefaultAsync(x => x.Id == occurrenceId, cancellationToken);

        if (occurrence is null)
            return null;

        var category = await dbContext.OccurrenceCategories
            .AsNoTracking()
            .Where(x => x.Id == occurrence.CategoryId)
            .Select(x => new CategoryInfo(x.Id, x.Name, x.Slug))
            .FirstOrDefaultAsync(cancellationToken);

        var media = await LoadPublicMediaAsync(occurrence.Id, cancellationToken);
        var supportCount = await dbContext.OccurrenceSupports
            .AsNoTracking()
            .CountAsync(support => support.OccurrenceId == occurrence.Id, cancellationToken);

        return new PublicOccurrenceDetails(
            occurrence.Id,
            occurrence.PublicCode.Value,
            category?.Name ?? string.Empty,
            category?.Slug ?? string.Empty,
            occurrence.Title,
            occurrence.Description,
            occurrence.Status.Value,
            occurrence.AddressText,
            occurrence.PostalCode,
            occurrence.StateCode,
            occurrence.Location.Latitude,
            occurrence.Location.Longitude,
            occurrence.ExternalProtocolNumber,
            occurrence.ExternalProtocolAgency,
            supportCount,
            occurrence.CreatedAt,
            occurrence.UpdatedAt,
            media);
    }

    private async Task<Dictionary<Guid, PublicOccurrenceMediaItem>> LoadPublicCoverMediaAsync(
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

            result[occurrenceId] = CreatePublicMediaItem(media);
        }

        return result;
    }

    private async Task<IReadOnlyList<PublicOccurrenceMediaItem>> LoadPublicMediaAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
            return Array.Empty<PublicOccurrenceMediaItem>();

        var mediaRows = await dbContext.OccurrenceMedia
            .AsNoTracking()
            .Where(media => media.OccurrenceId == occurrenceId
                && media.Status == OccurrenceMediaStatus.Ready)
            .OrderBy(media => media.CreatedAt)
            .ThenBy(media => media.Id)
            .ToListAsync(cancellationToken);

        return mediaRows.Select(CreatePublicMediaItem).ToArray();
    }

    private PublicOccurrenceMediaItem CreatePublicMediaItem(OccurrenceMedia media)
    {
        var now = DateTimeOffset.UtcNow;
        var readUrl = storage.CreateReadUrl(media.ObjectKey, now, out var expiresAt);
        return new PublicOccurrenceMediaItem(
            media.Id,
            media.OriginalFileName,
            media.ContentType,
            readUrl,
            expiresAt);
    }

    private static IQueryable<Occurrence> ApplyPublicVisibility(IQueryable<Occurrence> query) =>
        query.Where(x =>
            x.Status != OccurrenceStatus.Closed
            && x.Status != OccurrenceStatus.Cancelled);

    private IQueryable<Occurrence> CreateRadiusQuery(
        decimal latitude,
        decimal longitude,
        decimal radiusKm)
    {
        var lat = (double)latitude;
        var lng = (double)longitude;
        var radiusMeters = (double)(radiusKm * 1000m);

        return dbContext.Occurrences
            .FromSqlInterpolated($"""
                SELECT *
                FROM occurrences
                WHERE ST_DWithin(
                    location,
                    ST_SetSRID(ST_MakePoint({lng}, {lat}), 4326)::geography,
                    {radiusMeters})
                """)
            .AsNoTracking();
    }

    private static bool TryValidateLocationFilter(
        OccurrenceGeoSearchInput input,
        out string? error)
    {
        var hasLatitude = input.Latitude.HasValue;
        var hasLongitude = input.Longitude.HasValue;
        var hasRadius = input.RadiusKm.HasValue;
        var hasAnyGeo = hasLatitude || hasLongitude || hasRadius;
        var hasCompleteGeo = hasLatitude && hasLongitude && hasRadius;

        if (hasAnyGeo && !hasCompleteGeo)
        {
            error = "Latitude, longitude and radiusKm must be supplied together.";
            return false;
        }

        if (!hasCompleteGeo)
        {
            error = null;
            return true;
        }

        if (input.Latitude is < -90m or > 90m)
        {
            error = "Latitude must be between -90 and 90.";
            return false;
        }

        if (input.Longitude is < -180m or > 180m)
        {
            error = "Longitude must be between -180 and 180.";
            return false;
        }

        if (input.RadiusKm is <= 0m or > MaxRadiusKm)
        {
            error = $"Radius must be greater than zero and at most {MaxRadiusKm:0} km.";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record CategoryInfo(Guid Id, string Name, string Slug);
}
