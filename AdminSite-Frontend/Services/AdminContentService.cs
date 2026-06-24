using AdminSite.Models;

namespace AdminSite.Services
{
    public class AdminContentService
    {
        private readonly IHttpService _http;

        public AdminContentService(IHttpService http)
        {
            _http = http;
        }

        public Task<ApiResponse<List<ContentTypeModel>>> GetTypesAsync() =>
            _http.GetAsync<List<ContentTypeModel>>("api/admin/content/types");

        public Task<ApiResponse<ContentTypeModel>> CreateTypeAsync(ContentTypeRequest req) =>
            _http.PostAsync<ContentTypeModel>("api/admin/content/types", req);

        public Task<ApiResponse<ContentTypeModel>> UpdateTypeAsync(string id, ContentTypeRequest req) =>
            _http.PutAsync<ContentTypeModel>($"api/admin/content/types/{id}", req);

        public Task<ApiResponse<object>> DeleteTypeAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/content/types/{id}");

        public Task<ApiResponse<List<ContentItemModel>>> GetAllAsync(string? typeKey = null, string? status = null, string? scope = null)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(typeKey))
                query.Add($"typeKey={Uri.EscapeDataString(typeKey)}");
            if (!string.IsNullOrWhiteSpace(status))
                query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(scope))
                query.Add($"scope={Uri.EscapeDataString(scope)}");

            var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
            return _http.GetAsync<List<ContentItemModel>>($"api/admin/content{suffix}");
        }

        public Task<ApiResponse<ContentItemModel>> GetByIdAsync(string id) =>
            _http.GetAsync<ContentItemModel>($"api/admin/content/{id}");

        public Task<ApiResponse<ContentItemModel>> CreateAsync(ContentItemRequest req) =>
            _http.PostAsync<ContentItemModel>("api/admin/content", req);

        public Task<ApiResponse<ContentItemModel>> UpdateAsync(string id, ContentItemRequest req) =>
            _http.PutAsync<ContentItemModel>($"api/admin/content/{id}", req);

        public Task<ApiResponse<ContentItemModel>> SetStatusAsync(string id, string status, string? message = null) =>
            _http.PutAsync<ContentItemModel>($"api/admin/content/{id}/status", new ContentStatusRequest
            {
                Status = status,
                Message = message
            });

        public Task<ApiResponse<ContentItemModel>> PublishAsync(string id) =>
            _http.PostAsync<ContentItemModel>($"api/admin/content/{id}/publish", new { });

        public Task<ApiResponse<ContentItemModel>> RestoreAsync(string id) =>
            _http.PostAsync<ContentItemModel>($"api/admin/content/{id}/restore", new { });

        public Task<ApiResponse<object>> DeleteAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/content/{id}");

        public Task<ApiResponse<object>> PermanentDeleteAsync(IEnumerable<string> ids) =>
            _http.PostAsync<object>("api/admin/content/permanent-delete", new { Ids = ids.ToList() });


        public Task<ApiResponse<List<ManagedResourceModel>>> GetResourcesAsync(string? kind = null, string? search = null, bool includeInactive = true)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(kind))
                query.Add($"kind={Uri.EscapeDataString(kind)}");
            if (!string.IsNullOrWhiteSpace(search))
                query.Add($"search={Uri.EscapeDataString(search)}");
            query.Add($"includeInactive={includeInactive.ToString().ToLowerInvariant()}");
            var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
            return _http.GetAsync<List<ManagedResourceModel>>($"api/admin/resources{suffix}");
        }

        public Task<ApiResponse<ManagedResourceModel>> CreateResourceAsync(ManagedResourceRequest req) =>
            _http.PostAsync<ManagedResourceModel>("api/admin/resources", req);

        public Task<ApiResponse<ManagedResourceModel>> UpdateResourceAsync(string id, ManagedResourceRequest req) =>
            _http.PutAsync<ManagedResourceModel>($"api/admin/resources/{id}", req);

        public Task<ApiResponse<ManagedResourceModel>> UploadResourceAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file) =>
            _http.PostFileAsync<ManagedResourceModel>("api/admin/resources/upload", file, maxBytes: 20 * 1024 * 1024);
        public Task<ApiResponse<List<ContentAuditLogModel>>> GetLogsAsync(string stableId) =>
            _http.GetAsync<List<ContentAuditLogModel>>($"api/admin/content/{stableId}/logs");
    }
}
