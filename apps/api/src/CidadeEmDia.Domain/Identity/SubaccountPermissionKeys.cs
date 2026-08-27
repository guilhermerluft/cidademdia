namespace CidadeEmDia.Domain.Identity;

public static class SubaccountPermissionKeys
{
    public const string OccurrenceReadTargeted = "occurrence.read.targeted";
    public const string OccurrenceStatusChange = "occurrence.status.change";
    public const string ChatRead = "chat.read";
    public const string ChatMessageSend = "chat.message.send";
    public const string ChatAudioSend = "chat.audio.send";

    public static IReadOnlyCollection<string> All { get; } =
    [
        OccurrenceReadTargeted,
        OccurrenceStatusChange,
        ChatRead,
        ChatMessageSend,
        ChatAudioSend
    ];
}
