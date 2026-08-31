using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceSupportEndpoints
{
    public static RouteGroupBuilder MapOccurrenceSupportEndpoints(this RouteGroupBuilder api)
    {
        var support = api
            .MapGroup("/occurrences/{occurrenceId:guid}/support")
            .RequireAuthorization();

        support.MapGet("", async (
            Guid occurrenceId,
            IOccurrenceSupportService supportService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await supportService.GetAsync(
                userId,
                occurrenceId,
                cancellationToken);

            return result.Succeeded && result.Support is not null
                ? Results.Ok(result.Support)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        support.MapPost("", async (
            Guid occurrenceId,
            IOccurrenceSupportService supportService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await supportService.SupportAsync(
                userId,
                occurrenceId,
                cancellationToken);

            if (!result.Succeeded || result.Support is null)
                return MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);

            return result.WasCreated
                ? Results.Created($"/api/v1/occurrences/{occurrenceId}/support", result.Support)
                : Results.Ok(result.Support);
        });

        return api;
    }

    private static IResult MapFailure(
        string? errorCode,
        string? errorDetail,
        HttpContext httpContext) =>
        errorCode switch
        {
            "occurrence_not_found" => Problem(
                httpContext,
                StatusCodes.Status404NotFound,
                errorCode,
                "The occurrence does not exist."),
            "support_not_allowed" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                errorCode,
                "The authenticated user cannot support occurrences."),
            "occurrence_support_conflict" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                errorCode,
                errorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                errorCode ?? "invalid_support_request",
                errorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence support request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
