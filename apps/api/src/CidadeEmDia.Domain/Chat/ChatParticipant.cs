using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Chat;

public enum ChatParticipantRole
{
    Citizen,
    Master
}

public sealed class ChatParticipant
{
    private ChatParticipant()
    {
    }

    internal ChatParticipant(
        Guid conversationId,
        Guid userId,
        ChatParticipantRole role,
        DateTimeOffset createdAt)
    {
        if (conversationId == Guid.Empty)
            throw new DomainException("Chat conversation is required.");
        if (userId == Guid.Empty)
            throw new DomainException("Chat participant user is required.");

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ChatParticipantRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
