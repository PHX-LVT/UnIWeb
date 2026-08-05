using Contracts.Admin;
using Contracts.Auth;
using System.Security.Claims;

namespace FullProject.Security;

public static class ResourceUploadContextPolicy
{
    public const string BrandRootKey = "root.brand";
    public const string BackgroundsRootKey = "root.backgrounds";
    public const string ContentRootKey = "root.content";

    public static string? Normalize(string? context) => ResourceUploadContextCodes.Normalize(context);

    public static string? RootSystemKeyFor(string uploadContext)
    {
        var normalized = Normalize(uploadContext);
        if (normalized is null || string.Equals(normalized, ResourceUploadContextCodes.LibraryManual, StringComparison.OrdinalIgnoreCase))
            return null;
        if (normalized.StartsWith("brand.", StringComparison.Ordinal)) return BrandRootKey;
        if (normalized.StartsWith("background.", StringComparison.Ordinal)) return BackgroundsRootKey;
        if (normalized.StartsWith("content.", StringComparison.Ordinal)) return ContentRootKey;
        return null;
    }

    public static bool CanUse(ClaimsPrincipal user, string? uploadContext)
    {
        var normalized = Normalize(uploadContext);
        if (normalized is null) return false;

        if (normalized.StartsWith("brand.", StringComparison.Ordinal))
            return AdminAuthorization.HasPermission(user, AdminPermissionKeys.ManageSettings);

        if (normalized.StartsWith("background.", StringComparison.Ordinal) ||
            normalized.StartsWith("content.section.", StringComparison.Ordinal) ||
            normalized.StartsWith("content.block.", StringComparison.Ordinal))
        {
            return AdminAuthorization.HasPermission(user, AdminPermissionKeys.PageBuilder);
        }

        return string.Equals(normalized, ResourceUploadContextCodes.LibraryManual, StringComparison.Ordinal) ||
               normalized.StartsWith("content.management.", StringComparison.Ordinal)
            ? CanUseContentUploads(user)
            : false;
    }

    public static bool CanUseAny(ClaimsPrincipal user) =>
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.ManageSettings) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.PageBuilder) ||
        CanUseContentUploads(user);

    private static bool CanUseContentUploads(ClaimsPrincipal user) =>
        AdminAuthorization.IsAdminAdmin(user) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.CreateEditContent) ||
        AdminAuthorization.HasPermission(user, AdminPermissionKeys.ApproveContent);
}
