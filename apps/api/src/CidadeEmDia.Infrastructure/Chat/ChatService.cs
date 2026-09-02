using System.Data;
using System.Globalization;
using CidadeEmDia.Application.Chat;
using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using CidadeEmDia.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CidadeEmDia.Infrastructure.Chat;

internal sealed class ChatService(
    AppDbContext dbContext,
    R2ObjectStorage storage)
    : IChatService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private static readonly IReadOnlyDictionary<string, AudioTypeRule> AllowedAudioTypes =
        new Dictionary<string, AudioTypeRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/webm"] = new("webm", [".webm"]),
            ["audio/ogg"] = new("ogg", [".ogg", ".oga"]),
            ["audio/mp4"] = new("m4a", [".m4a", ".mp4"]),
            ["audio/mpeg"] = new("mp3", [".mp3"]),
            ["audio/wav"] = new("wav", [".wav"])
        };

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
            requiredSendPermissionKey: null,
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
            requiredSendPermissionKey: null,
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
            requiredSendPermissionKey: null,
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
            SubaccountPermissionKeys.ChatMessageSend,
            cancellationToken);

        if (!access.Succeeded)
            return ChatSendMessageResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        var duplicate = await FindDuplicateAsync(
            conversationId,
            requesterUserId,
            clientMessageId,
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
        return await PersistMessageAsync(
            transaction,
            message,
            requesterUserId,
            cancellationToken);
    }

    public async Task<ChatAudioUploadResult> RequestAudioUploadAsync(
        Guid requesterUserId,
        Guid conversationId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty || conversationId == Guid.Empty)
            return ChatAudioUploadResult.Failure("invalid_chat_audio_request");

        if (!storage.IsConfigured)
            return ChatAudioUploadResult.Failure(
                "storage_not_configured",
                "Cloudflare R2 is not configured for this environment.");

        var validation = ValidateAudioDescriptor(fileName, contentType, sizeBytes);
        if (validation.Descriptor is null)
            return ChatAudioUploadResult.Failure(validation.ErrorCode!, validation.ErrorDetail);

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        var access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            SubaccountPermissionKeys.ChatAudioSend,
            cancellationToken);

        if (!access.Succeeded)
            return ChatAudioUploadResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        var mediaId = Guid.NewGuid();
        var objectKey = BuildAudioObjectKey(
            conversationId,
            mediaId,
            validation.Descriptor.Rule.ObjectExtension);
        var now = DateTimeOffset.UtcNow;
        var uploadUrl = storage.CreateUploadUrl(
            objectKey,
            validation.Descriptor.ContentType,
            now,
            out var expiresAt);

        return ChatAudioUploadResult.Success(
            new ChatAudioUploadItem(
                mediaId,
                validation.Descriptor.ContentType,
                validation.Descriptor.SizeBytes,
                uploadUrl,
                expiresAt));
    }

    public async Task<ChatSendMessageResult> SendAudioAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid clientMessageId,
        Guid mediaId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty
            || conversationId == Guid.Empty
            || clientMessageId == Guid.Empty
            || mediaId == Guid.Empty)
        {
            return ChatSendMessageResult.Failure("invalid_chat_audio_request");
        }

        if (!storage.IsConfigured)
            return ChatSendMessageResult.Failure("storage_not_configured");

        var validation = ValidateAudioDescriptor(fileName, contentType, sizeBytes);
        if (validation.Descriptor is null)
            return ChatSendMessageResult.Failure(validation.ErrorCode!, validation.ErrorDetail);

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        var access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            SubaccountPermissionKeys.ChatAudioSend,
            cancellationToken);

        if (!access.Succeeded)
            return ChatSendMessageResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        var existing = await FindDuplicateAsync(
            conversationId,
            requesterUserId,
            clientMessageId,
            cancellationToken);

        if (existing is not null)
        {
            var duplicateItem = ToMessageItem(existing);
            return duplicateItem.Type == "AUDIO"
                ? ChatSendMessageResult.Success(duplicateItem, wasDuplicate: true)
                : ChatSendMessageResult.Failure(
                    "chat_message_conflict",
                    "The client message id is already used by a text message.");
        }

        var objectKey = BuildAudioObjectKey(
            conversationId,
            mediaId,
            validation.Descriptor.Rule.ObjectExtension);

        R2ObjectMetadata? metadata;
        byte[]? signature;
        try
        {
            metadata = await storage.GetObjectMetadataAsync(objectKey, cancellationToken);
            signature = metadata is null
                ? null
                : await storage.ReadObjectPrefixAsync(objectKey, 32, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return ChatSendMessageResult.Failure("chat_audio_storage_verification_failed", exception.Message);
        }

        if (metadata is null || signature is null)
        {
            return ChatSendMessageResult.Failure(
                "chat_audio_object_missing",
                "The uploaded chat audio object was not found in Cloudflare R2.");
        }

        if (metadata.SizeBytes != validation.Descriptor.SizeBytes
            || !string.Equals(
                metadata.ContentType,
                validation.Descriptor.ContentType,
                StringComparison.OrdinalIgnoreCase)
            || !HasExpectedAudioSignature(validation.Descriptor.ContentType, signature))
        {
            return ChatSendMessageResult.Failure(
                "chat_audio_verification_failed",
                "The uploaded chat audio does not match the declared type or size.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        conversation = await dbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            SubaccountPermissionKeys.ChatAudioSend,
            cancellationToken);

        if (!access.Succeeded)
            return ChatSendMessageResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        existing = await FindDuplicateAsync(
            conversationId,
            requesterUserId,
            clientMessageId,
            cancellationToken);

        if (existing is not null)
        {
            var duplicateItem = ToMessageItem(existing);
            return duplicateItem.Type == "AUDIO"
                ? ChatSendMessageResult.Success(duplicateItem, wasDuplicate: true)
                : ChatSendMessageResult.Failure("chat_message_conflict");
        }

        ChatMessage message;
        try
        {
            message = ChatMessage.CreateAudio(
                conversationId,
                requesterUserId,
                clientMessageId,
                mediaId,
                validation.Descriptor.FileName,
                validation.Descriptor.ContentType,
                validation.Descriptor.SizeBytes,
                DateTimeOffset.UtcNow);
        }
        catch (DomainException exception)
        {
            return ChatSendMessageResult.Failure("invalid_chat_audio_request", exception.Message);
        }

        dbContext.ChatMessages.Add(message);
        return await PersistMessageAsync(
            transaction,
            message,
            requesterUserId,
            cancellationToken);
    }

    public async Task<ChatAudioReadUrlResult> GetAudioReadUrlAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == Guid.Empty
            || conversationId == Guid.Empty
            || messageId == Guid.Empty)
        {
            return ChatAudioReadUrlResult.Failure("invalid_chat_audio_request");
        }

        if (!storage.IsConfigured)
            return ChatAudioReadUrlResult.Failure("storage_not_configured");

        var conversation = await dbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        var access = await AuthorizeConversationAsync(
            requesterUserId,
            conversation,
            requiredSendPermissionKey: null,
            cancellationToken);

        if (!access.Succeeded)
            return ChatAudioReadUrlResult.Failure(access.ErrorCode ?? "chat_access_denied", access.ErrorDetail);

        var message = await dbContext.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == messageId && x.ConversationId == conversationId,
                cancellationToken);

        if (message is null)
            return ChatAudioReadUrlResult.Failure("chat_message_not_found");
        if (!message.TryGetAudioMetadata(out var audio))
            return ChatAudioReadUrlResult.Failure("chat_message_not_audio");
        if (!AllowedAudioTypes.TryGetValue(audio.ContentType, out var rule))
            return ChatAudioReadUrlResult.Failure("chat_audio_type_not_allowed");

        var objectKey = BuildAudioObjectKey(conversationId, audio.MediaId, rule.ObjectExtension);
        var now = DateTimeOffset.UtcNow;
        var readUrl = storage.CreateReadUrl(objectKey, now, out var expiresAt);

        return ChatAudioReadUrlResult.Success(
            new ChatAudioReadUrlItem(message.Id, audio.MediaId, readUrl, expiresAt));
    }

    private async Task<ChatSendMessageResult> PersistMessageAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ChatMessage message,
        Guid requesterUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            var persistedDuplicate = await FindDuplicateAsync(
                message.ConversationId,
                requesterUserId,
                message.ClientMessageId,
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

    private Task<ChatMessage?> FindDuplicateAsync(
        Guid conversationId,
        Guid requesterUserId,
        Guid clientMessageId,
        CancellationToken cancellationToken) =>
        dbContext.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversationId
                    && x.SenderUserId == requesterUserId
                    && x.ClientMessageId == clientMessageId,
                cancellationToken);

    private async Task<ChatConversationResult> AuthorizeConversationAsync(
        Guid requesterUserId,
        ChatConversation? conversation,
        string? requiredSendPermissionKey,
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
            var permissionKey = requiredSendPermissionKey ?? SubaccountPermissionKeys.ChatRead;

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
                requiredSendPermissionKey is not null
                    ? "The authenticated user cannot send this message type without an active assignment and permission."
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

    private (AudioDescriptor? Descriptor, string? ErrorCode, string? ErrorDetail) ValidateAudioDescriptor(
        string fileName,
        string contentType,
        long sizeBytes)
    {
        var normalizedType = NormalizeContentType(contentType);
        if (string.IsNullOrWhiteSpace(normalizedType)
            || !AllowedAudioTypes.TryGetValue(normalizedType, out var rule))
        {
            return (null, "chat_audio_type_not_allowed", "Only WebM, OGG, M4A/MP4, MP3 and WAV audio are accepted.");
        }

        var safeFileName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
            return (null, "invalid_chat_audio_request", "A valid audio file name with at most 255 characters is required.");

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!rule.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return (null, "chat_audio_extension_not_allowed", "The audio file extension does not match its content type.");

        if (sizeBytes <= 0 || sizeBytes > storage.MaxAudioBytes)
        {
            return (
                null,
                "chat_audio_size_not_allowed",
                $"Declared audio size must be between 1 and {storage.MaxAudioBytes} bytes.");
        }

        return (new AudioDescriptor(safeFileName, normalizedType, sizeBytes, rule), null, null);
    }

    private static string NormalizeContentType(string? contentType) =>
        (contentType ?? string.Empty)
            .Split(';', 2, StringSplitOptions.TrimEntries)[0]
            .Trim()
            .ToLowerInvariant();

    private static string BuildAudioObjectKey(
        Guid conversationId,
        Guid mediaId,
        string extension) =>
        $"chat/audio/{conversationId:N}/{mediaId:N}.{extension}";

    private static bool HasExpectedAudioSignature(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType switch
        {
            "audio/webm" => bytes.Length >= 4
                && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            "audio/ogg" => bytes.Length >= 4 && bytes[..4].SequenceEqual("OggS"u8),
            "audio/mp4" => bytes.Length >= 8 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
            "audio/mpeg" => bytes.Length >= 3
                && (bytes[..3].SequenceEqual("ID3"u8)
                    || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0)),
            "audio/wav" => bytes.Length >= 12
                && bytes[..4].SequenceEqual("RIFF"u8)
                && bytes.Slice(8, 4).SequenceEqual("WAVE"u8),
            _ => false
        };

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

    private static ChatMessageItem ToMessageItem(ChatMessage message)
    {
        if (message.TryGetAudioMetadata(out var audio))
        {
            return new ChatMessageItem(
                message.Id,
                message.Sequence,
                message.ConversationId,
                message.SenderUserId,
                message.ClientMessageId,
                "AUDIO",
                null,
                new ChatAudioAttachmentItem(
                    audio.MediaId,
                    audio.OriginalFileName,
                    audio.ContentType,
                    audio.SizeBytes),
                message.SentAt);
        }

        return new ChatMessageItem(
            message.Id,
            message.Sequence,
            message.ConversationId,
            message.SenderUserId,
            message.ClientMessageId,
            "TEXT",
            message.Content,
            null,
            message.SentAt);
    }

    private sealed record AudioTypeRule(
        string ObjectExtension,
        IReadOnlyList<string> AllowedExtensions);

    private sealed record AudioDescriptor(
        string FileName,
        string ContentType,
        long SizeBytes,
        AudioTypeRule Rule);
}
