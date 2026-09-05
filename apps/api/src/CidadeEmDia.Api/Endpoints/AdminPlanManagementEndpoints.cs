using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Administration;

namespace CidadeEmDia.Api.Endpoints;

public static class AdminPlanManagementEndpoints
{
    public static RouteGroupBuilder MapAdminPlanManagementEndpoints(this RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/admin/plans")
            .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        plans.MapPut("/{planVersionId:guid}", async (
            Guid planVersionId,
            UpdateAdminPlanRequest request,
            ClaimsPrincipal principal,
            IAdminPlanManagementService service,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();

            var result = await service.UpdatePlanAsync(
                userId,
                planVersionId,
                new AdminPlanUpdateCommand(
                    request.PriceCents,
                    request.SignupFeeCents,
                    request.SubaccountLimit,
                    request.MonthlyPublicationLimit,
                    request.Reason),
                cancellationToken);

            if (result.Succeeded)
                return Results.Ok(result.Data);

            return result.ErrorCode switch
            {
                "admin_access_denied" => Results.Forbid(),
                "admin_plan_version_not_found" => Results.NotFound(new { error = result.ErrorCode }),
                "admin_plan_version_not_current" or "admin_plan_persistence_conflict" =>
                    Results.Conflict(new { error = result.ErrorCode, detail = result.ErrorDetail }),
                _ => Results.BadRequest(new { error = result.ErrorCode, detail = result.ErrorDetail })
            };
        });

        return api;
    }

    public sealed record UpdateAdminPlanRequest(
        long PriceCents,
        long SignupFeeCents,
        int SubaccountLimit,
        int MonthlyPublicationLimit,
        string Reason);
}
