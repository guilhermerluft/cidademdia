using System.Security.Claims;
using CidadeEmDia.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CidadeEmDia.Api.Hubs;

[Authorize]
public sealed class ChatHub(IChatService chatService) : Hub
{
    public static string GroupName(Guid conversationId) => $"chat:{conversationId:N}";

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.GetAsync(
            userId,
            conversationId,
            Context.ConnectionAborted);

        if (!result.Succeeded)
            throw new HubException(result.ErrorCode ?? "chat_access_denied");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(conversationId),
            Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GroupName(conversationId),
            Context.ConnectionAborted);

    public async Task<ChatMessageItem> SendMessage(
        Guid conversationId,
        Guid clientMessageId,
        string content)
    {
        var userId = GetCurrentUserId();
        var result = await chatService.SendTextAsync(
            userId,
            conversationId,
            clientMessageId,
            content,
            Context.ConnectionAborted);

        if (!result.Succeeded || result.Message is null)
            throw new HubException(result.ErrorCode ?? "chat_message_failed");

        if (!result.WasDuplicate)
        {
            await Clients
                .Group(GroupName(conversationId))
                .SendAsync(
                    "MessageReceived",
                    result.Message,
                    Context.ConnectionAborted);
        }

        return result.Message;
    }

    private Guid GetCurrentUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId))
            throw new HubException("unauthorized");

        return userId;
    }
}
