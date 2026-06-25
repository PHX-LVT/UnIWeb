using AdminSite.Models;

namespace AdminSite.Services
{
    public class AdminSettingsService
    {
        private readonly IHttpService _http;
        public AdminSettingsService(IHttpService http) => _http = http;

        public event Action? AppearanceChanged;
        public AdminAppearanceModel CurrentAppearance { get; private set; } = new();

        public Task<ApiResponse<SiteSettingsModel>> GetLanguagesAsync() =>
            _http.GetAsync<SiteSettingsModel>("api/admin/settings/languages");

        public Task<ApiResponse<string>> UpdateLanguagesAsync(SiteSettingsUpdateModel settings) =>
            _http.PutAsync<string>("api/admin/settings/languages", settings);

        public async Task<ApiResponse<AdminAppearanceModel>> GetAdminAppearanceAsync()
        {
            var res = await _http.GetAsync<AdminAppearanceModel>("api/admin/settings/admin-appearance");
            if (res.Success && res.Data is not null)
                CurrentAppearance = res.Data;
            return res;
        }

        public async Task<ApiResponse<AdminAppearanceModel>> UpdateAdminAppearanceAsync(AdminAppearanceModel settings)
        {
            var res = await _http.PutAsync<AdminAppearanceModel>("api/admin/settings/admin-appearance", settings);
            if (res.Success && res.Data is not null)
            {
                CurrentAppearance = res.Data;
                AppearanceChanged?.Invoke();
            }
            return res;
        }

        public Task<ApiResponse<ResourceLibrarySettingsModel>> GetResourceLibrarySettingsAsync() =>
            _http.GetAsync<ResourceLibrarySettingsModel>("api/admin/settings/resource-library");

        public Task<ApiResponse<ResourceLibrarySettingsModel>> UpdateResourceLibrarySettingsAsync(ResourceLibrarySettingsModel settings) =>
            _http.PutAsync<ResourceLibrarySettingsModel>("api/admin/settings/resource-library", settings);

        public Task<ApiResponse<List<GlossaryTermModel>>> GetGlossaryAsync() =>
            _http.GetAsync<List<GlossaryTermModel>>("api/admin/settings/glossary");

        public Task<ApiResponse<GlossaryTermModel>> CreateTermAsync(GlossaryTermRequest req) =>
            _http.PostAsync<GlossaryTermModel>("api/admin/settings/glossary", req);

        public Task<ApiResponse<GlossaryTermModel>> UpdateTermAsync(string id, GlossaryTermRequest req) =>
            _http.PutAsync<GlossaryTermModel>($"api/admin/settings/glossary/{id}", req);

        public Task<ApiResponse<object>> DeleteTermAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/settings/glossary/{id}");
    }
}
