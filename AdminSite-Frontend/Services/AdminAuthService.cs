using AdminSite.Models;
using AdminSite.Services.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace AdminSite.Services;

public sealed class AdminAuthService
{
    private readonly IHttpService _http;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AdminSession? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is { Status: AdminUserStatus.Active };

    public AdminAuthService(
        IHttpService http,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _http = http;
        _authenticationStateProvider = authenticationStateProvider;
    }

    // Projects the non-secret identity fields from the server authentication
    // state. No authentication material is read from browser storage.
    public async Task InitializeAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        CurrentUser = AdminAuthConstants.ToAdminSession(state.User);
    }

    public async Task RefreshSessionAsync()
    {
        await InitializeAsync();
        if (!IsAuthenticated) return;

        var response = await _http.GetAsync<SessionResponse>("api/auth/session");
        if (!response.Success || response.Data is not { Valid: true }) return;

        CurrentUser!.AdminId = response.Data.AdminId;
        CurrentUser.Email = response.Data.Email;
        CurrentUser.FullName = response.Data.FullName;
        CurrentUser.RoleId = response.Data.RoleId;
        CurrentUser.RoleName = response.Data.RoleName;
        CurrentUser.IsAdminAdmin = response.Data.IsAdminAdmin;
        CurrentUser.Status = response.Data.Status;
        CurrentUser.Permissions = response.Data.Permissions ?? new List<string>();
    }

    public bool HasPermission(string permission) =>
        CurrentUser is not null &&
        (CurrentUser.IsAdminAdmin ||
         CurrentUser.Permissions.Any(granted =>
             AdminPermissionKeys.PermissionImplies(granted, permission)));

    public bool IsAdminAdmin => CurrentUser?.IsAdminAdmin == true;

    public bool CanViewContent => HasPermission(AdminPermissionKeys.ViewContent);

    public bool CanCreateEditContent => HasPermission(AdminPermissionKeys.CreateEditContent);

    public bool CanApproveContent => HasPermission(AdminPermissionKeys.ApproveContent);

    public bool CanUsePageBuilder => HasPermission(AdminPermissionKeys.PageBuilder);

    public bool CanManageContent =>
        IsAdminAdmin || CanCreateEditContent || CanApproveContent;

    public bool CanManageContentConfiguration => IsAdminAdmin;

    public bool CanManageSettings => HasPermission(AdminPermissionKeys.ManageSettings);

    public bool CanManageUsers => HasPermission(AdminPermissionKeys.ManageUsers);

    public bool CanViewLogs => HasPermission(AdminPermissionKeys.ViewLogs);

    public bool CanViewFormDefinitions => HasPermission(AdminPermissionKeys.ViewFormDefinitions);

    public bool CanEditFormDefinitions => HasPermission(AdminPermissionKeys.EditFormDefinitions);

    public bool CanViewFormSubmissions => HasPermission(AdminPermissionKeys.ViewFormSubmissions);

    public bool CanManageFormSubmissions => HasPermission(AdminPermissionKeys.ManageFormSubmissions);

    public bool CanExportFormSubmissions => HasPermission(AdminPermissionKeys.ExportFormSubmissions);

    public bool CanAccessFormDefinitions => CanViewFormDefinitions;

    public bool CanAccessFormSubmissions => CanViewFormSubmissions;

    public bool CanUseFormManagement =>
        CanAccessFormDefinitions || CanAccessFormSubmissions;

}
