using FullProject.Settings;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MongoDB.Bson;

namespace FullProject.Services.AssetService;

public class R2StorageService
{
    private const string Service = "s3";
    private const string Region = "auto";
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";
    private readonly HttpClient _http;
    private readonly R2StorageSettings _settings;
    private readonly AssetStorageKeyPolicy _keyPolicy;

    public R2StorageService(HttpClient http, IOptions<R2StorageSettings> settings, AssetStorageKeyPolicy keyPolicy)
    {
        _http = http;
        _settings = settings.Value;
        _keyPolicy = keyPolicy;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public string PublicUrl(string key) =>
        $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key.TrimStart('/')}";

    public string CreatePendingKey(string sessionId, string fileName) =>
        _keyPolicy.CreateTemporaryKey(sessionId, fileName);

    public string CreateResourceFinalKey(string resourceId, int version, string kind, string friendlyName, string fileName) =>
        _keyPolicy.CreateResourceKey(resourceId, version, kind, friendlyName, fileName);

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        var result = await UploadWithMetadataAsync(stream, fileName, contentType, folder, cancellationToken);
        return result.Url;
    }

    public async Task<R2UploadResult> UploadWithMetadataAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        var assetId = ObjectId.GenerateNewId().ToString();
        var key = _keyPolicy.CreateMappedLegacyUploadKey(folder, assetId, 1, fileName);
        var result = await UploadWithMetadataToKeyAsync(stream, fileName, contentType, key, cancellationToken);
        result.AssetId = assetId;
        result.AssetVersion = 1;
        result.StorageSchemaVersion = _keyPolicy.SchemaVersion;
        return result;
    }

    public async Task<R2UploadResult> UploadWithMetadataToKeyAsync(
        Stream stream,
        string fileName,
        string contentType,
        string key,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (stream.CanSeek)
        {
            if (stream.Length == 0) throw new InvalidOperationException("Upload file is empty.");
            if (stream.Length > _settings.MaxUploadBytes)
                throw new InvalidOperationException($"Upload exceeds max size of {_settings.MaxUploadBytes / 1024 / 1024}MB.");
        }

        using var request = CreateSignedRequest(HttpMethod.Put, key, payloadHash: UnsignedPayload);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(NormalizeContentType(contentType));
        if (stream.CanSeek)
            request.Content.Headers.ContentLength = stream.Length - stream.Position;

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, "upload", cancellationToken);
        return new R2UploadResult { Url = PublicUrl(key), StorageKey = key };
    }

    public Uri CreatePresignedPutUrl(
        string key,
        TimeSpan lifetime,
        IReadOnlyDictionary<string, string>? query = null,
        string? contentType = null)
    {
        EnsureConfigured();
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var host = Host;
        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? null
            : NormalizeContentType(contentType);
        var signedHeaders = normalizedContentType is null ? "host" : "content-type;host";
        var canonicalHeaders = normalizedContentType is null
            ? $"host:{host}\n"
            : $"content-type:{NormalizeHeader(normalizedContentType)}\nhost:{host}\n";
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = $"{_settings.AccessKeyId}/{credentialScope}",
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = Math.Clamp((int)lifetime.TotalSeconds, 60, 604800).ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = signedHeaders
        };
        if (query is not null)
        {
            foreach (var pair in query)
                values[pair.Key] = pair.Value;
        }

        var canonicalQuery = CanonicalQuery(values);
        var canonicalRequest = $"PUT\n{CanonicalUri(key)}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{UnsignedPayload}";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{HashHex(canonicalRequest)}";
        values["X-Amz-Signature"] = ToHex(Hmac(GetSigningKey(dateStamp), stringToSign));
        return new Uri($"https://{host}{CanonicalUri(key)}?{CanonicalQuery(values)}");
    }

    public async Task<string> InitiateMultipartUploadAsync(string key, string contentType, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var query = new Dictionary<string, string> { ["uploads"] = string.Empty };
        using var request = CreateSignedRequest(
            HttpMethod.Post,
            key,
            query,
            EmptyHash,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = NormalizeContentType(contentType)
            });
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "start multipart upload", cancellationToken);
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return xml.Descendants().FirstOrDefault(node => node.Name.LocalName == "UploadId")?.Value
            ?? throw new InvalidOperationException("R2 did not return a multipart upload id.");
    }

    public Uri CreatePresignedPartUrl(string key, string uploadId, int partNumber, TimeSpan lifetime, string contentType) =>
        CreatePresignedPutUrl(key, lifetime, new Dictionary<string, string>
        {
            ["partNumber"] = partNumber.ToString(CultureInfo.InvariantCulture),
            ["uploadId"] = uploadId
        }, contentType);

    public async Task CompleteMultipartUploadAsync(
        string key,
        string uploadId,
        IReadOnlyList<R2CompletedPart> parts,
        CancellationToken cancellationToken = default)
    {
        var document = new XDocument(
            new XElement("CompleteMultipartUpload",
                parts.OrderBy(part => part.PartNumber).Select(part =>
                    new XElement("Part",
                        new XElement("PartNumber", part.PartNumber),
                        new XElement("ETag", NormalizeEtag(part.ETag))))));
        var payload = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        var query = new Dictionary<string, string> { ["uploadId"] = uploadId };
        using var request = CreateSignedRequest(HttpMethod.Post, key, query, ToHex(SHA256.HashData(payload)));
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "complete multipart upload", cancellationToken);
    }

    public async Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string> { ["uploadId"] = uploadId };
        using var request = CreateSignedRequest(HttpMethod.Delete, key, query, EmptyHash);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
            await EnsureSuccessAsync(response, "abort multipart upload", cancellationToken);
    }

    public async Task<R2ObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        using var request = CreateSignedRequest(HttpMethod.Head, key, payloadHash: EmptyHash);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "read object metadata", cancellationToken);
        return new R2ObjectMetadata
        {
            SizeBytes = response.Content.Headers.ContentLength ?? -1,
            ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            ETag = response.Headers.ETag?.Tag ?? string.Empty
        };
    }

    public async Task<R2ObjectListResult> ListObjectsAsync(
        string? prefix = null,
        int maximumObjects = 20_000,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        maximumObjects = Math.Clamp(maximumObjects, 1, 100_000);
        var result = new R2ObjectListResult();
        string? continuationToken = null;
        do
        {
            var query = new Dictionary<string, string>
            {
                ["list-type"] = "2",
                ["max-keys"] = Math.Min(1000, maximumObjects - result.Objects.Count).ToString(CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(prefix)) query["prefix"] = prefix.TrimStart('/');
            if (!string.IsNullOrWhiteSpace(continuationToken)) query["continuation-token"] = continuationToken;

            using var request = CreateSignedRequest(HttpMethod.Get, string.Empty, query, EmptyHash);
            using var response = await _http.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, "list objects", cancellationToken);
            var document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var content in document.Descendants().Where(node => node.Name.LocalName == "Contents"))
            {
                var key = content.Elements().FirstOrDefault(node => node.Name.LocalName == "Key")?.Value;
                if (string.IsNullOrWhiteSpace(key)) continue;
                _ = long.TryParse(content.Elements().FirstOrDefault(node => node.Name.LocalName == "Size")?.Value, out var size);
                _ = DateTime.TryParse(
                    content.Elements().FirstOrDefault(node => node.Name.LocalName == "LastModified")?.Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var modified);
                result.Objects.Add(new R2ListedObject
                {
                    Key = key,
                    SizeBytes = size,
                    ETag = content.Elements().FirstOrDefault(node => node.Name.LocalName == "ETag")?.Value ?? string.Empty,
                    LastModifiedUtc = modified == default ? null : modified
                });
                if (result.Objects.Count >= maximumObjects) break;
            }

            var truncatedValue = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "IsTruncated")?.Value;
            var pageTruncated = bool.TryParse(truncatedValue, out var truncated) && truncated;
            continuationToken = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "NextContinuationToken")?.Value;
            result.Truncated = pageTruncated;
            if (!pageTruncated || result.Objects.Count >= maximumObjects) break;
        } while (!string.IsNullOrWhiteSpace(continuationToken));
        return result;
    }

    public async Task<byte[]> ReadPrefixAsync(string key, int maximumBytes = 512, CancellationToken cancellationToken = default)
    {
        maximumBytes = Math.Clamp(maximumBytes, 1, 512 * 1024);
        using var request = CreateSignedRequest(HttpMethod.Get, key, payloadHash: EmptyHash);
        request.Headers.Range = new RangeHeaderValue(0, Math.Max(0, maximumBytes - 1));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, "inspect object", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(maximumBytes);
        var chunk = new byte[Math.Min(8192, maximumBytes)];
        var remaining = maximumBytes;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), cancellationToken);
            if (read <= 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
        return buffer.ToArray();
    }

    public async Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        var copySource = $"/{_settings.BucketName}/{EncodePath(sourceKey)}";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-amz-copy-source"] = copySource
        };
        using var request = CreateSignedRequest(HttpMethod.Put, destinationKey, payloadHash: EmptyHash, extraHeaders: headers);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "promote object", cancellationToken);
    }

    public Task<bool> DeleteKeyAsync(string? key, CancellationToken cancellationToken = default) =>
        DeleteObjectAsync(key, cancellationToken);

    public async Task<bool> DeleteAsync(string? publicUrl, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured || string.IsNullOrWhiteSpace(publicUrl)) return false;
        var baseUrl = _settings.PublicBaseUrl.TrimEnd('/') + "/";
        if (!publicUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) return false;
        return await DeleteObjectAsync(Uri.UnescapeDataString(publicUrl[baseUrl.Length..]), cancellationToken);
    }

    private async Task<bool> DeleteObjectAsync(string? key, CancellationToken cancellationToken)
    {
        if (!_settings.IsConfigured || string.IsNullOrWhiteSpace(key)) return false;
        using var request = CreateSignedRequest(HttpMethod.Delete, key, payloadHash: EmptyHash);
        using var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    private HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string key,
        IReadOnlyDictionary<string, string>? query = null,
        string? payloadHash = null,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        EnsureConfigured();
        payloadHash ??= EmptyHash;
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var host = Host;
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = host,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };
        if (extraHeaders is not null)
        {
            foreach (var pair in extraHeaders)
                headers[pair.Key.Trim().ToLowerInvariant()] = pair.Value.Trim();
        }

        var canonicalHeaders = string.Concat(headers.Select(pair => $"{pair.Key}:{NormalizeHeader(pair.Value)}\n"));
        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalQuery = CanonicalQuery(query);
        var canonicalRequest = $"{method.Method}\n{CanonicalUri(key)}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{HashHex(canonicalRequest)}";
        var signature = ToHex(Hmac(GetSigningKey(dateStamp), stringToSign));
        var endpoint = $"https://{host}{CanonicalUri(key)}" + (canonicalQuery.Length == 0 ? string.Empty : $"?{canonicalQuery}");

        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        if (extraHeaders is not null)
        {
            foreach (var pair in extraHeaders)
            {
                if (pair.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
                }
            }
        }
        request.Headers.TryAddWithoutValidation("Authorization",
            $"AWS4-HMAC-SHA256 Credential={_settings.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
        return request;
    }

    private string Host => $"{_settings.AccountId}.r2.cloudflarestorage.com";
    private string CanonicalUri(string key) => $"/{EncodePath(_settings.BucketName)}/{EncodePath(key)}";
    private static string EncodePath(string value) => string.Join('/', value.Split('/').Select(Encode));
    private static string Encode(string value) => Uri.EscapeDataString(value).Replace("%7E", "~", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalQuery(IReadOnlyDictionary<string, string>? values) => values is null
        ? string.Empty
        : string.Join('&', values
            .Select(pair => new KeyValuePair<string, string>(Encode(pair.Key), Encode(pair.Value ?? string.Empty)))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));

    private byte[] GetSigningKey(string dateStamp)
    {
        var date = Hmac(Encoding.UTF8.GetBytes("AWS4" + _settings.SecretAccessKey), dateStamp);
        var region = Hmac(date, Region);
        var service = Hmac(region, Service);
        return Hmac(service, "aws4_request");
    }

    private static byte[] Hmac(byte[] key, string data) => new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));
    private static string HashHex(string value) => ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static string NormalizeHeader(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeContentType(string? contentType) => string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
    private static string NormalizeEtag(string value) => $"\"{value.Trim().Trim('"')}\"";
    private static string EmptyHash => ToHex(SHA256.HashData(Array.Empty<byte>()));

    private void EnsureConfigured()
    {
        if (!_settings.IsConfigured)
            throw new InvalidOperationException("R2 storage is not configured. Set R2Storage in appsettings.json.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"R2 {operation} failed ({(int)response.StatusCode}): {body}");
    }
}

public sealed class R2UploadResult
{
    public string Url { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public int AssetVersion { get; set; }
    public int StorageSchemaVersion { get; set; }
}

public sealed class R2ObjectMetadata
{
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
}

public sealed record R2CompletedPart(int PartNumber, string ETag);

public sealed class R2ObjectListResult
{
    public List<R2ListedObject> Objects { get; set; } = [];
    public bool Truncated { get; set; }
}

public sealed class R2ListedObject
{
    public string Key { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ETag { get; set; } = string.Empty;
    public DateTime? LastModifiedUtc { get; set; }
}
