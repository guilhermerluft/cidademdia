namespace CidadeEmDia.Infrastructure.Storage;

internal static class MediaSignatureValidator
{
    public static bool HasExpectedSignature(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType switch
        {
            "image/jpeg" => bytes.Length >= 3
                && bytes[0] == 0xFF
                && bytes[1] == 0xD8
                && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8
                && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytes.Length >= 12
                && bytes[..4].SequenceEqual("RIFF"u8)
                && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            "video/mp4" => bytes.Length >= 8
                && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
            "video/webm" => bytes.Length >= 4
                && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };
}
