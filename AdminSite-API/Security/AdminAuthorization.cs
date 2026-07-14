using Contracts.Auth;
using System.Security.Claims;

namespace FullProject.Security;

public static class AdminAuthorization
{
    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return IsAdminAdmin(user) ||
            user.Claims.Any(claim =>
                string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
                AdminPermissionKeys.PermissionImplies(claim.Value, permission));
    }

    public static bool CanUsePageBuilder(ClaimsPrincipal user) =>
        HasPermission(user, AdminPermissionKeys.PageBuilder);

    public static bool IsAdminAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst("isAdminAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(user.FindFirst(ClaimTypes.Role)?.Value, FullProject.Services.AdminRoleService.ProtectedAdminRoleName, StringComparison.OrdinalIgnoreCase);
}
