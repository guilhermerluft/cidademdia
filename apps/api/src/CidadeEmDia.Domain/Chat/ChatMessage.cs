using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Chat;

public sealed class ChatMessage
{
    public const int MaxTextLength = 4000;

    private ChatMessage()
    {
    }

    public ChatMessage(
        Guid conversationId,
        Guid senderUserId,
        Guid clientMessageId,
        string content,
        DateTimeOffset sentAt)
    {
        if (conversationId == Guid.Empty)
            throw new DomainException("Chat conversation is required.");
        if (senderUserId == Guid.Empty)
            throw new DomainException("Chat sender is required.");
        if (clientMessageId == Guid.Empty)
            throw new DomainException("Chat client message id is required.");
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Chat message content is required.");

        var normalized = content.Trim();
        if (normalized.Length > MaxTextLength)
            throw new DomainException($"Chat message content must contain at most {MaxTextLength} characters.");

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        ClientMessageId = clientMessageId;
        Content = normalized;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }
    public long Sequence { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
}
