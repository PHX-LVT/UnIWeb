using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminSite.Services.Authentication;

public enum AdminSessionCheckStatus
{
    Valid,
    Invalid,
    Unavailable
}

public sealed record AdminLoginResult(bool Success, LoginResponse? Login, string Error)
{
    public static AdminLoginResult Accepted(LoginResponse login) => new(true, login, string.Empty);
    public static AdminLoginResult Rejected(string error) => new(false, null, error);
}

public sealed record AdminSessionCheck(AdminSessionCheckStatus Status, SessionResponse? Session = null);

public sealed class AdminApiAuthenticationClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminApiAuthenticationClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminApiAuthenticationClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AdminApiAuthenticationClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AdminLoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await CreateClient().PostAsJsonAsync(
                "api/auth/login",
                new LoginRequest { Email = email, Password = password },
                JsonOptions,
                cancellationToken);

            var payload = await ReadResponseAsync<LoginResponse>(response, cancellationToken);
            if (!response.IsSuccessStatusCode || payload?.Success != true || payload.Data is null)
                return AdminLoginResult.Rejected(
                    SafeAuthenticationMessage(payload?.Message, response.StatusCode));

            if (string.IsNullOrWhiteSpace(payload.Data.Token) ||
                string.IsNullOrWhiteSpace(payload.Data.AdminId))
            {
                _logger.LogWarning("Admin login API returned an incomplete authentication response.");
                return AdminLoginResult.Rejected("The authentication service returned an invalid response.");
            }

            return AdminLoginResult.Accepted(payload.Data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin login API request failed.");
            return AdminLoginResult.Rejected("Unable to sign in. Please try again.");
        }
    }

    public async Task<AdminSessionCheck> CheckSessionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AdminSessionCheck(AdminSessionCheckStatus.Invalid);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await CreateClient().SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new AdminSessionCheck(AdminSessionCheckStatus.Invalid);

            if (!response.IsSuccessStatusCode)
                return new AdminSessionCheck(AdminSessionCheckStatus.Unavailable);

            var payload = await ReadResponseAsync<SessionResponse>(response, cancellationToken);
            if (payload?.Success != true || payload.Data is null)
                return new AdminSessionCheck(AdminSessionCheckStatus.Unavailable);

            return payload.Data.Valid
                ? new AdminSessionCheck(AdminSessionCheckStatus.Valid, payload.Data)
                : new AdminSessionCheck(AdminSessionCheckStatus.Invalid);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin session validation API request failed.");
            return new AdminSessionCheck(AdminSessionCheckStatus.Unavailable);
        }
    }

    public async Task<bool> TryLogoutAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            using var boundedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            boundedCancellation.CancelAfter(TimeSpan.FromSeconds(3));
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
            {
                Content = JsonContent.Create(new { })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await CreateClient().SendAsync(request, boundedCancellation.Token);
            if (response.IsSuccessStatusCode) return true;

            _logger.LogWarning(
                "Admin API rejected logout revocation with status {StatusCode}; the local cookie will still be cleared.",
                (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin API logout request failed; the local cookie will still be cleared.");
            return false;
        }
    }

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient(AdminAuthConstants.ApiClientName);

    private static async Task<ApiResponse<T>?> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0) return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SafeAuthenticationMessage(string? message, HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
            return "Invalid email or password.";
        if (statusCode == HttpStatusCode.TooManyRequests)
            return "Too many sign-in attempts. Please wait and try again.";
        if ((int)statusCode >= 500)
            return "The authentication service is temporarily unavailable.";

        return string.IsNullOrWhiteSpace(message) ? "Login failed." : message;
    }
}
