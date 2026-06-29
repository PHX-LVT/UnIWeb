using Contracts.Auth;
using System.Security.Claims;

namespace FullProject.Security;

public static class AdminAuthorization
{
    public static bool HasPermission(ClaimsPrincipal user, string permission) =>
        IsAdminAdmin(user) || user.Claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase)) ||
        HasDefaultRolePermission(user, permission);

    public static bool CanUsePageBuilder(ClaimsPrincipal user) =>
        HasPermission(user, AdminPermissionKeys.PageBuilder);

    private static bool IsAdminAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst(ClaimTypes.Role)?.Value, AdminRole.AdminAdmin.ToString(), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(user.FindFirst("role")?.Value, AdminRole.AdminAdmin.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool HasDefaultRolePermission(ClaimsPrincipal user, string permission)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
        if (string.Equals(role, AdminRole.Manager.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ManagerDefaults.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        if (string.Equals(role, AdminRole.Writer.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return WriterDefaults.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }

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
