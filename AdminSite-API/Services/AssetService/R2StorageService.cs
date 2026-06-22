using FullProject.Settings;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace GlobalManager.Services.AssetService
{
    public class R2StorageService
    {
        private const string Service = "s3";
        private const string Region = "auto";
        private readonly HttpClient _http;
        private readonly R2StorageSettings _settings;

        public R2StorageService(HttpClient http, IOptions<R2StorageSettings> settings)
        {
            _http = http;
            _settings = settings.Value;
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsConfigured)
                throw new InvalidOperationException("R2 storage is not configured. Set R2Storage in appsettings.json.");

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) throw new InvalidOperationException("Upload file is empty.");
            if (bytes.Length > _settings.MaxUploadBytes)
                throw new InvalidOperationException($"Upload exceeds max size of {_settings.MaxUploadBytes / 1024 / 1024}MB.");

            var key = BuildKey(fileName, folder);
            var payloadHash = ToHex(SHA256.HashData(bytes));
            var now = DateTimeOffset.UtcNow;
            var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var host = $"{_settings.AccountId}.r2.cloudflarestorage.com";
            var canonicalUri = $"/{_settings.BucketName}/{Uri.EscapeDataString(key).Replace("%2F", "/")}";
            var endpoint = new Uri($"https://{host}{canonicalUri}");

            var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
            const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            var canonicalRequest = $"PUT\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
            var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
            var signature = ToHex(Hmac(GetSigningKey(dateStamp), stringToSign));

            using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
            request.Headers.TryAddWithoutValidation("Authorization",
                $"AWS4-HMAC-SHA256 Credential={_settings.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"R2 upload failed ({(int)response.StatusCode}): {body}");
            }

            return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
        }


        public async Task<bool> DeleteAsync(string? publicUrl, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsConfigured || string.IsNullOrWhiteSpace(publicUrl)) return false;

            var baseUrl = _settings.PublicBaseUrl.TrimEnd('/') + "/";
            if (!publicUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) return false;

            var key = Uri.UnescapeDataString(publicUrl[baseUrl.Length..]);
            if (string.IsNullOrWhiteSpace(key)) return false;

            var payloadHash = ToHex(SHA256.HashData(Array.Empty<byte>()));
            var now = DateTimeOffset.UtcNow;
            var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var host = $"{_settings.AccountId}.r2.cloudflarestorage.com";
            var canonicalUri = $"/{_settings.BucketName}/{Uri.EscapeDataString(key).Replace("%2F", "/")}";
            var endpoint = new Uri($"https://{host}{canonicalUri}");

            var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
            const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            var canonicalRequest = $"DELETE\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
            var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
            var signature = ToHex(Hmac(GetSigningKey(dateStamp), stringToSign));

            using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
            request.Headers.TryAddWithoutValidation("Authorization",
                $"AWS4-HMAC-SHA256 Credential={_settings.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");

            using var response = await _http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }

        private string BuildKey(string fileName, string folder)
        {
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
            var cleanFolder = SanitizePath(folder);
            var prefix = SanitizePath(_settings.KeyPrefix);
            var date = DateTime.UtcNow.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            return string.Join('/', new[] { prefix, cleanFolder, date, name }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string SanitizePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => new string(part.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()))
                .Where(part => part.Length > 0);
            return string.Join('/', parts);
        }

        private byte[] GetSigningKey(string dateStamp)
        {
            var kDate = Hmac(Encoding.UTF8.GetBytes("AWS4" + _settings.SecretAccessKey), dateStamp);
            var kRegion = Hmac(kDate, Region);
            var kService = Hmac(kRegion, Service);
            return Hmac(kService, "aws4_request");
        }

        private static byte[] Hmac(byte[] key, string data) =>
            new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

        private static string ToHex(byte[] bytes) =>
            Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
