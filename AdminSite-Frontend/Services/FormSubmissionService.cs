using AdminSite.Models;

namespace AdminSite.Services
{
    public class AdminFormSubmissionService
    {
        private readonly IHttpService _http;
        public AdminFormSubmissionService(IHttpService http) => _http = http;

        public Task<ApiResponse<List<FormSubmissionModel>>> GetAllAsync() =>
            _http.GetAsync<List<FormSubmissionModel>>("api/admin/forms/submissions");

        public Task<ApiResponse<object>> DeleteAsync(string id) =>
            _http.DeleteAsync<object>($"api/admin/forms/submissions/{id}");
    }
}