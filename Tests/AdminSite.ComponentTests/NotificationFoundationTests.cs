using System.Net;
using System.Security.Claims;
using System.Text;
using AdminSite.Languages;
using AdminSite.Services;
using AdminSite.Services.Authentication;
using AdminSite.Services.Notifications;
using AdminSite.Shared.Components;
using Bunit;
using Contracts.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdminSite.ComponentTests;

public sealed class NotificationFoundationTests
{
    [Fact]
    public void FailedBodyWithSuccessfulStatus_NeverProducesSuccessFeedback()
    {
        var response = ApiResponse<object>.Fail("Operation failed.", 200);

        var feedback = AdminFeedbackMessage.FromResponse(response);

        Assert.Equal(AdminFeedbackSeverity.Error, feedback.Severity);
    }

    [Theory]
    [InlineData(401, AdminFeedbackSeverity.Warning)]
    [InlineData(403, AdminFeedbackSeverity.Warning)]
    [InlineData(422, AdminFeedbackSeverity.Warning)]
    [InlineData(429, AdminFeedbackSeverity.Warning)]
    [InlineData(500, AdminFeedbackSeverity.Error)]
    public void SeverityPolicy_IsStable(int status, AdminFeedbackSeverity expected) =>
        Assert.Equal(expected, AdminFeedbackMessage.SeverityFromStatusCode(status));

    [Fact]
    public void StructuredActionArguments_AreResolvedForEveryAdminLanguage()
    {
        foreach (var language in new[] { "en", "vi", "cn" })
        {
            var action = AdminOutcomeTextResolver.ResolveArgument("@action:resource-album.deleted", language);
            var reason = AdminOutcomeTextResolver.ResolveArgument("@reason:in-use", language);

            Assert.DoesNotContain("@action:", action, StringComparison.Ordinal);
            Assert.DoesNotContain("@reason:", reason, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(action));
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    [Theory]
    [InlineData("direct_upload_disabled")]
    [InlineData("storage_unavailable")]
    [InlineData("resource_not_found")]
    [InlineData("replacement_in_progress")]
    [InlineData("too_many_active_uploads")]
    [InlineData("upload_in_progress")]
    [InlineData("registry_reconciliation_required")]
    [InlineData("resource_reconciliation_failed")]
    [InlineData("invalid_upload_context")]
    [InlineData("upload_expired")]
    [InlineData("invalid_upload_state")]
    public void ResourceUploadFailureCodes_HaveMeaningfulLocalizedReasons(string errorCode)
    {
        foreach (var language in new[] { "en", "vi", "cn" })
        {
            var generic = AdminOutcomeTextResolver.ResolveArgument("@reason:operation-failed", language);
            var resolved = AdminOutcomeTextResolver.ResolveArgument($"@reason:{errorCode}", language);

            Assert.False(string.IsNullOrWhiteSpace(resolved));
            Assert.NotEqual(generic, resolved);
        }
    }

    [Fact]
    public void NotificationKeys_ArePresentInEveryAdminLanguage()
    {
        var requiredKeys = new[]
        {
            "UploadBatchPartial",
            "ContentSaveSyncFailed",
            "ThemePreviewRefreshFailed",
            "PageLoadServiceUnavailable",
            "PreviewLoadFailed",
            "PageNotFound",
            "LogoTooLarge",
            "UploadSelectionFailed",
            "UploadStorageUnavailable",
            "UploadBrowserFileUnavailable",
            "UploadPartVerificationFailed"
        };
        var keys = EnglishUIText.Catalog.Values.Keys
            .Where(key => key.StartsWith("Notification", StringComparison.OrdinalIgnoreCase))
            .Concat(requiredKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var key in keys)
        {
            Assert.True(EnglishUIText.Catalog.Values.ContainsKey(key), $"English key missing: {key}");
            Assert.True(VietnameseUIText.Catalog.Values.ContainsKey(key), $"Vietnamese key missing: {key}");
            Assert.True(ChineseUIText.Catalog.Values.ContainsKey(key), $"Chinese key missing: {key}");
        }
    }

    [Fact]
    public async Task HttpService_UsesActualHttpStatusInsteadOfWrappedStatus()
    {
        const string body = """{"success":true,"statusCode":200,"message":"Saved."}""";
        var service = Service(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var response = await service.GetAsync<object>("api/test");

        Assert.False(response.Success);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("permission-denied", response.ErrorCode);
    }

    [Fact]
    public async Task HttpService_NormalizesEmptyForbiddenResponse()
    {
        var service = Service(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var response = await service.GetAsync<object>("api/test");

        Assert.False(response.Success);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("NotificationPermissionDenied", response.NotificationKey);
        Assert.Equal("permission-denied", response.ErrorCode);
    }

    [Fact]
    public async Task HttpService_PreservesRateLimitRetryTimeAsStructuredFeedback()
    {
        var httpResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        httpResponse.Headers.TryAddWithoutValidation("Retry-After", "7");
        var service = Service(httpResponse);

        var response = await service.GetAsync<object>("api/test");

        Assert.False(response.Success);
        Assert.Equal(429, response.StatusCode);
        Assert.Equal("rate-limited", response.ErrorCode);
        Assert.Equal("NotificationRateLimited", response.NotificationKey);
        Assert.Equal(7, response.RetryAfterSeconds);
        Assert.Equal(["7"], response.NotificationArgs);
    }

    [Fact]
    public void ValidationSummary_RendersFieldErrorsAsPersistentAccessibleFeedback()
    {
        using var context = new BunitContext();
        var errors = new List<ApiFieldError>
        {
            new() { FieldKey = "Title", Message = "Title is required." },
            new() { FieldKey = "Slug", Message = "Slug is invalid." }
        };

        var component = context.Render<AdminValidationSummary>(parameters => parameters
            .Add(item => item.Errors, errors)
            .Add(item => item.Title, "Correct these fields."));

        Assert.Equal("alert", component.Find(".admin-validation-summary").GetAttribute("role"));
        Assert.Contains("Title is required.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Slug is invalid.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineFeedback_IsClearedWhenNavigationChangesContext()
    {
        var navigation = new TestNavigationManager();
        using var notifications = new AdminNotificationService(
            null!,
            new AdminLanguageContext(),
            navigation,
            NullLogger<AdminNotificationService>.Instance);
        notifications.SetInlineStatus("editor", AdminFeedbackSeverity.Error, "Save failed.");
        Assert.True(notifications.TryGetInlineStatus("editor", out _));

        navigation.GoTo("content/all");

        Assert.False(notifications.TryGetInlineStatus("editor", out _));
    }

    [Fact]
    public void ToastHosts_PreserveOperationFeedbackAcrossNavigation()
    {
        var root = RepositoryRoot();
        foreach (var layout in new[] { "MainLayout.razor", "EmptyLayout.razor" })
        {
            var source = File.ReadAllText(Path.Combine(root, "AdminSite-Frontend", "Shared", layout));
            Assert.Contains("RemoveToastsOnNavigation=\"false\"", source, StringComparison.Ordinal);
        }
    }

    private static HttpService Service(HttpResponseMessage response)
    {
        var client = new HttpClient(new StaticHandler(response)) { BaseAddress = new Uri("https://admin.test/") };
        return new HttpService(
            new StaticHttpClientFactory(client),
            new AnonymousAuthenticationStateProvider(),
            new AdminSessionInvalidationService(NullLogger<AdminSessionInvalidationService>.Instance),
            new NullNotifications(),
            NullLogger<HttpService>.Instance);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "AdminSite-Frontend")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class NullNotifications : IAdminNotificationService
    {
        public event Action? InlineFeedbackChanged { add { } remove { } }
        public void Notify(AdminFeedbackMessage message) { }
        public void Notify(string? message, int statusCode) { }
        public void NotifyResponse<T>(ApiResponse<T>? response, string? successFallback = null, string? failureFallback = null, AdminFeedbackDisplayMode displayMode = AdminFeedbackDisplayMode.Toast, string? inlineTargetId = null) { }
        public void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null) { }
        public void SetInlineStatus(string targetId, AdminFeedbackSeverity severity, string message, string? technicalDetail = null) { }
        public void ClearInlineStatus(string targetId) { }
        public bool TryGetInlineStatus(string targetId, out AdminInlineFeedbackState status) { status = null!; return false; }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://admin.test/", "https://admin.test/");

        public void GoTo(string uri) => NavigateTo(uri);

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(false);
        }
    }
}
