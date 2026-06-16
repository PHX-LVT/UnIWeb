using AdminSite.Models;

namespace AdminSite.Services
{
    public class FooterService
    {
        private readonly IHttpService _http;
        public FooterService(IHttpService http) => _http = http;

        public Task<ApiResponse<FooterModel>> GetAsync() =>
            _http.GetAsync<FooterModel>("api/admin/footer");

        public Task<ApiResponse<FooterModel>> UpdateMetaAsync(FooterMetaRequest req) =>
            _http.PutAsync<FooterModel>("api/admin/footer", req);

        public Task<ApiResponse<FooterModel>> CreateGroupAsync(FooterGroupRequest req) =>
            _http.PostAsync<FooterModel>("api/admin/footer/groups", req);

        public Task<ApiResponse<FooterModel>> UpdateGroupAsync(string groupId, FooterGroupRequest req) =>
            _http.PutAsync<FooterModel>($"api/admin/footer/groups/{groupId}", req);

        public Task<ApiResponse<FooterModel>> DeleteGroupAsync(string groupId) =>
            _http.DeleteAsync<FooterModel>($"api/admin/footer/groups/{groupId}");

        public Task<ApiResponse<FooterModel>> SetGroupVisibilityAsync(string groupId, bool visible) =>
            _http.PutAsync<FooterModel>($"api/admin/footer/groups/{groupId}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<FooterModel>> ReorderGroupsAsync(List<string> orderedIds) =>
            _http.PutAsync<FooterModel>("api/admin/footer/groups/reorder",
                new ReorderRequest { OrderedIds = orderedIds });

        public Task<ApiResponse<FooterModel>> CreateLinkAsync(string groupId, FooterLinkRequest req) =>
            _http.PostAsync<FooterModel>($"api/admin/footer/groups/{groupId}/links", req);

        public Task<ApiResponse<FooterModel>> UpdateLinkAsync(string groupId, string linkId, FooterLinkRequest req) =>
            _http.PutAsync<FooterModel>($"api/admin/footer/groups/{groupId}/links/{linkId}", req);

        public Task<ApiResponse<FooterModel>> DeleteLinkAsync(string groupId, string linkId) =>
            _http.DeleteAsync<FooterModel>($"api/admin/footer/groups/{groupId}/links/{linkId}");

        public Task<ApiResponse<FooterModel>> SetLinkVisibilityAsync(string groupId, string linkId, bool visible) =>
            _http.PutAsync<FooterModel>($"api/admin/footer/groups/{groupId}/links/{linkId}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<FooterModel>> ReorderLinksAsync(string groupId, List<string> orderedIds) =>
            _http.PutAsync<FooterModel>($"api/admin/footer/groups/{groupId}/links/reorder",
                new ReorderRequest { OrderedIds = orderedIds });
    }
}
