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
        Assert.False(message.TryGetAudioMetadata(out _));
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

    [Fact]
    public void Audio_message_keeps_private_media_metadata_in_internal_envelope()
    {
        var mediaId = Guid.NewGuid();
        var message = ChatMessage.CreateAudio(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            mediaId,
            "gravacao.webm",
            "audio/webm",
            12345,
            DateTimeOffset.UtcNow);

        Assert.True(message.TryGetAudioMetadata(out var audio));
        Assert.Equal(mediaId, audio.MediaId);
        Assert.Equal("gravacao.webm", audio.OriginalFileName);
        Assert.Equal("audio/webm", audio.ContentType);
        Assert.Equal(12345, audio.SizeBytes);
        Assert.DoesNotContain("chat/audio/", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Reserved_audio_envelope_cannot_be_sent_as_text()
    {
        Assert.Throws<DomainException>(() => new ChatMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "\u001eCED_AUDIO_V1:{}",
            DateTimeOffset.UtcNow));
    }
}
