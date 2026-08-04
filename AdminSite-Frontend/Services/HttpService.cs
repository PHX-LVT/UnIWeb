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

        public static FileDownloadResult Fail(string message, int statusCode) => new()
        {
            Success = false,
            Message = message,
            StatusCode = statusCode
        };
    }

    public class HttpService : IHttpService
    {
        private readonly HttpClient _http;
        private readonly HttpClient _uploadHttp;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly AdminSessionInvalidationService _invalidations;
        private readonly IAdminNotificationService _notifications;

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
            IAdminNotificationService notifications)
        {
            _http = httpClientFactory.CreateClient(AdminAuthConstants.ApiClientName);
            _uploadHttp = httpClientFactory.CreateClient(AdminAuthConstants.ApiUploadClientName);
            _authenticationStateProvider = authenticationStateProvider;
            _invalidations = invalidations;
            _notifications = notifications;
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
                    return FileDownloadResult.Fail(AdminUiLocalizer.T("NotificationSessionExpired", "en"), 401);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var apiResponse = await ReadApiResponse<object>(response);
                    return FileDownloadResult.Fail(
                        apiResponse?.Message ?? response.ReasonPhrase ?? "Download failed.",
                        (int)response.StatusCode);
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
                _notifications.Notify(new AdminFeedbackMessage
                {
                    Severity = AdminFeedbackSeverity.Warning,
                    MessageKey = "NotificationRequestTimedOut",
                    MessageFallback = "The download request timed out. Please try again.",
                    TechnicalDetail = ex.Message
                });
                return FileDownloadResult.Fail("The download request timed out.", 504);
            }
            catch (HttpRequestException ex)
            {
                _notifications.Notify(new AdminFeedbackMessage
                {
                    Severity = AdminFeedbackSeverity.Error,
                    MessageKey = "NotificationServiceUnavailable",
                    MessageFallback = "The API is temporarily unavailable.",
                    TechnicalDetail = ex.Message
                });
                return FileDownloadResult.Fail("The API is temporarily unavailable.", 503);
            }
            catch (Exception ex)
            {
                _notifications.Notify(new AdminFeedbackMessage
                {
                    Severity = AdminFeedbackSeverity.Error,
                    MessageKey = "NotificationDownloadFailed",
                    MessageFallback = "Download failed.",
                    TechnicalDetail = ex.Message
                });
                return FileDownloadResult.Fail(AdminUiLocalizer.T("NotificationDownloadFailed", "en"), 500);
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
                       ?? ApiResponse<T>.Fail(
                           "No response from server.",
                           500,
                           notificationKey: "NotificationNoResponse");
            }
            catch (TaskCanceledException ex)
            {
                return ApiResponse<T>.Fail(
                    "Request timed out.",
                    504,
                    errors: [ex.Message],
                    notificationKey: "NotificationRequestTimedOut");
            }
            catch (HttpRequestException ex)
            {
                return ApiResponse<T>.Fail(
                    "The API is temporarily unavailable.",
                    503,
                    errors: [ex.Message],
                    notificationKey: "NotificationServiceUnavailable");
            }
            catch (Exception ex)
            {
                return ApiResponse<T>.Fail(
                    "Request failed.",
                           502,
                    errors: [ex.Message],
                    notificationKey: "NotificationRequestFailed");
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

            if (result is { Success: false, Errors.Count: > 0 })
                result.Message = string.Join(" ", result.Errors);

            return result;
        }

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




