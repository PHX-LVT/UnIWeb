using Contracts.Auth;
using System.Security.Claims;

namespace FullProject.Security;

public static class AdminAuthorization
{
    public static bool HasPermission(ClaimsPrincipal user, string permission) =>
        IsAdminAdmin(user) || user.Claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));

    public static bool CanUsePageBuilder(ClaimsPrincipal user) =>
        HasPermission(user, AdminPermissionKeys.PageBuilder);

    private static bool IsAdminAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirst(ClaimTypes.Role)?.Value, AdminRole.AdminAdmin.ToString(), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(user.FindFirst("role")?.Value, AdminRole.AdminAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
}
