using AdminSite.Models;
using Blazored.LocalStorage;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
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
        Task<ApiResponse<T>> PutAsync<T>(string uri, object body);
        Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null);
        Task<ApiResponse<T>> PostFilesAsync<T>(string uri, IReadOnlyList<IBrowserFile> files, string fieldName = "files", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null);
        Task<ApiResponse<T>> DeleteAsync<T>(string uri);
        Task<FileDownloadResult> GetFileAsync(string uri);
        void Toast(string? message, int statusCode);
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
        private readonly ILocalStorageService _storage;
        private readonly IToastService _toast;
        private readonly NavigationManager _nav;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public HttpService(
            HttpClient http,
            ILocalStorageService storage,
            IToastService toast,
            NavigationManager nav)
        {
            _http = http;
            _storage = storage;
            _toast = toast;
            _nav = nav;
        }

        public Task<ApiResponse<T>> GetAsync<T>(string uri) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, uri));

        public Task<ApiResponse<T>> PostAsync<T>(string uri, object body) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = Json(body)
            });

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
            });
        }

        public async Task<ApiResponse<T>> PostFilesAsync<T>(string uri, IReadOnlyList<IBrowserFile> files, string fieldName = "files", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null)
        {
            var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                var fileContent = new StreamContent(file.OpenReadStream(maxBytes));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
                content.Add(fileContent, fieldName, file.Name);
            }

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
            });
        }
        public Task<ApiResponse<T>> DeleteAsync<T>(string uri) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Delete, uri));

        public async Task<FileDownloadResult> GetFileAsync(string uri)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            try
            {
                var session = await _storage.GetItemAsync<AdminSession>("admin_session");
                var hasSessionToken = !string.IsNullOrWhiteSpace(session?.Token);
                if (hasSessionToken)
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", session!.Token);

                using var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized &&
                    ShouldExpireSession(request, response, hasSessionToken))
                {
                    await _storage.RemoveItemAsync("admin_session");
                    _nav.NavigateTo("/login");
                    return FileDownloadResult.Fail("Session expired.", 401);
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
            catch (Exception ex)
            {
                return FileDownloadResult.Fail(ex.Message, 500);
            }
        }

        private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request)
        {
            try
            {
                var session = await _storage.GetItemAsync<AdminSession>("admin_session");
                var hasSessionToken = !string.IsNullOrWhiteSpace(session?.Token);
                if (hasSessionToken && !IsLoginRequest(request))
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", session!.Token);

                using var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (ShouldExpireSession(request, response, hasSessionToken))
                    {
                        await _storage.RemoveItemAsync("admin_session");
                        _nav.NavigateTo("/login");
                        return ApiResponse<T>.Fail("Session expired.", 401);
                    }

                    return await ReadApiResponse<T>(response)
                           ?? ApiResponse<T>.Fail("Unauthorized.", 401);
                }

                return await ReadApiResponse<T>(response)
                       ?? ApiResponse<T>.Fail("Empty response.", 500);
            }
            catch (Exception ex)
            {
                return ApiResponse<T>.Fail(ex.Message, 500);
            }
        }

        public void Toast(string? message, int statusCode)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (statusCode is >= 200 and < 300)
                _toast.ShowSuccess(message);
            else if (statusCode is 400 or 404 or 422)
                _toast.ShowWarning(message);
            else
                _toast.ShowError(message);
        }

        private static StringContent Json(object body) =>
            new(JsonSerializer.Serialize(body, body.GetType(), _json), Encoding.UTF8, "application/json");

        private static async Task<ApiResponse<T>?> ReadApiResponse<T>(HttpResponseMessage response)
        {
            ApiResponse<T>? result;
            try
            {
                result = await response.Content
                    .ReadFromJsonAsync<ApiResponse<T>>(_json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (result is { Success: false, Errors.Count: > 0 })
                result.Message = string.Join(" ", result.Errors);

            return result;
        }

        private static bool ShouldExpireSession(
            HttpRequestMessage request,
            HttpResponseMessage response,
            bool hasSessionToken)
        {
            var sessionInvalid = response.Headers.TryGetValues("X-Admin-Session-Invalid", out var values) &&
                                 values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
            if (sessionInvalid) return true;
            if (!hasSessionToken) return false;

            return !IsLoginRequest(request);
        }

        private static bool IsLoginRequest(HttpRequestMessage request)
        {
            var uri = request.RequestUri?.OriginalString ?? string.Empty;
            return uri.Contains("api/auth/login", StringComparison.OrdinalIgnoreCase);
        }
    }
}




