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
                Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024);
        Task<ApiResponse<T>> DeleteAsync<T>(string uri);
        void Toast(string? message, int statusCode);
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

        public async Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(maxBytes));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, fieldName, file.Name);

            return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = content
            });
        }
        public Task<ApiResponse<T>> DeleteAsync<T>(string uri) =>
            SendAsync<T>(new HttpRequestMessage(HttpMethod.Delete, uri));

        private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request)
        {
            try
            {
                var session = await _storage.GetItemAsync<AdminSession>("admin_session");
                if (session?.Token is not null)
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", session.Token);

                using var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _storage.RemoveItemAsync("admin_session");
                    _nav.NavigateTo("/login");
                    return ApiResponse<T>.Fail("Session expired.", 401);
                }

                var result = await response.Content
                    .ReadFromJsonAsync<ApiResponse<T>>(_json);

                if (result is { Success: false, Errors.Count: > 0 })
                    result.Message = string.Join(" ", result.Errors);

                return result ?? ApiResponse<T>.Fail("Empty response.", 500);
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
    }
}




