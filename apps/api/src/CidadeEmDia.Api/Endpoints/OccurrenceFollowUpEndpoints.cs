using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceFollowUpEndpoints
{
    public static RouteGroupBuilder MapOccurrenceFollowUpEndpoints(this RouteGroupBuilder api)
    {
        var followUp = api
            .MapGroup("/occurrences/{occurrenceId:guid}")
            .RequireAuthorization();

        followUp.MapPost("/complements", async (
            Guid occurrenceId,
            AddOccurrenceComplementRequest request,
            IOccurrenceFollowUpService followUpService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await followUpService.AddComplementAsync(
                userId,
                occurrenceId,
                request.Content,
                cancellationToken);

            return result.Succeeded && result.Complement is not null
                ? Results.Created($"/api/v1/occurrences/{occurrenceId}", result.Complement)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        followUp.MapPost("/service-forecast", async (
            Guid occurrenceId,
            SetOccurrenceServiceForecastRequest request,
            IOccurrenceFollowUpService followUpService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await followUpService.SetServiceForecastAsync(
                userId,
                occurrenceId,
                request.EstimatedFor,
                request.Note,
                cancellationToken);

            return result.Succeeded && result.Forecast is not null
                ? Results.Created($"/api/v1/occurrences/{occurrenceId}", result.Forecast)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
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
                "The occurrence does not exist or is not available to the authenticated user."),
            "accepted_master_required" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                errorCode,
                errorDetail),
            "occurrence_terminal" or "occurrence_follow_up_conflict" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                errorCode,
                errorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                errorCode ?? "invalid_occurrence_follow_up_request",
                errorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence follow-up request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record AddOccurrenceComplementRequest(string Content);

    public sealed record SetOccurrenceServiceForecastRequest(
        DateTimeOffset EstimatedFor,
        string? Note);
}
