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

        self.MapPost("/avatar/upload", async (
            ProfileAvatarUploadRequest request,
            IProfileService profileService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await profileService.RequestAvatarUploadAsync(
                userId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                cancellationToken);

            return result.Succeeded && result.Upload is not null
                ? Results.Ok(result.Upload)
                : MapAvatarError(result.ErrorCode, result.ErrorDetail);
        }).RequireAuthorization(AuthorizationPolicies.ProfileUpdate);

        self.MapPost("/avatar/confirm", async (
            ProfileAvatarConfirmRequest request,
            IProfileService profileService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await profileService.ConfirmAvatarUploadAsync(
                userId,
                request.AvatarMediaId,
                request.ContentType,
                cancellationToken);

            return result.Succeeded && result.Confirmation is not null
                ? Results.Ok(result.Confirmation)
                : MapAvatarError(result.ErrorCode, result.ErrorDetail);
        }).RequireAuthorization(AuthorizationPolicies.ProfileUpdate);

        self.MapGet("/avatar", async (
            IProfileService profileService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await profileService.GetAvatarAsync(userId, cancellationToken);
            return result.Succeeded && result.Avatar is not null
                ? Results.Ok(result.Avatar)
                : MapAvatarError(result.ErrorCode);
        }).RequireAuthorization(AuthorizationPolicies.ProfileRead);

        self.MapDelete("/avatar", async (
            IProfileService profileService,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await profileService.RemoveAvatarAsync(userId, cancellationToken);
            return result.Succeeded && result.Profile is not null
                ? Results.Ok(result.Profile)
                : MapAvatarError(result.ErrorCode);
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

    private static IResult MapAvatarError(string? errorCode, string? detail = null) =>
        errorCode switch
        {
            "profile_not_found" or "avatar_not_found" or "avatar_object_missing" =>
                Results.NotFound(new { error = errorCode, detail }),
            "storage_not_configured" or "storage_verification_failed" =>
                Results.Json(new { error = errorCode, detail }, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.BadRequest(new
            {
                error = errorCode ?? "invalid_avatar_request",
                detail
            })
        };

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record UpdateProfileRequest(string DisplayName, string? Document, string? Phone);
    public sealed record ProfileAvatarUploadRequest(string FileName, string ContentType, long SizeBytes);
    public sealed record ProfileAvatarConfirmRequest(Guid AvatarMediaId, string ContentType);
}
