using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Administration;

namespace CidadeEmDia.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        admin.MapGet("/status", () => Results.Ok(new
        {
            access = "admin",
            utc = DateTimeOffset.UtcNow
        }));

        admin.MapGet("/overview", async (
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.GetOverviewAsync(userId, cancellationToken));
        });

        admin.MapGet("/users", async (
            string? search,
            string? status,
            string? role,
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.ListUsersAsync(
                userId,
                search,
                status,
                role,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        admin.MapPost("/users/{targetUserId:guid}/status", async (
            Guid targetUserId,
            ChangeUserStatusRequest request,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.ChangeUserStatusAsync(
                userId,
                targetUserId,
                request.Status,
                request.Reason,
                cancellationToken);

            if (!result.Succeeded)
                return MapError(result.ErrorCode, result.ErrorDetail);

            return Results.Ok(new
            {
                user = result.Data!.User,
                changed = result.Data.Changed
            });
        });

        admin.MapGet("/institutions", async (
            string? search,
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.ListInstitutionsAsync(
                userId,
                search,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        admin.MapGet("/occurrences", async (
            string? search,
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.ListOccurrencesAsync(
                userId,
                search,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        admin.MapGet("/posts", async (
            string? search,
            string? status,
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.ListPostsAsync(
                userId,
                search,
                status,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        admin.MapGet("/billing", async (
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.GetBillingAsync(
                userId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        admin.MapGet("/audit-logs", async (
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IAdminService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            return MapResult(await service.ListAuditLogsAsync(
                userId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        return api;
    }

    private static IResult MapResult<T>(AdminResult<T> result) =>
        result.Succeeded
            ? Results.Ok(result.Data)
            : MapError(result.ErrorCode, result.ErrorDetail);

    private static IResult MapError(string? errorCode, string? detail = null) =>
        errorCode switch
        {
            "admin_access_denied" => Results.Forbid(),
            "admin_user_not_found" => Results.NotFound(new { error = errorCode }),
            "admin_self_status_change_not_allowed" or "admin_target_is_admin" or "admin_persistence_conflict" =>
                Results.Conflict(new { error = errorCode, detail }),
            _ => Results.BadRequest(new
            {
                error = errorCode ?? "admin_operation_failed",
                detail
            })
        };

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record ChangeUserStatusRequest(string Status, string Reason);
}
