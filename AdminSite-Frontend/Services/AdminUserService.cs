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

        public Task<ApiResponse<AdminPagedResponse<AdminSessionResponse>>> GetSessionsAsync(int page, int pageSize, string? adminId = null)
        {
            var query = $"?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(adminId))
                query += $"&adminId={Uri.EscapeDataString(adminId)}";
            return _http.GetAsync<AdminPagedResponse<AdminSessionResponse>>($"api/admin/users/sessions{query}");
        }

        public Task<ApiResponse<AdminPagedResponse<AdminLoginActivityResponse>>> GetLoginActivityAsync(int page, int pageSize, string? adminId = null)
        {
            var query = $"?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(adminId))
                query += $"&adminId={Uri.EscapeDataString(adminId)}";
            return _http.GetAsync<AdminPagedResponse<AdminLoginActivityResponse>>($"api/admin/users/login-activity{query}");
        }

        public Task<ApiResponse<long>> DeleteSessionsAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/sessions/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<long>> DeleteLoginActivityAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/login-activity/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<AdminPagedResponse<AdminAuditLogResponse>>> GetAuditLogsAsync(int page, int pageSize, string? targetId = null)
        {
            var query = $"?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(targetId))
                query += $"&targetId={Uri.EscapeDataString(targetId)}";
            return _http.GetAsync<AdminPagedResponse<AdminAuditLogResponse>>($"api/admin/users/audit{query}");
        }

        public Task<ApiResponse<long>> DeleteAuditLogsAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/audit/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<List<AdminSessionResponse>>> GetMySessionsAsync() =>
            _http.GetAsync<List<AdminSessionResponse>>("api/admin/users/me/sessions");

        public Task<ApiResponse<List<AdminLoginActivityResponse>>> GetMyLoginActivityAsync() =>
            _http.GetAsync<List<AdminLoginActivityResponse>>("api/admin/users/me/login-activity");

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
