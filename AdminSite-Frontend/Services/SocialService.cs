using AdminSite.Models;

namespace AdminSite.Services
{
    public class SocialService
    {
        private readonly IHttpService _http;
        public SocialService(IHttpService http) => _http = http;

        public Task<ApiResponse<SocialGroupModel>> GetAsync() =>
            _http.GetAsync<SocialGroupModel>("api/admin/social");

        public Task<ApiResponse<SocialButtonModel>> CreateAsync(SocialButtonRequest req) =>
            _http.PostAsync<SocialButtonModel>("api/admin/social", req);

        public Task<ApiResponse<SocialButtonModel>> UpdateAsync(string id, SocialButtonRequest req) =>
            _http.PutAsync<SocialButtonModel>($"api/admin/social/{id}", req);

        public Task<ApiResponse<object>> DeleteAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/social/{id}");

        public Task<ApiResponse<object>> SetButtonVisibilityAsync(string id, bool visible) =>
            _http.PutAsync<object>($"api/admin/social/{id}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<object>> SetGroupVisibilityAsync(bool visible) =>
            _http.PutAsync<object>("api/admin/social/visibility",
                new VisibilityRequest { Visible = visible });
    }
}