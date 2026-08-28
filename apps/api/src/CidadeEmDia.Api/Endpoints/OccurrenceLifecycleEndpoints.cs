using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceLifecycleEndpoints
{
    public static RouteGroupBuilder MapOccurrenceLifecycleEndpoints(this RouteGroupBuilder api)
    {
        var lifecycle = api
            .MapGroup("/occurrences/{occurrenceId:guid}")
            .RequireAuthorization();

        lifecycle.MapPost("/status", async (
            Guid occurrenceId,
            ChangeOccurrenceStatusRequest request,
            IOccurrenceLifecycleService lifecycleService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await lifecycleService.ChangeStatusAsync(
                userId,
                occurrenceId,
                request.Status,
                request.Reason,
                cancellationToken);

            return result.Succeeded && result.Occurrence is not null
                ? Results.Ok(result.Occurrence)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        lifecycle.MapPost("/cancel", async (
            Guid occurrenceId,
            CancelOccurrenceRequest request,
            IOccurrenceLifecycleService lifecycleService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await lifecycleService.CancelAsync(
                userId,
                occurrenceId,
                request.Reason,
                cancellationToken);

            return result.Succeeded && result.Occurrence is not null
                ? Results.Ok(result.Occurrence)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        lifecycle.MapDelete("", async (
            Guid occurrenceId,
            IOccurrenceLifecycleService lifecycleService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await lifecycleService.DeleteAsync(
                userId,
                occurrenceId,
                cancellationToken);

            return result.Succeeded
                ? Results.NoContent()
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
            "admin_required" or "accepted_master_required" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                errorCode,
                errorDetail),
            "occurrence_already_assigned"
                or "cancellation_not_allowed"
                or "status_transition_not_allowed"
                or "occurrence_lifecycle_conflict"
                or "occurrence_delete_conflict" => Problem(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    errorCode,
                    errorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                errorCode ?? "invalid_occurrence_lifecycle_request",
                errorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence lifecycle request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record ChangeOccurrenceStatusRequest(string Status, string? Reason);
    public sealed record CancelOccurrenceRequest(string? Reason);
}
