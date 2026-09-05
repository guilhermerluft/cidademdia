using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Institutions;

namespace CidadeEmDia.Api.Endpoints;

public static class InstitutionEndpoints
{
    public static RouteGroupBuilder MapInstitutionEndpoints(this RouteGroupBuilder api)
    {
        var institutions = api.MapGroup("/institutions");

        institutions.MapGet("", async (
            string? search,
            string? type,
            string? stateCode,
            int? page,
            int? pageSize,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(
                search,
                type,
                stateCode,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken);

            return Results.Ok(result);
        });

        api.MapGet("/masters", async (
            string? search,
            int? page,
            int? pageSize,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListActiveMastersAsync(
                search,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken);

            return Results.Ok(result);
        });

        institutions.MapGet("/{institutionId:guid}", async (
            Guid institutionId,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(institutionId, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Institution)
                : MapError(result.ErrorCode, result.ErrorDetail);
        });

        institutions.MapPost("", async (
            CreateInstitutionRequest request,
            ClaimsPrincipal principal,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.CreateAsync(
                userId,
                new CreateInstitutionCommand(
                    request.Name,
                    request.Slug,
                    request.Type,
                    request.ScopeLevel,
                    request.Cnpj,
                    request.OfficialEmail,
                    request.OfficialDomain,
                    request.Description,
                    request.CityId,
                    request.StateCode,
                    (request.Jurisdictions ?? [])
                        .Select(x => new InstitutionJurisdictionInput(
                            x.JurisdictionType,
                            x.CityId,
                            x.StateCode,
                            x.CustomAreaLabel))
                        .ToArray()),
                cancellationToken);

            return result.Succeeded
                ? Results.Created($"/api/v1/institutions/{result.Institution!.Id}", result.Institution)
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        institutions.MapPost("/{institutionId:guid}/representatives", async (
            Guid institutionId,
            CreateRepresentativeRequest request,
            ClaimsPrincipal principal,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.CreateRepresentativeAsync(
                userId,
                institutionId,
                new CreateRepresentativeCommand(
                    request.Name,
                    request.Slug,
                    request.PublicRole,
                    request.OfficialEmail,
                    request.PhotoMediaId,
                    request.MandateStart,
                    request.MandateEnd,
                    request.DisplayOrder),
                cancellationToken);

            return result.Succeeded
                ? Results.Created(
                    $"/api/v1/institutions/{institutionId}/representatives/{result.Representative!.Id}",
                    result.Representative)
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        institutions.MapPost("/{institutionId:guid}/invites", async (
            Guid institutionId,
            CreateInstitutionInviteRequest request,
            ClaimsPrincipal principal,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.CreateInviteAsync(
                userId,
                institutionId,
                new CreateInstitutionInviteCommand(
                    request.RepresentativeId,
                    request.ExpectedEmail,
                    request.ExpiresInHours ?? 72),
                cancellationToken);

            return result.Succeeded
                ? Results.Created(
                    $"/api/v1/institutions/{institutionId}/invites/{result.Invite!.Id}",
                    result.Invite)
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        api.MapPost("/institution-invites/claim", async (
            ClaimInstitutionInviteRequest request,
            ClaimsPrincipal principal,
            IInstitutionService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.ClaimInviteAsync(
                userId,
                request.Token,
                cancellationToken: cancellationToken);

            return result.Succeeded
                ? Results.Ok(new
                {
                    membership = result.Membership,
                    representative = result.Representative,
                    changed = result.WasChanged
                })
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization();

        return api;
    }

    private static IResult MapError(string? errorCode, string? detail = null) =>
        errorCode switch
        {
            "institution_not_found" or "representative_not_found" or "invite_not_found" =>
                Results.NotFound(new { error = errorCode }),

            "institution_admin_required" =>
                Results.Forbid(),

            "invite_email_mismatch"
                or "invite_email_not_verified"
                or "invite_user_not_active" =>
                Results.StatusCode(StatusCodes.Status403Forbidden),

            "institution_persistence_conflict"
                or "representative_persistence_conflict"
                or "invite_persistence_conflict"
                or "invite_claim_conflict"
                or "representative_invite_pending"
                or "representative_already_claimed"
                or "invite_already_used" =>
                Results.Conflict(new { error = errorCode, detail }),

            "invite_revoked" or "invite_expired" or "invite_not_usable" =>
                Results.BadRequest(new { error = errorCode, detail }),

            _ => Results.BadRequest(new
            {
                error = errorCode ?? "institution_operation_failed",
                detail
            })
        };

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);

    public sealed record InstitutionJurisdictionRequest(
        string JurisdictionType,
        Guid? CityId,
        string? StateCode,
        string? CustomAreaLabel);

    public sealed record CreateInstitutionRequest(
        string Name,
        string Slug,
        string Type,
        string ScopeLevel,
        string? Cnpj,
        string? OfficialEmail,
        string? OfficialDomain,
        string? Description,
        Guid? CityId,
        string? StateCode,
        IReadOnlyCollection<InstitutionJurisdictionRequest>? Jurisdictions);

    public sealed record CreateRepresentativeRequest(
        string Name,
        string Slug,
        string PublicRole,
        string? OfficialEmail,
        Guid? PhotoMediaId,
        DateOnly? MandateStart,
        DateOnly? MandateEnd,
        int DisplayOrder = 0);

    public sealed record CreateInstitutionInviteRequest(
        Guid? RepresentativeId,
        string? ExpectedEmail,
        int? ExpiresInHours);

    public sealed record ClaimInstitutionInviteRequest(string Token);
}
