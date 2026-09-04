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

        occurrences.MapGet("/masters", async (
            IOccurrenceService occurrenceService,
            CancellationToken cancellationToken) =>
        {
            var masters = await occurrenceService.GetEligibleMastersAsync(cancellationToken);
            return Results.Ok(masters);
        });

        occurrences.MapPost("", async (
            CreateOccurrenceRequest request,
            IOccurrenceCreationService creationService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            if (!TryValidateCreateRequest(request, out var addressText, out var validationError))
            {
                return Problem(
                    httpContext,
                    StatusCodes.Status400BadRequest,
                    "invalid_input",
                    validationError);
            }

            var result = await creationService.CreateAsync(
                userId,
                request.MasterUserId,
                new CreateOccurrenceInput(
                    request.CategoryId,
                    request.Title,
                    request.Description,
                    addressText,
                    request.Latitude,
                    request.Longitude,
                    request.PostalCode,
                    request.CityId,
                    request.StateCode,
                    request.ExternalProtocolNumber,
                    request.ExternalProtocolAgency),
                request.MediaIds,
                cancellationToken);

            if (result.Succeeded && result.Occurrence is not null)
            {
                return Results.Created(
                    $"/api/v1/occurrences/{result.Occurrence.Id}",
                    result.Occurrence);
            }

            return MapCreateFailure(result, httpContext);
        });

        occurrences.MapPost("/{occurrenceId:guid}/targets", async (
            Guid occurrenceId,
            AddOccurrenceTargetRequest request,
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await occurrenceService.AddMasterTargetAsync(
                userId,
                occurrenceId,
                request.MasterUserId,
                cancellationToken);

            if (result.Succeeded && result.Target is not null)
            {
                return Results.Created(
                    $"/api/v1/occurrences/{occurrenceId}/targets/{result.Target.Id}",
                    result.Target);
            }

            return MapTargetFailure(result, httpContext);
        });

        occurrences.MapGet("/{occurrenceId:guid}/targets", async (
            Guid occurrenceId,
            IOccurrenceService occurrenceService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var targets = await occurrenceService.GetTargetsAsync(
                userId,
                occurrenceId,
                cancellationToken);

            return targets is null ? Results.NotFound() : Results.Ok(targets);
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

    private static bool TryValidateCreateRequest(
        CreateOccurrenceRequest request,
        out string addressText,
        out string? error)
    {
        addressText = string.Empty;

        if (request.MasterUserId == Guid.Empty)
        {
            error = "A Master account must be selected before publishing the occurrence.";
            return false;
        }

        var street = request.Street?.Trim();
        var number = request.Number?.Trim();
        var neighborhood = request.Neighborhood?.Trim();
        var city = request.City?.Trim();
        var protocol = request.ExternalProtocolNumber?.Trim();

        if (string.IsNullOrWhiteSpace(street))
        {
            error = "Street is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            error = "Street number is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(neighborhood))
        {
            error = "Neighborhood is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            error = "City is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(protocol))
        {
            error = "External protocol number is required.";
            return false;
        }

        if (street.Length > 220 || number.Length > 40 || neighborhood.Length > 160 || city.Length > 120)
        {
            error = "The structured address exceeds the allowed length.";
            return false;
        }

        var stateCode = request.StateCode?.Trim().ToUpperInvariant();
        var cityState = string.IsNullOrWhiteSpace(stateCode)
            ? city
            : $"{city} - {stateCode}";

        addressText = $"{street}, {number} - {neighborhood}, {cityState}";
        if (addressText.Length > 500)
        {
            error = "Occurrence address must contain at most 500 characters.";
            return false;
        }

        error = null;
        return true;
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
            "master_not_eligible" => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode,
                result.ErrorDetail ?? "The selected Master account is not eligible to receive occurrences."),
            "photo_required" => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode,
                result.ErrorDetail ?? "At least one ready photo is required to publish an occurrence."),
            "media_not_ready_or_owned" or "media_persistence_conflict" or "media_attach_not_allowed" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                result.ErrorCode,
                result.ErrorDetail),
            "invalid_media_selection" => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode,
                result.ErrorDetail),
            "target_persistence_conflict" or "occurrence_persistence_conflict" => Problem(
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

    private static IResult MapTargetFailure(AddOccurrenceTargetResult result, HttpContext httpContext) =>
        result.ErrorCode switch
        {
            "occurrence_not_found" => Problem(
                httpContext,
                StatusCodes.Status404NotFound,
                result.ErrorCode,
                "The occurrence does not exist or does not belong to the authenticated user."),
            "master_not_eligible" => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode,
                result.ErrorDetail),
            "duplicate_target" or "target_limit_reached" or "target_persistence_conflict" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                result.ErrorCode,
                result.ErrorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? "invalid_target",
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
        Guid MasterUserId,
        string Title,
        string? Description,
        string Street,
        string Number,
        string Neighborhood,
        string City,
        decimal Latitude,
        decimal Longitude,
        string? PostalCode,
        Guid? CityId,
        string? StateCode,
        string ExternalProtocolNumber,
        string? ExternalProtocolAgency,
        IReadOnlyList<Guid>? MediaIds = null);

    public sealed record AddOccurrenceTargetRequest(Guid MasterUserId);
}
