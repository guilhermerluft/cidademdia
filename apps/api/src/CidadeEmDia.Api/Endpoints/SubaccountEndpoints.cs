using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Subaccounts;

namespace CidadeEmDia.Api.Endpoints;

public static class SubaccountEndpoints
{
    public static RouteGroupBuilder MapSubaccountEndpoints(this RouteGroupBuilder api)
    {
        var master = api.MapGroup("/master/subaccounts")
            .RequireAuthorization(AuthorizationPolicies.MasterScope);

        master.MapGet("", async (
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var masterUserId))
                return Results.Unauthorized();

            return Results.Ok(await service.ListAsync(masterUserId, cancellationToken));
        });

        master.MapPost("", async (
            CreateSubaccountRequest request,
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var masterUserId))
                return Results.Unauthorized();

            var result = await service.AddAsync(
                masterUserId,
                request.Email,
                request.Permissions ?? Array.Empty<string>(),
                cancellationToken);

            return MapResult(result, created: true);
        });

        master.MapPost("/invitations", async (
            CreateSubaccountRequest request,
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var masterUserId))
                return Results.Unauthorized();

            var result = await service.InviteAsync(
                masterUserId,
                request.Email,
                request.Permissions ?? Array.Empty<string>(),
                cancellationToken);

            return MapInvitationResult(result);
        });

        master.MapPut("/{linkId:guid}/permissions", async (
            Guid linkId,
            UpdateSubaccountPermissionsRequest request,
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var masterUserId))
                return Results.Unauthorized();

            var result = await service.UpdatePermissionsAsync(
                masterUserId,
                linkId,
                request.Permissions ?? Array.Empty<string>(),
                cancellationToken);

            return MapResult(result);
        });

        master.MapDelete("/{linkId:guid}", async (
            Guid linkId,
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var masterUserId))
                return Results.Unauthorized();

            var result = await service.RevokeAsync(masterUserId, linkId, cancellationToken);
            return result.Succeeded ? Results.NoContent() : MapResult(result);
        });

        var invitations = api.MapGroup("/subaccount-invitations");

        invitations.MapGet("/preview", async (
            string token,
            IMasterSubaccountService service,
            CancellationToken cancellationToken) =>
        {
            var preview = await service.PreviewInvitationAsync(token, cancellationToken);
            return preview is null
                ? Results.NotFound(new { error = "invalid_or_expired_invitation" })
                : Results.Ok(preview);
        });

        invitations.MapPost("/accept", async (
            AcceptSubaccountInvitationRequest request,
            IMasterSubaccountService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AcceptInvitationAsync(
                request.Token,
                request.Password,
                request.DisplayName,
                cancellationToken);

            if (result.Succeeded)
                return Results.NoContent();

            return result.ErrorCode switch
            {
                "invalid_input" or "invalid_permissions" => Results.BadRequest(new { error = result.ErrorCode }),
                "invalid_or_expired_invitation" => Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status410Gone),
                "email_already_registered" or "subaccount_limit_reached" => Results.Conflict(new { error = result.ErrorCode }),
                "master_unavailable" => Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status403Forbidden),
                "subaccount_limit_unavailable" or "identity_catalog_unavailable" =>
                    Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status503ServiceUnavailable),
                _ => Results.BadRequest(new { error = "invitation_accept_failed" })
            };
        });

        var subaccount = api.MapGroup("/subaccount")
            .RequireAuthorization(AuthorizationPolicies.SubaccountRole);

        subaccount.MapGet("/masters/{masterUserId:guid}/context", async (
            Guid masterUserId,
            IMasterSubaccountService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var subaccountUserId))
                return Results.Unauthorized();

            var context = await service.GetContextAsync(subaccountUserId, masterUserId, cancellationToken);
            return context is null ? Results.Forbid() : Results.Ok(context);
        });

        return api;
    }

    private static IResult MapResult(MasterSubaccountResult result, bool created = false)
    {
        if (result.Succeeded && result.Member is not null)
            return created
                ? Results.Created($"/api/v1/master/subaccounts/{result.Member.LinkId}", result.Member)
                : Results.Ok(result.Member);

        return result.ErrorCode switch
        {
            "invalid_input" or "invalid_permissions" or "cannot_link_self" =>
                Results.BadRequest(new { error = result.ErrorCode }),
            "master_required" => Results.Forbid(),
            "subaccount_user_not_found" or "subaccount_not_found" =>
                Results.NotFound(new { error = result.ErrorCode }),
            "subaccount_already_linked" or "subaccount_limit_reached" or "subaccount_revoked" or "incompatible_account_role" =>
                Results.Conflict(new { error = result.ErrorCode }),
            "subaccount_user_unavailable" =>
                Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status403Forbidden),
            "subaccount_limit_unavailable" or "identity_catalog_unavailable" =>
                Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(new { error = "subaccount_operation_failed" })
        };
    }

    private static IResult MapInvitationResult(SubaccountInvitationResult result)
    {
        if (result.Succeeded && result.Invitation is not null)
            return Results.Accepted($"/api/v1/master/subaccounts/invitations/{result.Invitation.InvitationId}", result.Invitation);

        return result.ErrorCode switch
        {
            "invalid_input" or "invalid_permissions" => Results.BadRequest(new { error = result.ErrorCode }),
            "master_required" => Results.Forbid(),
            "subaccount_user_already_registered" or "subaccount_limit_reached" => Results.Conflict(new { error = result.ErrorCode }),
            "subaccount_limit_unavailable" or "identity_catalog_unavailable" or "invitation_delivery_failed" =>
                Results.Json(new { error = result.ErrorCode }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(new { error = "subaccount_invitation_failed" })
        };
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record CreateSubaccountRequest(string Email, IReadOnlyCollection<string>? Permissions);
    public sealed record UpdateSubaccountPermissionsRequest(IReadOnlyCollection<string>? Permissions);
    public sealed record AcceptSubaccountInvitationRequest(string Token, string Password, string DisplayName);
}
