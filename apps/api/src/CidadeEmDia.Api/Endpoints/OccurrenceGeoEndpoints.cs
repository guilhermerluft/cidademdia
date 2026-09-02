using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceGeoEndpoints
{
    public static RouteGroupBuilder MapOccurrenceGeoEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/occurrences/geo-search", async (
            IOccurrenceGeoSearchService geoSearchService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            string? status,
            Guid? categoryId,
            string? city,
            decimal? latitude,
            decimal? longitude,
            decimal? radiusKm,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();

            var result = await geoSearchService.SearchMineAsync(
                userId,
                new OccurrenceGeoSearchInput(
                    status,
                    categoryId,
                    city,
                    latitude,
                    longitude,
                    radiusKm,
                    page ?? 1,
                    pageSize ?? 20),
                cancellationToken);

            if (result.Succeeded && result.Page is not null)
                return Results.Ok(result.Page);

            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Occurrence geographic search could not be processed.",
                detail: result.ErrorDetail,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.ErrorCode ?? "invalid_geo_query",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        })
        .RequireAuthorization();

        return api;
    }
}
