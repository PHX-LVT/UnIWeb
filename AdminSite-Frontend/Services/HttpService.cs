using AdminSite.Models;
using AdminSite.Services.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminSite.Services
{
    public interface IHttpService
    {
        Task<ApiResponse<T>> GetAsync<T>(string uri);
        Task<ApiResponse<T>> PostAsync<T>(string uri, object body);
        Task<ApiResponse<T>> PostLongRunningAsync<T>(string uri, object body);
        Task<ApiResponse<T>> PutAsync<T>(string uri, object body);
        Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null);
        Task<ApiResponse<T>> DeleteAsync<T>(string uri);
        Task<FileDownloadResult> GetFileAsync(string uri);
        Task<FileDownloadResult> PostFileDownloadAsync(string uri, object body);
        void Notify(string? message, int statusCode);
        void Notify<T>(ApiResponse<T>? response, string? successFallback = null, string? failureFallback = null);
        void NotifyInline<T>(ApiResponse<T>? response, string targetId, string? successFallback = null, string? failureFallback = null);
        void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null);
        void SetInlineStatus(string targetId, AdminFeedbackSeverity severity, string message, string? technicalDetail = null);
        void ClearInlineStatus(string targetId);
    }

    public sealed class FileDownloadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = "download.bin";
        public string ContentType { get; set; } = "application/octet-stream";
        public string? NotificationKey { get; set; }
        public List<string>? NotificationArgs { get; set; }
        public string? ErrorCode { get; set; }
        public string? TraceId { get; set; }

        public static FileDownloadResult Fail(
            string message,
            int statusCode,
            string? notificationKey = null,
            string? errorCode = null,
            string? traceId = null) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode,
            NotificationKey = notificationKey,
            ErrorCode = errorCode,
            TraceId = traceId
        };

        public ApiResponse<object> ToApiResponse() => new()
        {
            Success = Success,
            Message = Message,
            StatusCode = StatusCode,
            NotificationKey = NotificationKey,
            NotificationArgs = NotificationArgs,
            ErrorCode = ErrorCode,
            TraceId = TraceId
        };
    }

    public class HttpService : IHttpService
    {
        private readonly HttpClient _http;
        private readonly HttpClient _uploadHttp;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly AdminSessionInvalidationService _invalidations;
        private readonly IAdminNotificationService _notifications;
        private readonly ILogger<HttpService> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public HttpService(
            IHttpClientFactory httpClientFactory,
            AuthenticationStateProvider authenticationStateProvider,
            AdminSessionInvalidationService invalidations,
            IAdminNotificationService notifications,
            ILogger<HttpService> logger)
        {
            _http = httpClientFactory.CreateClient(AdminAuthConstants.ApiClientName);
            _uploadHttp = httpClientFactory.CreateClient(AdminAuthConstants.ApiUploadClientName);
            _authenticationStateProvider = authenticationStateProvider;
            _invalidations = invalidations;
            _notifications = notifications;
            _logger = logger;
        }

        public Task<ApiResponse<T>> GetAsync<T>(string uri) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, uri));

        public Task<ApiResponse<T>> PostAsync<T>(string uri, object body) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = Json(body)
            });

        public Task<ApiResponse<T>> PostLongRunningAsync<T>(string uri, object body) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = Json(body)
            }, _uploadHttp);

        public Task<ApiResponse<T>> PutAsync<T>(string uri, object body) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = Json(body)
            });

        public async Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(maxBytes));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, fieldName, file.Name);
            if (formFields is not null)
            {
                foreach (var field in formFields)
                {
                    if (!string.IsNullOrWhiteSpace(field.Key))
                        content.Add(new StringContent(field.Value ?? string.Empty), field.Key);
                }
            }

            return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = content
            }, _uploadHttp);
        }

        public Task<ApiResponse<T>> DeleteAsync<T>(string uri) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Delete, uri));

        public async Task<FileDownloadResult> GetFileAsync(string uri)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            return await DownloadAsync(request);
        }

        public async Task<FileDownloadResult> PostFileDownloadAsync(string uri, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = Json(body) };
            return await DownloadAsync(request);
        }

        private async Task<FileDownloadResult> DownloadAsync(HttpRequestMessage request)
        {

            try
            {
                var auth = await GetAuthenticationContextAsync();
                if (auth.HasToken)
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", auth.Token);

                using var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized &&
                    ShouldExpireSession(request, response, auth.IsAuthenticated))
                {
                    PublishSessionInvalidation(auth);
                    return FileDownloadResult.Fail(
                        "Session expired.",
                        401,
                        "NotificationSessionExpired",
                        "authentication-required");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var apiResponse = await ReadApiResponse<object>(response);
                    return FileDownloadResult.Fail(
                        apiResponse?.Message ?? response.ReasonPhrase ?? "Download failed.",
                        (int)response.StatusCode,
                        apiResponse?.NotificationKey ?? "NotificationDownloadFailed",
                        apiResponse?.ErrorCode ?? ErrorCodeFromStatus((int)response.StatusCode),
                        apiResponse?.TraceId);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var disposition = response.Content.Headers.ContentDisposition;
                var filename = disposition?.FileNameStar
                               ?? disposition?.FileName?.Trim('"')
                               ?? "download.bin";

                return new FileDownloadResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Bytes = bytes,
                    FileName = filename,
                    ContentType = response.Content.Headers.ContentType?.MediaType
                                  ?? "application/octet-stream"
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Download request timed out.");
                return FileDownloadResult.Fail(
                    "Download failed: the request timed out.",
                    504,
                    "NotificationRequestTimedOut",
                    "request-timeout");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Download service was unavailable.");
                return FileDownloadResult.Fail(
                    "Download failed: the service is temporarily unavailable.",
                    503,
                    "NotificationServiceUnavailable",
                    "service-unavailable");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected download failure.");
                return FileDownloadResult.Fail(
                    "Download failed: an unexpected system error occurred.",
                    500,
                    "NotificationDownloadFailed",
                    "unexpected-error");
            }
        }

        private Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request) => SendAsync<T>(request, _http);

        private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, HttpClient client)
        {
            try
            {
                var auth = await GetAuthenticationContextAsync();
                if (auth.HasToken && !IsLoginRequest(request))
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", auth.Token);

                using var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (ShouldExpireSession(request, response, auth.IsAuthenticated))
                    {
                        PublishSessionInvalidation(auth);
                        return ApiResponse<T>.Fail(
                            "Session expired.",
                            401,
                            notificationKey: "NotificationSessionExpired");
                    }

                    return await ReadApiResponse<T>(response)
                           ?? ApiResponse<T>.Fail(
                               "Unauthorized.",
                               401,
                               notificationKey: "NotificationUnauthorized");
                }

                return await ReadApiResponse<T>(response)
                       ?? EmptyResponse<T>(response);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Admin API request timed out.");
                return ApiResponse<T>.Fail(
                    "Request timed out.",
                    504,
                    notificationKey: "NotificationRequestTimedOut",
                    errorCode: "request-timeout");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Admin API request service was unavailable.");
                return ApiResponse<T>.Fail(
                    "The API is temporarily unavailable.",
                    503,
                    notificationKey: "NotificationServiceUnavailable",
                    errorCode: "service-unavailable");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Admin API request failure.");
                return ApiResponse<T>.Fail(
                    "Request failed.",
                           502,
                    notificationKey: "NotificationRequestFailed",
                    errorCode: "unexpected-error");
            }
        }

        public void Notify(string? message, int statusCode) =>
            _notifications.Notify(message, statusCode);

        public void Notify<T>(ApiResponse<T>? response, string? successFallback = null, string? failureFallback = null) =>
            _notifications.NotifyResponse(response, successFallback, failureFallback);

        public void NotifyInline<T>(
            ApiResponse<T>? response,
            string targetId,
            string? successFallback = null,
            string? failureFallback = null) =>
            _notifications.NotifyResponse(
                response,
                successFallback,
                failureFallback,
                AdminFeedbackDisplayMode.Inline,
                targetId);

        public void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null) =>
            _notifications.SilentSuccess(response, failureFallback);

        public void SetInlineStatus(
            string targetId,
            AdminFeedbackSeverity severity,
            string message,
            string? technicalDetail = null) =>
            _notifications.SetInlineStatus(targetId, severity, message, technicalDetail);

        public void ClearInlineStatus(string targetId) =>
            _notifications.ClearInlineStatus(targetId);

        private static StringContent Json(object body) =>
            new(JsonSerializer.Serialize(body, body.GetType(), _json), Encoding.UTF8, "application/json");

        private static async Task<ApiResponse<T>?> ReadApiResponse<T>(HttpResponseMessage response)
        {
            ApiResponse<T>? result;
            var statusCode = (int)response.StatusCode;
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content)) return null;

            try
            {
                result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _json);
            }
            catch (JsonException)
            {
                return ApiResponse<T>.Fail(
                    "Unexpected response format.",
                    statusCode,
                    notificationKey: "NotificationUnexpectedResponse");
            }

            if (result is null) return null;

            result.StatusCode = statusCode;
            result.Success = response.IsSuccessStatusCode && result.Success;
            result.ErrorCode ??= result.Success ? null : ErrorCodeFromStatus(statusCode);
            if (response.Headers.TryGetValues("Retry-After", out var retryValues) &&
                int.TryParse(retryValues.FirstOrDefault(), out var retryAfter))
                result.RetryAfterSeconds ??= retryAfter;

            return result;
        }

        private static ApiResponse<T> EmptyResponse<T>(HttpResponseMessage response)
        {
            var statusCode = (int)response.StatusCode;
            return statusCode switch
            {
                401 => ApiResponse<T>.Fail("Authentication failed: the session is no longer valid.", 401,
                    notificationKey: "NotificationSessionExpired", errorCode: "authentication-required"),
                403 => ApiResponse<T>.Fail("Permission denied: your account cannot perform this operation.", 403,
                    notificationKey: "NotificationPermissionDenied", errorCode: "permission-denied"),
                404 => ApiResponse<T>.Fail("Load failed: the requested item no longer exists.", 404,
                    notificationKey: "NotificationActionFailed",
                    notificationArgs: ["@action:operation.load", "@reason:not-found"], errorCode: "not-found"),
                413 => ApiResponse<T>.Fail("Upload failed: the selected file exceeds the allowed size.", 413,
                    notificationKey: "NotificationActionFailed",
                    notificationArgs: ["@action:resource.uploaded", "@reason:size-exceeded"], errorCode: "size-exceeded"),
                429 => RateLimited<T>(response),
                >= 500 => ApiResponse<T>.Fail("Request failed: the required service is temporarily unavailable.", statusCode,
                    notificationKey: "NotificationServiceUnavailable", errorCode: "service-unavailable"),
                _ => ApiResponse<T>.Fail("Request failed: the server returned no details.", statusCode,
                    notificationKey: "NotificationNoResponse", errorCode: ErrorCodeFromStatus(statusCode))
            };
        }

        private static ApiResponse<T> RateLimited<T>(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.TryGetValues("Retry-After", out var values) &&
                             int.TryParse(values.FirstOrDefault(), out var parsed)
                ? Math.Max(1, parsed)
                : 1;
            var result = ApiResponse<T>.Fail(
                "Request failed: too many requests are currently active.",
                429,
                notificationKey: "NotificationRateLimited",
                notificationArgs: [retryAfter.ToString()],
                errorCode: "rate-limited");
            result.RetryAfterSeconds = retryAfter;
            return result;
        }

        private static string ErrorCodeFromStatus(int statusCode) => statusCode switch
        {
            401 => "authentication-required",
            403 => "permission-denied",
            404 => "not-found",
            409 => "conflict",
            410 => "expired",
            413 => "size-exceeded",
            422 => "validation-failed",
            429 => "rate-limited",
            502 or 503 or 504 => "service-unavailable",
            >= 500 => "unexpected-error",
            _ => "operation-failed"
        };

        private static bool ShouldExpireSession(
            HttpRequestMessage request,
            HttpResponseMessage response,
            bool isAuthenticated)
        {
            var sessionInvalid = response.Headers.TryGetValues("X-Admin-Session-Invalid", out var values) &&
                                 values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
            if (sessionInvalid) return true;
            if (!isAuthenticated) return false;

            return !IsLoginRequest(request);
        }

        private async Task<AdminRequestAuthentication> GetAuthenticationContextAsync()
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var principal = state.User;
            return new AdminRequestAuthentication(
                principal.Identity?.IsAuthenticated == true,
                AdminAuthConstants.GetApiToken(principal),
                AdminAuthConstants.GetAdminId(principal),
                AdminAuthConstants.GetTokenId(principal));
        }

        private void PublishSessionInvalidation(AdminRequestAuthentication auth)
        {
            if (!string.IsNullOrWhiteSpace(auth.AdminId) &&
                !string.IsNullOrWhiteSpace(auth.TokenId))
            {
                _invalidations.InvalidateToken(
                    auth.AdminId,
                    auth.TokenId,
                    "session-expired");
            }
        }

        private static bool IsLoginRequest(HttpRequestMessage request)
        {
            var uri = request.RequestUri?.OriginalString ?? string.Empty;
            return uri.Contains("api/auth/login", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record AdminRequestAuthentication(
            bool IsAuthenticated,
            string? Token,
            string? AdminId,
            string? TokenId)
        {
            public bool HasToken => !string.IsNullOrWhiteSpace(Token);
        }
    }
}




