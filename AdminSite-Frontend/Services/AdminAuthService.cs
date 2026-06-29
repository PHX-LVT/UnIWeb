using AdminSite.Models;
using Blazored.LocalStorage;

namespace AdminSite.Services
{
    public class AdminAuthService
    {
        private readonly IHttpService _http;
        private readonly ILocalStorageService _storage;

        public AdminSession? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser is not null
                                    && !string.IsNullOrEmpty(CurrentUser.Token);

        public AdminAuthService(IHttpService http, ILocalStorageService storage)
        {
            _http = http;
            _storage = storage;
        }

        // Called on every first render to restore session from localStorage
        public async Task InitializeAsync()
        {
            CurrentUser = await _storage.GetItemAsync<AdminSession>("admin_session");
        }

        public async Task<(bool Success, string? Error)> LoginAsync(
            string email, string password)
        {
            CurrentUser = null;
            await _storage.RemoveItemAsync("admin_session");

            var response = await _http.PostAsync<LoginResponse>(
                "api/auth/login",
                new LoginRequest { Email = email, Password = password });

            if (!response.Success || response.Data is null)
                return (false, response.Message ?? "Login failed.");

            CurrentUser = new AdminSession
            {
                AdminId = response.Data.AdminId,
                Email = response.Data.Email,
                FullName = response.Data.FullName,
                Role = response.Data.Role,
                Status = response.Data.Status,
                Permissions = response.Data.Permissions,
                Token = response.Data.Token
            };

            await _storage.SetItemAsync("admin_session", CurrentUser);
            return (true, null);
        }

        public async Task LogoutAsync()
        {
            if (IsAuthenticated)
                await _http.PostAsync<string>("api/auth/logout", new { });

            CurrentUser = null;
            await _storage.RemoveItemAsync("admin_session");
        }

        public async Task RefreshSessionAsync()
        {
            if (!IsAuthenticated) return;

            var response = await _http.GetAsync<SessionResponse>("api/auth/session");
            if (!response.Success || response.Data is null) return;

            CurrentUser!.Email = response.Data.Email;
            CurrentUser.FullName = response.Data.FullName;
            CurrentUser.Role = response.Data.Role;
            CurrentUser.Status = response.Data.Status;
            CurrentUser.Permissions = response.Data.Permissions;

            await _storage.SetItemAsync("admin_session", CurrentUser);
        }

        public bool HasPermission(string permission) =>
            CurrentUser is not null &&
            !(CurrentUser.Role == AdminRole.Viewer && IsFormManagementPermission(permission)) &&
            (CurrentUser.Role == AdminRole.AdminAdmin ||
             CurrentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
             HasDefaultRolePermission(CurrentUser.Role, permission));

        public bool IsAdminAdmin => CurrentUser?.Role == AdminRole.AdminAdmin;

        public bool IsManager => CurrentUser?.Role == AdminRole.Manager;

        public bool IsWriter => CurrentUser?.Role == AdminRole.Writer;

        public bool IsViewer => CurrentUser?.Role == AdminRole.Viewer;

        public bool CanManageAllContent => IsAdminAdmin || IsManager;

        public bool CanViewContent => IsAuthenticated;

        public bool CanUsePageBuilder => HasPermission(AdminPermissionKeys.PageBuilder);

        public bool CanManageContent =>
            CanManageAllContent ||
            HasPermission(AdminPermissionKeys.ManageContent) ||
            HasPermission(AdminPermissionKeys.PublishContent) ||
            HasPermission(AdminPermissionKeys.DeleteContent) ||
            IsWriter;

        public bool CanManageSettings => HasPermission(AdminPermissionKeys.ManageSettings);

        public bool CanManageUsers => HasPermission(AdminPermissionKeys.ManageUsers);

        public bool CanViewLogs => HasPermission(AdminPermissionKeys.ViewLogs);

        public bool CanViewFormDefinitions => HasPermission(AdminPermissionKeys.ViewFormDefinitions);

        public bool CanEditFormDefinitions => HasPermission(AdminPermissionKeys.EditFormDefinitions);

        public bool CanViewFormSubmissions => HasPermission(AdminPermissionKeys.ViewFormSubmissions);

        public bool CanManageFormSubmissions => HasPermission(AdminPermissionKeys.ManageFormSubmissions);

        public bool CanExportFormSubmissions => HasPermission(AdminPermissionKeys.ExportFormSubmissions);

        public bool CanUseFormManagement => CanViewFormDefinitions || CanEditFormDefinitions || CanViewFormSubmissions;

        private static bool HasDefaultRolePermission(AdminRole role, string permission)
        {
            if (role == AdminRole.Manager)
                return ManagerDefaults.Contains(permission, StringComparer.OrdinalIgnoreCase);
            if (role == AdminRole.Writer)
                return WriterDefaults.Contains(permission, StringComparer.OrdinalIgnoreCase);
            return false;
        }

        private static bool IsFormManagementPermission(string permission) =>
            string.Equals(permission, AdminPermissionKeys.ViewFormDefinitions, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, AdminPermissionKeys.EditFormDefinitions, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, AdminPermissionKeys.ViewFormSubmissions, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, AdminPermissionKeys.ManageFormSubmissions, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, AdminPermissionKeys.ExportFormSubmissions, StringComparison.OrdinalIgnoreCase);

        private static readonly string[] ManagerDefaults =
        [
            AdminPermissionKeys.ManageContent,
            AdminPermissionKeys.PublishContent,
            AdminPermissionKeys.DeleteContent,
            AdminPermissionKeys.ViewFormDefinitions,
            AdminPermissionKeys.EditFormDefinitions,
            AdminPermissionKeys.ViewFormSubmissions,
            AdminPermissionKeys.ManageFormSubmissions,
            AdminPermissionKeys.ExportFormSubmissions
        ];

        private static readonly string[] WriterDefaults =
        [
            AdminPermissionKeys.ManageContent,
            AdminPermissionKeys.ViewFormDefinitions,
            AdminPermissionKeys.EditFormDefinitions,
            AdminPermissionKeys.ViewFormSubmissions,
            AdminPermissionKeys.ManageFormSubmissions,
            AdminPermissionKeys.ExportFormSubmissions
        ];
    }
}
