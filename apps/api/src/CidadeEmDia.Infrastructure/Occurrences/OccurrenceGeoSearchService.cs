using CidadeEmDia.Application.Occurrences;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Occurrences;

internal sealed class OccurrenceGeoSearchService(AppDbContext dbContext) : IOccurrenceGeoSearchService
{
    private const int MaxPageSize = 50;
    private const decimal MaxRadiusKm = 100m;

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
            var latitude = (double)input.Latitude.Value;
            var longitude = (double)input.Longitude.Value;
            var radiusMeters = (double)(input.RadiusKm.Value * 1000m);

            query = dbContext.Occurrences
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM occurrences
                    WHERE ST_DWithin(
                        location,
                        ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography,
                        {radiusMeters})
                    """)
                .AsNoTracking();
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
}
