using System.Text.Json;
using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Chat;

public sealed class ChatMessage
{
    public const int MaxTextLength = 4000;
    private const string AudioEnvelopePrefix = "\u001eCED_AUDIO_V1:";

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
        ValidateIdentity(conversationId, senderUserId, clientMessageId);

        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Chat message content is required.");

        var normalized = content.Trim();
        if (normalized.StartsWith(AudioEnvelopePrefix, StringComparison.Ordinal))
            throw new DomainException("Chat message content uses a reserved format.");
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

    public static ChatMessage CreateAudio(
        Guid conversationId,
        Guid senderUserId,
        Guid clientMessageId,
        Guid mediaId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset sentAt)
    {
        ValidateIdentity(conversationId, senderUserId, clientMessageId);

        if (mediaId == Guid.Empty)
            throw new DomainException("Chat audio media id is required.");

        var safeFileName = Path.GetFileName(originalFileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
            throw new DomainException("Chat audio file name is required and must contain at most 255 characters.");

        var normalizedContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedContentType) || normalizedContentType.Length > 120)
            throw new DomainException("Chat audio content type is required and must contain at most 120 characters.");
        if (sizeBytes <= 0)
            throw new DomainException("Chat audio size must be greater than zero.");

        var envelope = new AudioEnvelope(mediaId, safeFileName, normalizedContentType, sizeBytes);
        var content = AudioEnvelopePrefix + JsonSerializer.Serialize(envelope);
        if (content.Length > MaxTextLength)
            throw new DomainException("Chat audio metadata is too large.");

        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            ClientMessageId = clientMessageId,
            Content = content,
            SentAt = sentAt
        };
    }

    public bool TryGetAudioMetadata(out ChatAudioMetadata metadata)
    {
        metadata = default!;
        if (!Content.StartsWith(AudioEnvelopePrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var envelope = JsonSerializer.Deserialize<AudioEnvelope>(Content[AudioEnvelopePrefix.Length..]);
            if (envelope is null
                || envelope.MediaId == Guid.Empty
                || string.IsNullOrWhiteSpace(envelope.OriginalFileName)
                || string.IsNullOrWhiteSpace(envelope.ContentType)
                || envelope.SizeBytes <= 0)
            {
                return false;
            }

            metadata = new ChatAudioMetadata(
                envelope.MediaId,
                envelope.OriginalFileName,
                envelope.ContentType,
                envelope.SizeBytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateIdentity(Guid conversationId, Guid senderUserId, Guid clientMessageId)
    {
        if (conversationId == Guid.Empty)
            throw new DomainException("Chat conversation is required.");
        if (senderUserId == Guid.Empty)
            throw new DomainException("Chat sender is required.");
        if (clientMessageId == Guid.Empty)
            throw new DomainException("Chat client message id is required.");
    }

    private sealed record AudioEnvelope(
        Guid MediaId,
        string OriginalFileName,
        string ContentType,
        long SizeBytes);
}

public sealed record ChatAudioMetadata(
    Guid MediaId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);
