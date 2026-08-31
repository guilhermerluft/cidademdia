using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Chat;

public enum ChatConversationStatus
{
    Active,
    Closed
}

public sealed class ChatConversation : BaseEntity
{
    private readonly List<ChatParticipant> _participants = [];

    private ChatConversation()
    {
    }

    public ChatConversation(
        Guid occurrenceId,
        Guid occurrenceTargetId,
        Guid citizenUserId,
        Guid masterUserId,
        DateTimeOffset createdAt)
    {
        if (occurrenceId == Guid.Empty)
            throw new DomainException("Chat occurrence is required.");
        if (occurrenceTargetId == Guid.Empty)
            throw new DomainException("Chat occurrence target is required.");
        if (citizenUserId == Guid.Empty)
            throw new DomainException("Chat citizen is required.");
        if (masterUserId == Guid.Empty)
            throw new DomainException("Chat Master is required.");
        if (citizenUserId == masterUserId)
            throw new DomainException("Chat participants must be different users.");

        OccurrenceId = occurrenceId;
        OccurrenceTargetId = occurrenceTargetId;
        CitizenUserId = citizenUserId;
        MasterUserId = masterUserId;
        Status = ChatConversationStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;

        _participants.Add(new ChatParticipant(Id, citizenUserId, ChatParticipantRole.Citizen, createdAt));
        _participants.Add(new ChatParticipant(Id, masterUserId, ChatParticipantRole.Master, createdAt));
    }

    public Guid OccurrenceId { get; private set; }
    public Guid OccurrenceTargetId { get; private set; }
    public Guid CitizenUserId { get; private set; }
    public Guid MasterUserId { get; private set; }
    public ChatConversationStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public IReadOnlyList<ChatParticipant> Participants => _participants.AsReadOnly();

    public bool IsActive => Status == ChatConversationStatus.Active;

    public void Close(DateTimeOffset closedAt)
    {
        if (Status == ChatConversationStatus.Closed)
            return;

        if (closedAt < CreatedAt)
            throw new DomainException("Chat closure cannot predate the conversation.");

        Status = ChatConversationStatus.Closed;
        ClosedAt = closedAt;
        UpdatedAt = closedAt;
    }
}
