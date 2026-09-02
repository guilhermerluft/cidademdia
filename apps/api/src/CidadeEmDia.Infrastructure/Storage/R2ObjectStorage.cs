using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace CidadeEmDia.Infrastructure.Storage;

internal sealed class R2ObjectStorage(R2Options options)
{
    private static readonly HttpClient HttpClient = new();

    public bool IsConfigured => options.IsConfigured;
    public long MaxImageBytes => options.MaxImageBytes;
    public long MaxVideoBytes => options.MaxVideoBytes;
    public long MaxAudioBytes => options.MaxAudioBytes;
    public TimeSpan UploadUrlLifetime => options.UploadUrlLifetime;
    public TimeSpan ReadUrlLifetime => options.ReadUrlLifetime;

    public Uri CreateUploadUrl(
        string objectKey,
        string contentType,
        DateTimeOffset now,
        out DateTimeOffset expiresAt)
    {
        expiresAt = now.Add(options.UploadUrlLifetime);
        return CreatePresignedUrl(HttpMethod.Put, objectKey, contentType, options.UploadUrlLifetime, now);
    }

    public Uri CreateReadUrl(
        string objectKey,
        DateTimeOffset now,
        out DateTimeOffset expiresAt)
    {
        expiresAt = now.Add(options.ReadUrlLifetime);
        return CreatePresignedUrl(HttpMethod.Get, objectKey, null, options.ReadUrlLifetime, now);
    }

    public async Task<R2ObjectMetadata?> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
            return null;

        var now = DateTimeOffset.UtcNow;
        var url = CreatePresignedUrl(HttpMethod.Head, objectKey, null, TimeSpan.FromMinutes(2), now);

        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"R2 object metadata request failed with HTTP {(int)response.StatusCode}.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType)
            && response.Headers.TryGetValues("Content-Type", out var values))
        {
            contentType = values.FirstOrDefault();
        }

        var length = response.Content.Headers.ContentLength;
        if (!length.HasValue)
            throw new InvalidOperationException("R2 object metadata did not include Content-Length.");

        return new R2ObjectMetadata(length.Value, contentType?.Trim().ToLowerInvariant());
    }

    public async Task<byte[]?> ReadObjectPrefixAsync(
        string objectKey,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
            return null;
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        var now = DateTimeOffset.UtcNow;
        var url = CreatePresignedUrl(HttpMethod.Get, objectKey, null, TimeSpan.FromMinutes(2), now);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, maxBytes - 1);

        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode is not HttpStatusCode.PartialContent
            && response.StatusCode is not HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"R2 object signature request failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (read == 0)
                break;

            totalRead += read;
        }

        return buffer[..totalRead];
    }

    private Uri CreatePresignedUrl(
        HttpMethod method,
        string objectKey,
        string? contentType,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("Cloudflare R2 is not configured.");

        var accountId = options.AccountId!;
        var accessKeyId = options.AccessKeyId!;
        var secretAccessKey = options.SecretAccessKey!;
        var bucket = options.Bucket!;

        var host = $"{accountId}.r2.cloudflarestorage.com";
        var timestamp = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        const string region = "auto";
        const string service = "s3";
        const string algorithm = "AWS4-HMAC-SHA256";
        var credentialScope = $"{date}/{region}/{service}/aws4_request";

        var canonicalUri = $"/{EncodePathSegment(bucket)}/{EncodeObjectKey(objectKey)}";
        var signedHeaders = string.IsNullOrWhiteSpace(contentType)
            ? "host"
            : "content-type;host";

        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = algorithm,
            ["X-Amz-Credential"] = $"{accessKeyId}/{credentialScope}",
            ["X-Amz-Date"] = timestamp,
            ["X-Amz-Expires"] = Math.Clamp((int)lifetime.TotalSeconds, 1, 604800).ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = signedHeaders
        };

        var canonicalQuery = string.Join(
            "&",
            query.Select(pair => $"{AwsEncode(pair.Key)}={AwsEncode(pair.Value)}"));

        var canonicalHeaders = string.IsNullOrWhiteSpace(contentType)
            ? $"host:{host}\n"
            : $"content-type:{contentType.Trim().ToLowerInvariant()}\nhost:{host}\n";

        var canonicalRequest = string.Join(
            "\n",
            method.Method,
            canonicalUri,
            canonicalQuery,
            canonicalHeaders,
            signedHeaders,
            "UNSIGNED-PAYLOAD");

        var stringToSign = string.Join(
            "\n",
            algorithm,
            timestamp,
            credentialScope,
            Sha256Hex(canonicalRequest));

        var signingKey = DeriveSigningKey(secretAccessKey, date, region, service);
        var signature = HmacHex(signingKey, stringToSign);

        return new Uri($"https://{host}{canonicalUri}?{canonicalQuery}&X-Amz-Signature={signature}");
    }

    private static byte[] DeriveSigningKey(string secret, string date, string region, string service)
    {
        var dateKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + secret), date);
        var regionKey = Hmac(dateKey, region);
        var serviceKey = Hmac(regionKey, service);
        return Hmac(serviceKey, "aws4_request");
    }

    private static byte[] Hmac(byte[] key, string value)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string HmacHex(byte[] key, string value) =>
        Convert.ToHexString(Hmac(key, value)).ToLowerInvariant();

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string EncodeObjectKey(string objectKey) =>
        string.Join('/', objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(EncodePathSegment));

    private static string EncodePathSegment(string value) => AwsEncode(value);

    private static string AwsEncode(string value) =>
        Uri.EscapeDataString(value)
            .Replace("%7E", "~", StringComparison.OrdinalIgnoreCase);
}

internal sealed record R2ObjectMetadata(long SizeBytes, string? ContentType);
