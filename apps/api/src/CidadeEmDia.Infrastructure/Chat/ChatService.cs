using System.Data;
using System.Globalization;
using CidadeEmDia.Application.Chat;
using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Chat;

internal sealed class ChatService(AppDbContext dbContext) : IChatService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    public async Task<ChatConversationResult> GetByTargetAsync(
        Guid requesterUserId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || targetId == Guid.Empty)
            return ChatConversationResult.Failure("invalid_chat_request");

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OccurrenceTargetId == targetId, cancellationToken);

        return await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            requireSendPermission: false,
            cancellationToken);
    }

    public async Task<ChatConversationResult> GetAsync(
        Guid requesterUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || conversationId == Guid.Empty)
            return ChatConversationResult.Failure("invalid_chat_request");

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        return await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            requireSendPermission: false,
            cancellationToken);
    }

    public async Task<ChatMessagePageResult> GetMessagesAsync(
        Guid requesterUserId,
        Guid conversationId,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || conversationId == Guid.Empty)
            return ChatMessagePageResult.Failure("invalid_chat_request");

        if (!TryParseCursor(cursor, out var afterSequence))
            return ChatMessagePageResult.Failure("invalid_cursor", "The chat cursor is invalid.");

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        var access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            requireSendPermission: false,
            cancellationToken);

        if (!access.Succeeded || access.Conversation is null)
            return ChatMessagePageResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Clamp(pageSize, 1, MaxPageSize);

        var rows = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.Sequence > afterSequence)
            .OrderBy(x => x.Sequence)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var pageRows = hasMore ? rows.Take(pageSize).ToArray() : rows.ToArray();
        var items = pageRows.Select(ToMessageItem).ToArray();
        var nextCursor = pageRows.Length == 0
            ? cursor
            : pageRows[^1].Sequence.ToString(CultureInfo.InvariantCulture);

        return ChatMessagePageResult.Success(new ChatMessagePage(items, nextCursor, hasMore));
    }

    public async Task<ChatSendMessageResult> SendTextAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid clientMessageId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty
            || conversationId == Guid.Empty
            || clientMessageId == Guid.Empty)
        {
            return ChatSendMessageResult.Failure("invalid_message");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var conversation = await dbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        var access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            requireSendPermission: true,
            cancellationToken);

        if (!access.Succeeded)
            return ChatSendMessageResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        var duplicate = await dbContext.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversationId
                    && x.SenderUserId == requesterUserId
                    && x.ClientMessageId == clientMessageId,
                cancellationToken);

        if (duplicate is not null)
            return ChatSendMessageResult.Success(ToMessageItem(duplicate), wasDuplicate: true);

        ChatMessage message;
        try
        {
            message = new ChatMessage(
                conversationId,
                requesterUserId,
                clientMessageId,
                content,
                DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return ChatSendMessageResult.Failure("invalid_message", exception.Message);
        }

        dbContext.ChatMessages.Add(message);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            var persistedDuplicate = await dbContext.ChatMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ConversationId == conversationId
                        && x.SenderUserId == requesterUserId
                        && x.ClientMessageId == clientMessageId,
                    cancellationToken);

            if (persistedDuplicate is not null)
                return ChatSendMessageResult.Success(ToMessageItem(persistedDuplicate), wasDuplicate: true);

            return ChatSendMessageResult.Failure(
                "chat_message_conflict",
                "The chat message could not be persisted.");
        }
        catch (PostgresException exception) when (exception.SqlState == "40001")
        {
            return ChatSendMessageResult.Failure(
                "chat_message_conflict",
                "The chat conversation changed concurrently. Retry the operation.");
        }

        return ChatSendMessageResult.Success(ToMessageItem(message));
    }

    private async Task<ChatConversationResult> AuthorizeConversationAsync(
        Guid requesterUserId,
        ChatConversation? conversation,
        bool requireSendPermission,
        CancellationToken cancellationToken)
    {
        if (conversation is null)
            return ChatConversationResult.Failure("conversation_not_found");

        var userIsActive = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == requesterUserId && x.Status == UserStatus.Active,
                cancellationToken);

        if (!userIsActive)
            return ChatConversationResult.Failure("chat_access_denied");

        var directParticipant = false;

        if (conversation.CitizenUserId == requesterUserId)
        {
            directParticipant = true;
        }
        else if (conversation.MasterUserId == requesterUserId)
        {
            directParticipant = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == requesterUserId
                        && x.Roles.Any(role => role.Role.Key == IdentityRoleKeys.Master),
                    cancellationToken);
        }

        var subaccountAuthorized = false;
        if (!directParticipant)
        {
            var permissionKey = requireSendPermission
                ? SubaccountPermissionKeys.ChatMessageSend
                : SubaccountPermissionKeys.ChatRead;

            subaccountAuthorized = await dbContext.OccurrenceTargetAssignments
                .AsNoTracking()
                .AnyAsync(
                    assignment => assignment.OccurrenceTargetId == conversation.OccurrenceTargetId
                        && assignment.MasterSubaccount.MasterUserId == conversation.MasterUserId
                        && assignment.MasterSubaccount.SubaccountUserId == requesterUserId
                        && assignment.MasterSubaccount.Status == MasterSubaccountStatus.Active
                        && assignment.MasterSubaccount.SubaccountUser.Status == UserStatus.Active
                        && assignment.MasterSubaccount.Permissions.Any(permission =>
                            permission.Permission.Key == permissionKey),
                    cancellationToken);
        }

        if (!directParticipant && !subaccountAuthorized)
        {
            return ChatConversationResult.Failure(
                "chat_access_denied",
                requireSendPermission
                    ? "The authenticated user cannot send messages in this conversation without an active assignment and permission."
                    : "The authenticated user cannot access this conversation without an active assignment and permission.");
        }

        if (!conversation.IsActive)
        {
            return ChatConversationResult.Failure(
                "conversation_closed",
                "This conversation is closed and its normal participant history is no longer available.");
        }

        return ChatConversationResult.Success(ToConversationItem(conversation));
    }

    private static bool TryParseCursor(string? cursor, out long sequence)
    {
        sequence = 0;
        return string.IsNullOrWhiteSpace(cursor)
            || (long.TryParse(
                    cursor,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out sequence)
                && sequence >= 0);
    }

    private static ChatConversationItem ToConversationItem(ChatConversation conversation) =>
        new(
            conversation.Id,
            conversation.OccurrenceId,
            conversation.OccurrenceTargetId,
            conversation.CitizenUserId,
            conversation.MasterUserId,
            conversation.Status.ToString().ToUpperInvariant(),
            conversation.CreatedAt,
            conversation.ClosedAt);

    private static ChatMessageItem ToMessageItem(ChatMessage message) =>
        new(
            message.Id,
            message.Sequence,
            message.ConversationId,
            message.SenderUserId,
            message.ClientMessageId,
            message.Content,
            message.SentAt);
}
