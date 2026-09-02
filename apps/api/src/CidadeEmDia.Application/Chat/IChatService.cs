namespace CidadeEmDia.Application.Chat;

public interface IChatService
{
    Task<ChatConversationResult> GetByTargetAsync(
        Guid requesterUserId,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<ChatConversationResult> GetAsync(
        Guid requesterUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ChatMessagePageResult> GetMessagesAsync(
        Guid requesterUserId,
        Guid conversationId,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ChatSendMessageResult> SendTextAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid clientMessageId,
        string content,
        CancellationToken cancellationToken = default);

    Task<ChatAudioUploadResult> RequestAudioUploadAsync(
        Guid requesterUserId,
        Guid conversationId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<ChatSendMessageResult> SendAudioAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid clientMessageId,
        Guid mediaId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<ChatAudioReadUrlResult> GetAudioReadUrlAsync(
        Guid requesterUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default);
}

public sealed record ChatConversationItem(
    Guid Id,
    Guid OccurrenceId,
    Guid OccurrenceTargetId,
    Guid CitizenUserId,
    Guid MasterUserId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

public sealed record ChatConversationResult(
    bool Succeeded,
    ChatConversationItem? Conversation,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static ChatConversationResult Success(ChatConversationItem conversation) =>
        new(true, conversation, null, null);

    public static ChatConversationResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ChatAudioAttachmentItem(
    Guid MediaId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

public sealed record ChatMessageItem(
    Guid Id,
    long Sequence,
    Guid ConversationId,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Type,
    string? Content,
    ChatAudioAttachmentItem? Audio,
    DateTimeOffset SentAt);

public sealed record ChatMessagePage(
    IReadOnlyList<ChatMessageItem> Items,
    string? NextCursor,
    bool HasMore);

public sealed record ChatMessagePageResult(
    bool Succeeded,
    ChatMessagePage? Page,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static ChatMessagePageResult Success(ChatMessagePage page) =>
        new(true, page, null, null);

    public static ChatMessagePageResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ChatSendMessageResult(
    bool Succeeded,
    ChatMessageItem? Message,
    bool WasDuplicate,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static ChatSendMessageResult Success(ChatMessageItem message, bool wasDuplicate = false) =>
        new(true, message, wasDuplicate, null, null);

    public static ChatSendMessageResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, false, errorCode, errorDetail);
}

public sealed record ChatAudioUploadItem(
    Guid MediaId,
    string ContentType,
    long SizeBytes,
    Uri UploadUrl,
    DateTimeOffset UploadUrlExpiresAt);

public sealed record ChatAudioUploadResult(
    bool Succeeded,
    ChatAudioUploadItem? Upload,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static ChatAudioUploadResult Success(ChatAudioUploadItem upload) =>
        new(true, upload, null, null);

    public static ChatAudioUploadResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}

public sealed record ChatAudioReadUrlItem(
    Guid MessageId,
    Guid MediaId,
    Uri ReadUrl,
    DateTimeOffset ReadUrlExpiresAt);

public sealed record ChatAudioReadUrlResult(
    bool Succeeded,
    ChatAudioReadUrlItem? Audio,
    string? ErrorCode,
    string? ErrorDetail)
{
    public static ChatAudioReadUrlResult Success(ChatAudioReadUrlItem audio) =>
        new(true, audio, null, null);

    public static ChatAudioReadUrlResult Failure(string errorCode, string? errorDetail = null) =>
        new(false, null, errorCode, errorDetail);
}
