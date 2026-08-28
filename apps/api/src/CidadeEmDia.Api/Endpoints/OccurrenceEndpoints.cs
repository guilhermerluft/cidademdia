using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceEndpoints
{
    public static RouteGroupBuilder MapOccurrenceEndpoints(this RouteGroupBuilder api)
    {
        var occurrences = api.MapGroup("/occurrences").RequireAuthorization();

        occurrences.MapGet("/categories", async (
            IOccurrenceService occurrenceService,
            CancellationToken cancellationToken) =>
        {
            var categories = await occurrenceService.GetActiveCategoriesAsync(cancellationToken);
            return Results.Ok(categories);
        });

        occurrences.MapPost("", async (
            CreateOccurrenceRequest request,
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await occurrenceService.CreateAsync(
                userId,
                new CreateOccurrenceInput(
                    request.CategoryId,
                    request.Title,
                    request.Description,
                    request.AddressText,
                    request.Latitude,
                    request.Longitude,
                    request.PostalCode,
                    request.CityId,
                    request.StateCode,
                    request.ExternalProtocolNumber,
                    request.ExternalProtocolAgency),
                cancellationToken);

            if (result.Succeeded && result.Occurrence is not null)
            {
                return Results.Created(
                    $"/api/v1/occurrences/{result.Occurrence.Id}",
                    result.Occurrence);
            }

            return MapCreateFailure(result, httpContext);
        });

        occurrences.MapGet("", async (
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            string? status,
            Guid? categoryId,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await occurrenceService.GetMineAsync(
                userId,
                status,
                categoryId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken);

            if (result.Succeeded && result.Page is not null)
                return Results.Ok(result.Page);

            return Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? "invalid_query",
                result.ErrorDetail);
        });

        occurrences.MapGet("/by-code/{publicCode}", async (
            string publicCode,
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var occurrence = await occurrenceService.GetMineByPublicCodeAsync(
                userId,
                publicCode,
                cancellationToken);

            return occurrence is null ? Results.NotFound() : Results.Ok(occurrence);
        });

        occurrences.MapGet("/{occurrenceId:guid}", async (
            Guid occurrenceId,
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var occurrence = await occurrenceService.GetMineByIdAsync(
                userId,
                occurrenceId,
                cancellationToken);

            return occurrence is null ? Results.NotFound() : Results.Ok(occurrence);
        });

        return api;
    }

    private static IResult MapCreateFailure(CreateOccurrenceResult result, HttpContext httpContext) =>
        result.ErrorCode switch
        {
            "author_not_found" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                result.ErrorCode,
                "The authenticated user is not active or no longer exists."),
            "category_not_found" => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode,
                "The occurrence category does not exist."),
            "category_inactive" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                result.ErrorCode,
                "The occurrence category is inactive."),
            "occurrence_persistence_conflict" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                result.ErrorCode,
                result.ErrorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? "invalid_input",
                result.ErrorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record CreateOccurrenceRequest(
        Guid CategoryId,
        string Title,
        string? Description,
        string AddressText,
        decimal Latitude,
        decimal Longitude,
        string? PostalCode,
        Guid? CityId,
        string? StateCode,
        string? ExternalProtocolNumber,
        string? ExternalProtocolAgency);
}
