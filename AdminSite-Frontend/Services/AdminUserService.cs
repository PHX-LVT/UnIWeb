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

        public Task<ApiResponse<List<AdminRoleResponse>>> GetRolesAsync() =>
            _http.GetAsync<List<AdminRoleResponse>>("api/admin/roles");

        public Task<ApiResponse<AdminRoleResponse>> CreateRoleAsync(AdminRoleCreateRequest request) =>
            _http.PostAsync<AdminRoleResponse>("api/admin/roles", request);

        public Task<ApiResponse<AdminRoleImpactResponse>> GetRoleImpactAsync(string id, AdminRoleUpdateRequest request) =>
            _http.PostAsync<AdminRoleImpactResponse>($"api/admin/roles/{id}/impact", request);

        public Task<ApiResponse<AdminRoleResponse>> UpdateRoleAsync(string id, AdminRoleUpdateRequest request) =>
            _http.PutAsync<AdminRoleResponse>($"api/admin/roles/{id}", request);

        public Task<ApiResponse<long>> DeleteRoleAsync(string id, string? replacementRoleId) =>
            _http.PostAsync<long>($"api/admin/roles/{id}/delete", new AdminRoleDeleteRequest
            {
                ReplacementRoleId = replacementRoleId
            });

        public Task<ApiResponse<AdminPagedResponse<AdminSessionResponse>>> GetSessionsAsync(int page, int pageSize, string? adminId = null)
        {
            var query = $"?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(adminId))
                query += $"&adminId={Uri.EscapeDataString(adminId)}";
            return _http.GetAsync<AdminPagedResponse<AdminSessionResponse>>($"api/admin/users/sessions{query}");
        }

        public Task<ApiResponse<long>> DeleteSessionsAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/sessions/delete", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<AdminPagedResponse<AdminRememberedDeviceResponse>>> GetRememberedDevicesAsync(
            int page,
            int pageSize,
            string? adminId = null)
        {
            var query = $"?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(adminId))
                query += $"&adminId={Uri.EscapeDataString(adminId)}";
            return _http.GetAsync<AdminPagedResponse<AdminRememberedDeviceResponse>>(
                $"api/admin/users/remembered-devices{query}");
        }

        public Task<ApiResponse<long>> RevokeRememberedDevicesAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/remembered-devices/revoke", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<List<AdminSessionResponse>>> GetMySessionsAsync() =>
            _http.GetAsync<List<AdminSessionResponse>>("api/admin/users/me/sessions");

        public Task<ApiResponse<List<AdminRememberedDeviceResponse>>> GetMyRememberedDevicesAsync() =>
            _http.GetAsync<List<AdminRememberedDeviceResponse>>("api/admin/users/me/remembered-devices");

        public Task<ApiResponse<long>> RevokeMyRememberedDevicesAsync(IEnumerable<string> ids) =>
            _http.PostAsync<long>("api/admin/users/me/remembered-devices/revoke", new AdminBulkDeleteRequest
            {
                Ids = ids.ToList()
            });

        public Task<ApiResponse<string>> SignOutAllMyDevicesAsync() =>
            _http.PostAsync<string>("api/admin/users/me/sign-out-all", new { });

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
