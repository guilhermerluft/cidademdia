using System.Security.Claims;
using CidadeEmDia.Application.Occurrences;

namespace CidadeEmDia.Api.Endpoints;

public static class OccurrenceMediaEndpoints
{
    public static RouteGroupBuilder MapOccurrenceMediaEndpoints(this RouteGroupBuilder api)
    {
        var media = api.MapGroup("/occurrence-media").RequireAuthorization();

        media.MapPost("/uploads", async (
            RequestOccurrenceMediaUpload request,
            IOccurrenceMediaService mediaService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await mediaService.RequestUploadAsync(
                userId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                cancellationToken);

            if (result.Succeeded && result.Upload is not null)
            {
                return Results.Created(
                    $"/api/v1/occurrence-media/{result.Upload.Id}",
                    result.Upload);
            }

            return MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        media.MapPost("/{mediaId:guid}/confirm", async (
            Guid mediaId,
            IOccurrenceMediaService mediaService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await mediaService.ConfirmUploadAsync(
                userId,
                mediaId,
                cancellationToken);

            return result.Succeeded && result.Media is not null
                ? Results.Ok(result.Media)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        media.MapGet("/{mediaId:guid}/read-url", async (
            Guid mediaId,
            IOccurrenceMediaService mediaService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await mediaService.GetReadUrlAsync(
                userId,
                mediaId,
                cancellationToken);

            return result.Succeeded && result.Media is not null
                ? Results.Ok(result.Media)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        api.MapGet("/occurrences/{occurrenceId:guid}/media", async (
            Guid occurrenceId,
            IOccurrenceMediaService mediaService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await mediaService.ListForOccurrenceAsync(
                userId,
                occurrenceId,
                cancellationToken);

            return result.Succeeded && result.Media is not null
                ? Results.Ok(result.Media)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        }).RequireAuthorization();

        return api;
    }

    private static IResult MapFailure(
        string? errorCode,
        string? errorDetail,
        HttpContext httpContext) =>
        errorCode switch
        {
            "media_not_found" or "occurrence_not_found" =>
                Problem(httpContext, 404, errorCode, errorDetail ?? "The requested occurrence media resource was not found."),
            "media_access_denied" or "media_upload_not_allowed" => Problem(httpContext, 403, errorCode, errorDetail),
            "storage_not_configured" => Problem(httpContext, 503, errorCode, errorDetail),
            "media_object_missing" or "media_verification_failed" or "media_persistence_conflict"
                or "storage_verification_failed" => Problem(httpContext, 409, errorCode, errorDetail),
            "media_not_ready" => Problem(httpContext, 409, errorCode, "Occurrence media is not ready."),
            _ => Problem(httpContext, 400, errorCode ?? "invalid_media_request", errorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Occurrence media request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record RequestOccurrenceMediaUpload(
        string FileName,
        string ContentType,
        long SizeBytes);
}
