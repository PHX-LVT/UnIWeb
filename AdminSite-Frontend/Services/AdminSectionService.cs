using AdminSite.Models;

using Contracts.Admin;

namespace AdminSite.Services
{
    public class AdminSectionService
    {
        private readonly IHttpService _http;
        public AdminSectionService(IHttpService http) => _http = http;

        private string Base(string pageId) =>
            $"api/admin/pages/{pageId}/sections";

        public Task<ApiResponse<List<SectionModel>>> GetAllAsync(string pageId) =>
            _http.GetAsync<List<SectionModel>>(Base(pageId));

        public Task<ApiResponse<SectionModel>> GetByIdAsync(string pageId, string sectionId) =>
            _http.GetAsync<SectionModel>($"{Base(pageId)}/{sectionId}");

        public Task<ApiResponse<SectionModel>> CreateAsync(string pageId, object dto) =>
            _http.PostAsync<SectionModel>(Base(pageId), dto);

        public Task<ApiResponse<SectionModel>> UpdateAsync(string pageId, string sectionId, object dto) =>
            _http.PutAsync<SectionModel>($"{Base(pageId)}/{sectionId}", dto);

        public Task<ApiResponse<SectionModel>> UpdateStyleAsync(string pageId, string sectionId, SectionStyleDto style) =>
            _http.PutAsync<SectionModel>($"{Base(pageId)}/{sectionId}/style", style);
        public Task<ApiResponse<object>> DeleteAsync(string pageId, string sectionId) =>
            _http.DeleteAsync<object>($"{Base(pageId)}/{sectionId}");

        public Task<ApiResponse<object>> SetVisibilityAsync(string pageId, string sectionId, bool visible) =>
            _http.PutAsync<object>($"{Base(pageId)}/{sectionId}/visibility",
                new VisibilityRequest { Visible = visible });
        public Task<ApiResponse<object>> ReorderAsync(string pageId, List<string> orderedIds) =>
            _http.PutAsync<object>($"{Base(pageId)}/reorder",
                new ReorderRequest { OrderedIds = orderedIds });

        public Task<ApiResponse<List<CanvasSectionPresetModel>>> GetCanvasPresetsAsync() =>
            _http.GetAsync<List<CanvasSectionPresetModel>>("api/admin/canvas-section-presets");

        public Task<ApiResponse<CanvasSectionPresetModel>> SaveCanvasPresetAsync(string pageId, string sectionId, Dictionary<string, string> name) =>
            _http.PostAsync<CanvasSectionPresetModel>("api/admin/canvas-section-presets",
                new CanvasSectionPresetCreateRequest
                {
                    PageId = pageId,
                    SectionId = sectionId,
                    Name = name
                });

        public Task<ApiResponse<object>> ApplyCanvasPresetAsync(string pageId, string presetId) =>
            _http.PostAsync<object>($"api/admin/canvas-section-presets/{presetId}/apply",
                new CanvasSectionPresetApplyRequest { PageId = pageId });

        public Task<ApiResponse<object>> DeleteCanvasPresetAsync(string presetId) =>
            _http.DeleteAsync<object>($"api/admin/canvas-section-presets/{presetId}");
    }
}
