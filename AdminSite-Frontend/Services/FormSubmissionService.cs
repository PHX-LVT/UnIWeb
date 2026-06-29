using AdminSite.Models;

namespace AdminSite.Services
{
    public class AdminFormSubmissionService
    {
        private readonly IHttpService _http;
        public AdminFormSubmissionService(IHttpService http) => _http = http;

        public Task<ApiResponse<List<FormSubmissionModel>>> GetAllAsync() =>
            _http.GetAsync<List<FormSubmissionModel>>("api/admin/forms/submissions");

        public Task<ApiResponse<List<FormSubmissionModel>>> GetAllAsync(
            string? formKey,
            FormSubmissionStatusModel? status,
            string? search,
            DateTime? from = null,
            DateTime? to = null)
        {
            var suffix = BuildQuery(formKey, status, search, from, to);
            return _http.GetAsync<List<FormSubmissionModel>>($"api/admin/forms/submissions{suffix}");
        }

        public Task<FileDownloadResult> ExportAsync(
            string? formKey,
            FormSubmissionStatusModel? status,
            string? search,
            DateTime? from,
            DateTime? to)
        {
            var suffix = BuildQuery(formKey, status, search, from, to);
            return _http.GetFileAsync($"api/admin/forms/submissions/export{suffix}");
        }

        public Task<ApiResponse<FormSubmissionModel>> GetAsync(string id) =>
            _http.GetAsync<FormSubmissionModel>($"api/admin/forms/submissions/{id}");

        public Task<ApiResponse<List<FormSubmissionAssigneeModel>>> GetAssigneesAsync() =>
            _http.GetAsync<List<FormSubmissionAssigneeModel>>("api/admin/forms/submissions/assignees");

        public Task<ApiResponse<FormSubmissionModel>> UpdateAsync(string id, FormSubmissionUpdateRequest request) =>
            _http.PutAsync<FormSubmissionModel>($"api/admin/forms/submissions/{id}", request);

        public Task<ApiResponse<object>> BulkStatusAsync(List<string> ids, FormSubmissionStatusModel status) =>
            _http.PostAsync<object>("api/admin/forms/submissions/bulk-status", new { Ids = ids, Status = status });

        public Task<ApiResponse<object>> BulkDeleteAsync(List<string> ids) =>
            _http.PostAsync<object>("api/admin/forms/submissions/bulk-delete", new { Ids = ids });

        public Task<ApiResponse<object>> DeleteAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/forms/submissions/{id}");

        public Task<ApiResponse<List<FormDefinitionModel>>> GetDefinitionsAsync() =>
            _http.GetAsync<List<FormDefinitionModel>>("api/admin/forms/definitions");

        public Task<ApiResponse<FormDefinitionUsageModel>> GetDefinitionUsageAsync(string id) =>
            _http.GetAsync<FormDefinitionUsageModel>($"api/admin/forms/definitions/{id}/usage");

        public Task<ApiResponse<FormDefinitionModel>> SaveDefinitionAsync(FormDefinitionModel definition)
        {
            var request = new
            {
                definition.Key,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel,
                definition.DisplayMode,
                definition.Layout,
                definition.Active,
                definition.Fields
            };

            return string.IsNullOrWhiteSpace(definition.Id)
                ? _http.PostAsync<FormDefinitionModel>("api/admin/forms/definitions", request)
                : _http.PutAsync<FormDefinitionModel>($"api/admin/forms/definitions/{definition.Id}", request);
        }

        public Task<ApiResponse<object>> DeleteDefinitionAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/forms/definitions/{id}");

        private static string BuildQuery(
            string? formKey,
            FormSubmissionStatusModel? status,
            string? search,
            DateTime? from = null,
            DateTime? to = null)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(formKey)) query.Add($"formKey={Uri.EscapeDataString(formKey)}");
            if (status is not null) query.Add($"status={status}");
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (from is not null) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd"))}");
            if (to is not null) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd"))}");
            return query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        }
    }
}
