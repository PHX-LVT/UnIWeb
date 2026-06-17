namespace AdminSite.Services
{
    public class AdminUserService
    {
        private readonly IHttpService _http;

        public AdminUserService(IHttpService http)
        {
            _http = http;
        }

        public Task<ApiResponse<List<AdminUserResponse>>> GetUsersAsync() =>
            _http.GetAsync<List<AdminUserResponse>>("api/admin/users");

        public Task<ApiResponse<AdminUserResponse>> CreateUserAsync(AdminUserCreateRequest request) =>
            _http.PostAsync<AdminUserResponse>("api/admin/users", request);

        public Task<ApiResponse<AdminUserResponse>> UpdateUserAsync(string id, AdminUserUpdateRequest request) =>
            _http.PutAsync<AdminUserResponse>($"api/admin/users/{id}", request);

        public Task<ApiResponse<AdminUserResponse>> EnableUserAsync(string id) =>
            _http.PostAsync<AdminUserResponse>($"api/admin/users/{id}/enable", new { });

        public Task<ApiResponse<AdminUserResponse>> DisableUserAsync(string id) =>
            _http.PostAsync<AdminUserResponse>($"api/admin/users/{id}/disable", new { });

        public Task<ApiResponse<AdminUserResponse>> ResetPasswordAsync(string id, string newPassword) =>
            _http.PostAsync<AdminUserResponse>($"api/admin/users/{id}/reset-password", new AdminPasswordResetRequest
            {
                NewPassword = newPassword
            });

        public Task<ApiResponse<AdminUserResponse>> DeleteUserAsync(string id) =>
            _http.DeleteAsync<AdminUserResponse>($"api/admin/users/{id}");

        public Task<ApiResponse<List<AdminSessionResponse>>> GetSessionsAsync(string? adminId = null)
        {
            var query = string.IsNullOrWhiteSpace(adminId) ? string.Empty : $"?adminId={Uri.EscapeDataString(adminId)}";
            return _http.GetAsync<List<AdminSessionResponse>>($"api/admin/users/sessions{query}");
        }

        public Task<ApiResponse<long>> DeleteSessionsAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/sessions/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<List<AdminAuditLogResponse>>> GetAuditLogsAsync(string? targetId = null)
        {
            var query = string.IsNullOrWhiteSpace(targetId) ? string.Empty : $"?targetId={Uri.EscapeDataString(targetId)}";
            return _http.GetAsync<List<AdminAuditLogResponse>>($"api/admin/users/audit{query}");
        }

        public Task<ApiResponse<long>> DeleteAuditLogsAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/audit/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<List<AdminSessionResponse>>> GetMySessionsAsync() =>
            _http.GetAsync<List<AdminSessionResponse>>("api/admin/users/me/sessions");

        public Task<ApiResponse<List<AdminAuditLogResponse>>> GetMyAuditLogsAsync() =>
            _http.GetAsync<List<AdminAuditLogResponse>>("api/admin/users/me/audit");

        public Task<ApiResponse<string>> UpdatePasswordAsync(string currentPassword, string newPassword) =>
            _http.PutAsync<string>("api/auth/password", new PasswordUpdateRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            });
    }
}
