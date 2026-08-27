using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Profiles;
using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Api.Endpoints;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder api)
    {
        var self = api.MapGroup("/profile").RequireAuthorization();

        self.MapGet("", async (IProfileService profileService, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var profile = await profileService.GetPrivateAsync(userId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).RequireAuthorization(AuthorizationPolicies.ProfileRead);

        self.MapPut("", async (UpdateProfileRequest request, IProfileService profileService, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await profileService.UpdateAsync(
                userId,
                request.DisplayName,
                request.Document,
                request.Phone,
                cancellationToken);

            return MapUpdateResult(result);
        }).RequireAuthorization(AuthorizationPolicies.ProfileUpdate);

        var profiles = api.MapGroup("/profiles").RequireAuthorization();

        profiles.MapGet("/{userId:guid}", async (Guid userId, IProfileService profileService, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var currentUserId))
                return Results.Unauthorized();

            if (currentUserId != userId && !principal.IsInRole(IdentityRoleKeys.Admin))
                return Results.Forbid();

            var profile = await profileService.GetPrivateAsync(userId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).RequireAuthorization(AuthorizationPolicies.ProfileRead);

        profiles.MapGet("/{userId:guid}/public", async (Guid userId, IProfileService profileService, CancellationToken cancellationToken) =>
        {
            var profile = await profileService.GetPublicAsync(userId, cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        return api;
    }

    private static IResult MapUpdateResult(ProfileUpdateResult result)
    {
        if (result.Succeeded && result.Profile is not null)
            return Results.Ok(result.Profile);

        return result.ErrorCode switch
        {
            "profile_not_found" => Results.NotFound(),
            "invalid_document" => Results.BadRequest(new { error = "invalid_document" }),
            "invalid_phone" => Results.BadRequest(new { error = "invalid_phone" }),
            _ => Results.BadRequest(new { error = "invalid_input" })
        };
    }

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record UpdateProfileRequest(string DisplayName, string? Document, string? Phone);
}
