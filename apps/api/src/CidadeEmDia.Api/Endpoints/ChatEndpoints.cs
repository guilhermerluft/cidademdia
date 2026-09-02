using System.Security.Claims;
using CidadeEmDia.Api.Hubs;
using CidadeEmDia.Application.Chat;
using Microsoft.AspNetCore.SignalR;

namespace CidadeEmDia.Api.Endpoints;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        var chat = api
            .MapGroup("/chat")
            .RequireAuthorization();

        chat.MapGet("/targets/{targetId:guid}/conversation", async (
            Guid targetId,
            IChatService chatService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.GetByTargetAsync(
                userId,
                targetId,
                cancellationToken);

            return result.Succeeded && result.Conversation is not null
                ? Results.Ok(result.Conversation)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        chat.MapGet("/conversations/{conversationId:guid}", async (
            Guid conversationId,
            IChatService chatService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.GetAsync(
                userId,
                conversationId,
                cancellationToken);

            return result.Succeeded && result.Conversation is not null
                ? Results.Ok(result.Conversation)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        chat.MapGet("/conversations/{conversationId:guid}/messages", async (
            Guid conversationId,
            string? cursor,
            int? pageSize,
            IChatService chatService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.GetMessagesAsync(
                userId,
                conversationId,
                cursor,
                pageSize ?? 50,
                cancellationToken);

            return result.Succeeded && result.Page is not null
                ? Results.Ok(result.Page)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        chat.MapPost("/conversations/{conversationId:guid}/messages", async (
            Guid conversationId,
            SendChatMessageRequest request,
            IChatService chatService,
            IHubContext<ChatHub> hubContext,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.SendTextAsync(
                userId,
                conversationId,
                request.ClientMessageId,
                request.Content,
                cancellationToken);

            return await MapSendResultAsync(
                conversationId,
                result,
                hubContext,
                httpContext,
                cancellationToken);
        });

        chat.MapPost("/conversations/{conversationId:guid}/audio/uploads", async (
            Guid conversationId,
            RequestChatAudioUpload request,
            IChatService chatService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.RequestAudioUploadAsync(
                userId,
                conversationId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                cancellationToken);

            return result.Succeeded && result.Upload is not null
                ? Results.Created(
                    $"/api/v1/chat/conversations/{conversationId}/audio/uploads/{result.Upload.MediaId}",
                    result.Upload)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        chat.MapPost("/conversations/{conversationId:guid}/audio/{mediaId:guid}/confirm", async (
            Guid conversationId,
            Guid mediaId,
            ConfirmChatAudioRequest request,
            IChatService chatService,
            IHubContext<ChatHub> hubContext,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.SendAudioAsync(
                userId,
                conversationId,
                request.ClientMessageId,
                mediaId,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                cancellationToken);

            return await MapSendResultAsync(
                conversationId,
                result,
                hubContext,
                httpContext,
                cancellationToken);
        });

        chat.MapGet("/conversations/{conversationId:guid}/messages/{messageId:guid}/audio/read-url", async (
            Guid conversationId,
            Guid messageId,
            IChatService chatService,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetCurrentUserId(principal, out var userId))
                return Results.Unauthorized();

            var result = await chatService.GetAudioReadUrlAsync(
                userId,
                conversationId,
                messageId,
                cancellationToken);

            return result.Succeeded && result.Audio is not null
                ? Results.Ok(result.Audio)
                : MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);
        });

        return api;
    }

    private static async Task<IResult> MapSendResultAsync(
        Guid conversationId,
        ChatSendMessageResult result,
        IHubContext<ChatHub> hubContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded || result.Message is null)
            return MapFailure(result.ErrorCode, result.ErrorDetail, httpContext);

        if (!result.WasDuplicate)
        {
            await hubContext.Clients
                .Group(ChatHub.GroupName(conversationId))
                .SendAsync(
                    "MessageReceived",
                    result.Message,
                    cancellationToken);
        }

        return result.WasDuplicate
            ? Results.Ok(result.Message)
            : Results.Created(
                $"/api/v1/chat/conversations/{conversationId}/messages?cursor={result.Message.Sequence}",
                result.Message);
    }

    private static IResult MapFailure(
        string? errorCode,
        string? errorDetail,
        HttpContext httpContext) =>
        errorCode switch
        {
            "conversation_not_found" or "chat_message_not_found" => Problem(
                httpContext,
                StatusCodes.Status404NotFound,
                errorCode ?? "conversation_not_found",
                errorDetail ?? "The requested chat resource does not exist."),
            "chat_access_denied" => Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                errorCode,
                errorDetail),
            "conversation_closed" => Problem(
                httpContext,
                StatusCodes.Status410Gone,
                errorCode,
                errorDetail),
            "chat_message_conflict" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                errorCode,
                errorDetail),
            "storage_not_configured" => Problem(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                errorCode,
                errorDetail),
            "chat_audio_object_missing" or "chat_audio_verification_failed" or "chat_audio_storage_verification_failed" => Problem(
                httpContext,
                StatusCodes.Status409Conflict,
                errorCode,
                errorDetail),
            _ => Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                errorCode ?? "invalid_chat_request",
                errorDetail)
        };

    private static IResult Problem(
        HttpContext httpContext,
        int statusCode,
        string code,
        string? detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: "Chat request could not be processed.",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });

    private static bool TryGetCurrentUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public sealed record SendChatMessageRequest(Guid ClientMessageId, string Content);

    public sealed record RequestChatAudioUpload(
        string FileName,
        string ContentType,
        long SizeBytes);

    public sealed record ConfirmChatAudioRequest(
        Guid ClientMessageId,
        string FileName,
        string ContentType,
        long SizeBytes);
}
