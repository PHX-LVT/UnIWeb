using AdminSite.Models;
using System.Text.Json;

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


        public Task<ApiResponse<List<ManagedResourceModel>>> GetResourcesAsync(string? kind = null, string? search = null, bool includeInactive = true, string? albumId = null)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(kind))
                query.Add($"kind={Uri.EscapeDataString(kind)}");
            if (!string.IsNullOrWhiteSpace(search))
                query.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(albumId))
                query.Add($"albumId={Uri.EscapeDataString(albumId)}");
            query.Add($"includeInactive={includeInactive.ToString().ToLowerInvariant()}");
            var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
            return _http.GetAsync<List<ManagedResourceModel>>($"api/admin/resources{suffix}");
        }

        public Task<ApiResponse<List<ResourceAlbumModel>>> GetResourceAlbumsAsync(string? scope = null)
        {
            var suffix = string.IsNullOrWhiteSpace(scope)
                ? string.Empty
                : $"?scope={Uri.EscapeDataString(scope)}";
            return _http.GetAsync<List<ResourceAlbumModel>>($"api/admin/resources/albums{suffix}");
        }

        public Task<ApiResponse<ResourceAlbumModel>> CreateResourceAlbumAsync(ResourceAlbumRequest req) =>
            _http.PostAsync<ResourceAlbumModel>("api/admin/resources/albums", req);

        public Task<ApiResponse<ResourceAlbumModel>> UpdateResourceAlbumAsync(string id, ResourceAlbumRequest req) =>
            _http.PutAsync<ResourceAlbumModel>($"api/admin/resources/albums/{id}", req);

        public Task<ApiResponse<object>> DeleteResourceAlbumAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/resources/albums/{id}");

        public Task<ApiResponse<ResourceAlbumAssignResourcesResult>> AssignResourcesToAlbumAsync(string id, IEnumerable<string> resourceIds) =>
            _http.PostAsync<ResourceAlbumAssignResourcesResult>($"api/admin/resources/albums/{id}/resources", new ResourceAlbumAssignResourcesRequest
            {
                ResourceIds = resourceIds.ToList()
            });

        public Task<ApiResponse<ManagedResourceModel>> UpdateResourceAsync(string id, ManagedResourceRequest req) =>
            _http.PutAsync<ManagedResourceModel>($"api/admin/resources/{id}", req);

        public Task<ApiResponse<ManagedResourceModel>> CreateResourceAsync(ManagedResourceRequest req) =>
            _http.PostAsync<ManagedResourceModel>("api/admin/resources", req);

        public Task<ApiResponse<ManagedResourceUsageModel>> GetResourceUsageAsync(string id) =>
            _http.GetAsync<ManagedResourceUsageModel>($"api/admin/resources/{id}/usage");

        public Task<ApiResponse<object>> DeleteResourceAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/resources/{id}");

        public Task<ApiResponse<ManagedResourceModel>> UploadResourceAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, string kind, string? albumId = null) =>
            _http.PostFileAsync<ManagedResourceModel>(
                "api/admin/resources/upload",
                file,
                maxBytes: 250 * 1024 * 1024,
                formFields: BuildUploadFields(kind, albumId));

        public Task<ApiResponse<ManagedResourceUploadBatchModel>> UploadResourcesAsync(
            IReadOnlyList<Microsoft.AspNetCore.Components.Forms.IBrowserFile> files,
            string kind,
            string? albumId = null,
            IReadOnlyList<string>? resourceNames = null)
        {
            var fields = BuildUploadFields(kind, albumId);
            fields["ResourceNamesJson"] = JsonSerializer.Serialize(resourceNames ?? []);
            return _http.PostFilesAsync<ManagedResourceUploadBatchModel>(
                "api/admin/resources/upload-batch",
                files,
                fieldName: "Files",
                maxBytes: 250 * 1024 * 1024,
                formFields: fields);
        }

        public Task<ApiResponse<ManagedResourceModel>> ReplaceResourceFileAsync(string id, Microsoft.AspNetCore.Components.Forms.IBrowserFile file, string kind) =>
            _http.PostFileAsync<ManagedResourceModel>(
                $"api/admin/resources/{id}/replace",
                file,
                maxBytes: 250 * 1024 * 1024,
                formFields: new Dictionary<string, string> { ["Kind"] = kind });

        private static Dictionary<string, string> BuildUploadFields(string kind, string? albumId)
        {
            var fields = new Dictionary<string, string> { ["Kind"] = kind };
            if (!string.IsNullOrWhiteSpace(albumId))
                fields["AlbumId"] = albumId;
            return fields;
        }

        public Task<ApiResponse<List<ContentAuditLogModel>>> GetLogsAsync(string stableId) =>
            _http.GetAsync<List<ContentAuditLogModel>>($"api/admin/content/{stableId}/logs");
    }
}
