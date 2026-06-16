using AdminSite.Models;

namespace AdminSite.Services
{
    public class BrandingService
    {
        private readonly IHttpService _http;
        public BrandingService(IHttpService http) => _http = http;

        public Task<ApiResponse<BrandingModel>> GetAsync() =>
            _http.GetAsync<BrandingModel>("api/admin/global/branding");

        public Task<ApiResponse<BrandingModel>> UpdateAsync(BrandingModel model) =>
            _http.PutAsync<BrandingModel>("api/admin/global/branding", model);
    }
}