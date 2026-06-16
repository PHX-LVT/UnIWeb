using AdminSite.Models;

namespace AdminSite.Services
{
    public class GlobalButtonsService
    {
        private readonly IHttpService _http;
        public GlobalButtonsService(IHttpService http) => _http = http;

        public Task<ApiResponse<List<GlobalButtonModel>>> GetAllAsync() =>
            _http.GetAsync<List<GlobalButtonModel>>("api/admin/global/buttons");

        public Task<ApiResponse<GlobalButtonModel>> CreateAsync(GlobalButtonRequest req) =>
            _http.PostAsync<GlobalButtonModel>("api/admin/global/buttons", req);

        public Task<ApiResponse<GlobalButtonModel>> UpdateAsync(string id, GlobalButtonRequest req) =>
            _http.PutAsync<GlobalButtonModel>($"api/admin/global/buttons/{id}", req);

        public Task<ApiResponse<object>> DeleteAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/global/buttons/{id}");

        public Task<ApiResponse<object>> SetVisibilityAsync(string id, bool visible) =>
            _http.PutAsync<object>($"api/admin/global/buttons/{id}/visibility",
                new VisibilityRequest { Visible = visible });

        public Task<ApiResponse<object>> ReorderAsync(List<string> orderedIds) =>
            _http.PutAsync<object>("api/admin/global/buttons/reorder",
                new ReorderRequest { OrderedIds = orderedIds });
    }
}