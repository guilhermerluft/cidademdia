using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceTargetDecisionEndpoints
{
    public static RouteGroupBuilder MapOccurrenceTargetDecisionEndpoints(this RouteGroupBuilder api)
    {
        var targets = api
            .MapGroup("/occurrences/{occurrenceId:guid}/targets/{targetId:guid}")
            .RequireAuthorization();

        targets.MapGet("", async (
            Guid occurrenceId,
            Guid targetId,
            IOccurrenceTargetDecisionService decisionService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var decision = await decisionService.GetAsync(
                userId,
                occurrenceId,
                targetId,
                cancellationToken);

            return decision is null ? Results.NotFound() : Results.Ok(decision);
        });

        targets.MapPost("/accept", async (
            Guid occurrenceId,
            Guid targetId,
            IOccurrenceTargetDecisionService decisionService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await decisionService.AcceptAsync(
                userId,
                occurrenceId,
                targetId,
                cancellationToken);

            return result.Succeeded && result.Decision is not null
                ? Results.Ok(result.Decision)
                : MapFailure(result, httpContext);
        });

        targets.MapPost("/reject", async (
            Guid occurrenceId,
            Guid targetId,
            RejectOccurrenceTargetRequest request,
            IOccurrenceTargetDecisionService decisionService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await decisionService.RejectAsync(
                userId,
                occurrenceId,
                targetId,
                request.Reason,
                cancellationToken);

            return result.Succeeded && result.Decision is not null
                ? Results.Ok(result.Decision)
                : MapFailure(result, httpContext);
        });

        return api;
    }

    private static IResult MapFailure(
        OccurrenceTargetDecisionResult result,
        HttpContext httpContext) =>
        result.ErrorCode switch
        {
            "master_not_eligible" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                result.ErrorCode,
                result.ErrorDetail),
            "target_not_found" => Problem(
                httpContext,
                StatusCodes.Status404NotFound,
                result.ErrorCode,
                "The target does not exist or is not assigned to the authenticated Master."),
            "target_already_decided" or "target_decision_conflict" or "occurrence_terminal" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                result.ErrorCode,
                result.ErrorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? "invalid_target_decision",
                result.ErrorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence target decision could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record RejectOccurrenceTargetRequest(string Reason);
}
