using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Content;

namespace CidadeEmDia.Api.Endpoints;

public static class ContentEndpoints
{
    public static RouteGroupBuilder MapContentEndpoints(this RouteGroupBuilder api)
    {
        var posts = api.MapGroup("/posts");

        posts.MapGet("/placements/{placementKey}", async (
            string placementKey,
            string? cursor,
            int? limit,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPlacementAsync(
                placementKey,
                cursor,
                limit ?? 20,
                cancellationToken);

            return result.Succeeded
                ? Results.Ok(result.Page)
                : Results.BadRequest(new { error = result.ErrorCode });
        });

        posts.MapGet("/manage", async (
            int? page,
            int? pageSize,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.ListManagedAsync(
                userId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken);

            return result.Succeeded
                ? Results.Ok(result.Page)
                : MapError(result.ErrorCode);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        posts.MapPost("", async (
            CreatePostDraftRequest request,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.CreateDraftAsync(
                userId,
                new CreatePostDraftCommand(
                    request.Type,
                    request.Title,
                    request.Body,
                    request.LinkUrl,
                    request.Placements
                        .Select(x => new ContentPlacementInput(
                            x.PlacementKey,
                            x.Priority,
                            x.DisplayOrder))
                        .ToArray()),
                cancellationToken);

            return MapPostResult(result, created: true);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        posts.MapPost("/{postId:guid}/media/upload", async (
            Guid postId,
            RequestPostMediaUpload request,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.RequestMediaUploadAsync(
                userId,
                postId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                request.SortOrder,
                cancellationToken);

            return result.Succeeded
                ? Results.Ok(result.Upload)
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        posts.MapPost("/{postId:guid}/media/{mediaId:guid}/confirm", async (
            Guid postId,
            Guid mediaId,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.ConfirmMediaUploadAsync(
                userId,
                postId,
                mediaId,
                cancellationToken);

            return result.Succeeded
                ? Results.Ok(new
                {
                    media = result.Media,
                    changed = result.WasChanged
                })
                : MapError(result.ErrorCode, result.ErrorDetail);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        posts.MapPost("/{postId:guid}/publish", async (
            Guid postId,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.PublishAsync(
                userId,
                postId,
                cancellationToken: cancellationToken);

            return MapPostResult(result);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        posts.MapPost("/{postId:guid}/archive", async (
            Guid postId,
            ClaimsPrincipal principal,
            IContentService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await service.ArchiveAsync(
                userId,
                postId,
                cancellationToken: cancellationToken);

            return MapPostResult(result);
        })
        .RequireAuthorization(AuthorizationPolicies.ContentPublish);

        return api;
    }

    private static IResult MapPostResult(
        ContentPostResult result,
        bool created = false)
    {
        if (result.Succeeded)
        {
            return created
                ? Results.Created($"/api/v1/posts/{result.Post!.Id}", result.Post)
                : Results.Ok(new
                {
                    post = result.Post,
                    changed = result.WasChanged
                });
        }

        return MapError(result.ErrorCode, result.ErrorDetail);
    }

    private static IResult MapError(string? errorCode, string? detail = null) =>
        errorCode switch
        {
            "post_not_found" or "media_not_found" =>
                Results.NotFound(new { error = errorCode }),

            "post_access_denied" or "post_publish_not_allowed" =>
                Results.Forbid(),

            "subscription_not_found" or "subscription_access_denied" =>
                Results.StatusCode(StatusCodes.Status403Forbidden),

            "publication_limit_reached" =>
                Results.Conflict(new { error = errorCode }),

            "storage_not_configured" =>
                Results.Json(
                    new { error = errorCode, detail },
                    statusCode: StatusCodes.Status503ServiceUnavailable),

            "post_persistence_conflict"
                or "media_persistence_conflict"
                or "content_concurrency_conflict" =>
                Results.Conflict(new { error = errorCode }),

            _ => Results.BadRequest(new
            {
                error = errorCode ?? "content_operation_failed",
                detail
            })
        };

    private static bool TryGetUserId(
        ClaimsPrincipal principal,
        out Guid userId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);

    public sealed record CreatePostDraftRequest(
        string Type,
        string? Title,
        string? Body,
        string? LinkUrl,
        IReadOnlyCollection<PostPlacementRequest> Placements);

    public sealed record PostPlacementRequest(
        string PlacementKey,
        int Priority = 0,
        int DisplayOrder = 0);

    public sealed record RequestPostMediaUpload(
        string FileName,
        string ContentType,
        long SizeBytes,
        int SortOrder = 0);
}
