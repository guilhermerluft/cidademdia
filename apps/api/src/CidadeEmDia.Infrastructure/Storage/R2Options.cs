using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Storage;

internal sealed record R2Options(
    string? AccountId,
    string? AccessKeyId,
    string? SecretAccessKey,
    string? Bucket,
    long MaxImageBytes,
    long MaxVideoBytes,
    long MaxAudioBytes,
    TimeSpan UploadUrlLifetime,
    TimeSpan ReadUrlLifetime)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey)
        && !string.IsNullOrWhiteSpace(Bucket);

    public static R2Options FromConfiguration(IConfiguration configuration) =>
        new(
            configuration["R2_ACCOUNT_ID"],
            configuration["R2_ACCESS_KEY_ID"],
            configuration["R2_SECRET_ACCESS_KEY"],
            configuration["R2_BUCKET"],
            ReadPositiveLong(configuration["OCCURRENCE_MEDIA_MAX_IMAGE_BYTES"], 10L * 1024 * 1024),
            ReadPositiveLong(configuration["OCCURRENCE_MEDIA_MAX_VIDEO_BYTES"], 100L * 1024 * 1024),
            ReadPositiveLong(configuration["CHAT_AUDIO_MAX_BYTES"], 20L * 1024 * 1024),
            TimeSpan.FromMinutes(ReadPositiveInt(configuration["R2_UPLOAD_URL_MINUTES"], 10)),
            TimeSpan.FromMinutes(ReadPositiveInt(configuration["R2_READ_URL_MINUTES"], 5)));

    private static long ReadPositiveLong(string? value, long fallback) =>
        long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
