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
        });

        return api;
    }

    private static IResult MapFailure(
        string? errorCode,
        string? errorDetail,
        HttpContext httpContext) =>
        errorCode switch
        {
            "conversation_not_found" => Problem(
                httpContext,
                StatusCodes.Status404NotFound,
                errorCode,
                "The conversation does not exist."),
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
}
