using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceAssignmentEndpoints
{
    public static RouteGroupBuilder MapOccurrenceAssignmentEndpoints(this RouteGroupBuilder api)
    {
        var master = api.MapGroup("/master/occurrence-targets").RequireAuthorization();

        master.MapGet("", async (
            IOccurrenceAssignmentService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var items = await service.ListMasterTargetsAsync(userId, cancellationToken);
            return items is null ? Results.Forbid() : Results.Ok(items);
        });

        master.MapPut("/{targetId:guid}/assignment", async (
            Guid targetId,
            AssignOccurrenceTargetRequest request,
            IOccurrenceAssignmentService service,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.AssignAsync(
                userId,
                targetId,
                request.MasterSubaccountId,
                cancellationToken);

            return result.Succeeded && result.Assignment is not null
                ? Results.Ok(result.Assignment)
                : MapFailure(result, httpContext);
        });

        master.MapDelete("/{targetId:guid}/assignment", async (
            Guid targetId,
            IOccurrenceAssignmentService service,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.UnassignAsync(userId, targetId, cancellationToken);
            return result.Succeeded ? Results.NoContent() : MapFailure(result, httpContext);
        });

        api.MapGet("/subaccount/occurrence-assignments", async (
            IOccurrenceAssignmentService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var items = await service.ListAssignedAsync(userId, cancellationToken);
            return items is null ? Results.Forbid() : Results.Ok(items);
        }).RequireAuthorization();

        return api;
    }

    private static IResult MapFailure(OccurrenceAssignmentResult result, HttpContext httpContext) =>
        result.ErrorCode switch
        {
            "master_required" => Problem(httpContext, 403, result.ErrorCode, result.ErrorDetail),
            "target_not_found" => Problem(httpContext, 404, result.ErrorCode, result.ErrorDetail),
            "subaccount_link_not_found" => Problem(httpContext, 400, result.ErrorCode, result.ErrorDetail),
            "target_not_accepted" or "assignment_conflict" => Problem(httpContext, 409, result.ErrorCode, result.ErrorDetail),
            _ => Problem(httpContext, 400, result.ErrorCode ?? "invalid_assignment", result.ErrorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence assignment request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record AssignOccurrenceTargetRequest(Guid MasterSubaccountId);
}
