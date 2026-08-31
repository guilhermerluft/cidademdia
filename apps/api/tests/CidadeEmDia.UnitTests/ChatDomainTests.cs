using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Common;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class ChatDomainTests
{
    [Fact]
    public void Conversation_starts_active_with_citizen_and_master_participants()
    {
        var occurrenceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        var masterId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var conversation = new ChatConversation(
            occurrenceId,
            targetId,
            citizenId,
            masterId,
            createdAt);

        Assert.Equal(ChatConversationStatus.Active, conversation.Status);
        Assert.Null(conversation.ClosedAt);
        Assert.Equal(2, conversation.Participants.Count);
        Assert.Contains(conversation.Participants, x =>
            x.UserId == citizenId && x.Role == ChatParticipantRole.Citizen);
        Assert.Contains(conversation.Participants, x =>
            x.UserId == masterId && x.Role == ChatParticipantRole.Master);
    }

    [Fact]
    public void Closed_conversation_records_timestamp_and_is_not_active()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var conversation = new ChatConversation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAt);

        var closedAt = createdAt.AddHours(1);
        conversation.Close(closedAt);

        Assert.Equal(ChatConversationStatus.Closed, conversation.Status);
        Assert.Equal(closedAt, conversation.ClosedAt);
        Assert.False(conversation.IsActive);
    }

    [Fact]
    public void Text_message_normalizes_content_and_keeps_client_id()
    {
        var clientMessageId = Guid.NewGuid();

        var message = new ChatMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            clientMessageId,
            "  Mensagem de teste  ",
            DateTimeOffset.UtcNow);

        Assert.Equal(clientMessageId, message.ClientMessageId);
        Assert.Equal("Mensagem de teste", message.Content);
    }

    [Fact]
    public void Empty_text_message_is_rejected()
    {
        Assert.Throws<DomainException>(() => new ChatMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            DateTimeOffset.UtcNow));
    }
}
