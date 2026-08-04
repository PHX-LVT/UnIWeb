using AdminSite.Components.Pages;
using AdminSite.Models;
using AdminSite.Services;
using AdminSite.Services.Notifications;
using Bunit;
using Blazored.Toast;
using Contracts.Api;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace AdminSite.ComponentTests;

public sealed class CanvasSectionSwitchingTests : BunitContext
{
    public CanvasSectionSwitchingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var http = new SuccessfulHttpService();
        var sections = new AdminSectionService(http);
        var forms = new AdminFormSubmissionService(http);
        Services.AddSingleton<IHttpService>(http);
        Services.AddSingleton(sections);
        Services.AddSingleton(forms);
        Services.AddSingleton(new AdminPageService(http, sections, forms));
        Services.AddSingleton(new AdminBlockService(http));
        Services.AddSingleton(new AdminContentService(http));
        Services.AddSingleton(new AdminLanguageService(null!, new AdminSettingsService(http)));
        Services.AddBlazoredToast();
        Services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        Services.AddDevExpressBlazor();
    }

    [Fact]
    public async Task ClickingAnotherSection_SwitchesPersistentPanelAndPreservesTab()
    {
        var page = new PageModel
        {
            Id = "page-1",
            Name = new Dictionary<string, string> { ["en"] = "Test page" }
        };
        var sections = new List<SectionModel>
        {
            Section("section-a", "hero", 0),
            Section("section-b", "hero", 1)
        };
        var cut = Render<Canvas>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Page, page)
            .Add(component => component.Sections, sections)
            .Add(component => component.EditMode, true)
            .Add(component => component.CanEdit, true));

        await cut.InvokeAsync(() => cut.Instance.UpdateSectionPositions(
            [
                new Canvas.SectionPosition { Id = "section-a", Top = 0, Width = 1000, Height = 300 },
                new Canvas.SectionPosition { Id = "section-b", Top = 300, Width = 1000, Height = 300 }
            ],
            600,
            [],
            []));

        cut.Find("[data-section-select-id='section-a']").Click();
        Assert.Equal("section-a", cut.Find(".ez-section-editor-window").GetAttribute("data-selected-section-id"));

        cut.FindAll("button.ez-panel-tab")
            .Single(button => button.TextContent.Contains("Content", StringComparison.Ordinal))
            .Click();

        cut.Find("[data-section-select-id='section-b']").Click();
        var switchedPanel = cut.Find(".ez-section-editor-window");
        Assert.Equal("section-b", switchedPanel.GetAttribute("data-selected-section-id"));
        Assert.Contains("Content", cut.Find("button.ez-panel-tab.active").TextContent, StringComparison.Ordinal);

        cut.Find("[data-section-select-id='section-b']").Click();
        Assert.Equal("section-b", cut.Find(".ez-section-editor-window").GetAttribute("data-selected-section-id"));
    }

    [Fact]
    public async Task ExplicitClose_IsRequiredBeforePanelCanDisappear()
    {
        var page = new PageModel
        {
            Id = "page-1",
            Name = new Dictionary<string, string> { ["en"] = "Test page" }
        };
        var sections = new List<SectionModel> { Section("section-a", "hero", 0) };
        var cut = Render<Canvas>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Page, page)
            .Add(component => component.Sections, sections)
            .Add(component => component.EditMode, true)
            .Add(component => component.CanEdit, true));

        await cut.InvokeAsync(() => cut.Instance.UpdateSectionPositions(
            [new Canvas.SectionPosition { Id = "section-a", Top = 0, Width = 1000, Height = 300 }],
            300,
            [],
            []));

        cut.Find("[data-section-select-id='section-a']").Click();
        cut.Find(".ez-section-editor-window button[aria-label='Close']").Click();
        Assert.Empty(cut.FindAll(".ez-section-editor-window"));

        cut.Find("[data-section-select-id='section-a']").Click();
        Assert.Equal("section-a", cut.Find(".ez-section-editor-window").GetAttribute("data-selected-section-id"));
    }

    [Fact]
    public async Task UnsavedChanges_KeepCurrentSectionUntilDiscardIsConfirmed()
    {
        var page = new PageModel
        {
            Id = "page-1",
            Name = new Dictionary<string, string> { ["en"] = "Test page" }
        };
        var sections = new List<SectionModel>
        {
            Section("section-a", "hero", 0),
            Section("section-b", "hero", 1)
        };
        var cut = Render<Canvas>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Page, page)
            .Add(component => component.Sections, sections)
            .Add(component => component.EditMode, true)
            .Add(component => component.CanEdit, true));

        await cut.InvokeAsync(() => cut.Instance.UpdateSectionPositions(
            [
                new Canvas.SectionPosition { Id = "section-a", Top = 0, Width = 1000, Height = 300 },
                new Canvas.SectionPosition { Id = "section-b", Top = 300, Width = 1000, Height = 300 }
            ],
            600,
            [],
            []));

        cut.Find("[data-section-select-id='section-a']").Click();
        cut.Find(".ez-design-footer__reset .btn-admin").Click();
        cut.Find("[data-section-select-id='section-b']").Click();

        Assert.Equal("section-a", cut.Find(".ez-section-editor-window").GetAttribute("data-selected-section-id"));
        Assert.NotEmpty(cut.FindAll(".block-arrange-guard"));

        cut.Find(".block-arrange-guard .btn-danger").Click();

        Assert.Equal("section-b", cut.Find(".ez-section-editor-window").GetAttribute("data-selected-section-id"));
    }

    private static SectionModel Section(string id, string type, int order) => new()
    {
        Id = id,
        PageId = "page-1",
        Type = type,
        Order = order,
        Visible = true,
        Style = new SectionStyleModel()
    };

    private sealed class SuccessfulHttpService : IHttpService
    {
        public Task<ApiResponse<T>> GetAsync<T>(string uri) => Success<T>();
        public Task<ApiResponse<T>> PostAsync<T>(string uri, object body) => Success<T>();
        public Task<ApiResponse<T>> PostLongRunningAsync<T>(string uri, object body) => Success<T>();
        public Task<ApiResponse<T>> PutAsync<T>(string uri, object body) => Success<T>();
        public Task<ApiResponse<T>> DeleteAsync<T>(string uri) => Success<T>();
        public Task<ApiResponse<T>> PostFileAsync<T>(string uri, IBrowserFile file, string fieldName = "file", long maxBytes = 10 * 1024 * 1024, IReadOnlyDictionary<string, string>? formFields = null) => Success<T>();
        public Task<FileDownloadResult> GetFileAsync(string uri) => Task.FromResult(new FileDownloadResult { Success = true });
        public Task<FileDownloadResult> PostFileDownloadAsync(string uri, object body) => Task.FromResult(new FileDownloadResult { Success = true });
        public void Notify(string? message, int statusCode) { }
        public void Notify<T>(ApiResponse<T>? response, string? successFallback = null, string? failureFallback = null) { }
        public void NotifyInline<T>(ApiResponse<T>? response, string targetId, string? successFallback = null, string? failureFallback = null) { }
        public void SilentSuccess<T>(ApiResponse<T>? response, string? failureFallback = null) { }
        public void SetInlineStatus(string targetId, AdminFeedbackSeverity severity, string message, string? technicalDetail = null) { }
        public void ClearInlineStatus(string targetId) { }

        private static Task<ApiResponse<T>> Success<T>() => Task.FromResult(new ApiResponse<T>
        {
            Success = true,
            StatusCode = 200
        });
    }
}
