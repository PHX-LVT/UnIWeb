using AdminSite.Models;
using Contracts.Auth;
using System.Security.Claims;
using System.Text.Json;

namespace AdminSite.Services.Authentication;

public static class AdminAuthConstants
{
    public const string Scheme = "AdminCookie";
    public const string CookieName = "__Host-AdminSession";
    public const string RememberedDeviceCookieName = "__Host-AdminSiteRememberedDevice";
    public const string ApiClientName = "AdminApi";
    public const string ApiUploadClientName = "AdminApiUpload";

    public const string AdminIdClaim = "adminId";
    public const string StatusClaim = "adminStatus";
    public const string PermissionClaim = "permission";
    public const string ApiTokenClaim = "adminApiToken";
    public const string TokenIdClaim = "adminTokenId";
    public const string RoleIdClaim = "adminRoleId";
    public const string RoleNameClaim = "adminRoleName";
    public const string AdminAdminClaim = "isAdminAdmin";
    public const string RememberedDeviceIdClaim = "adminRememberedDeviceId";

    public static ClaimsPrincipal CreatePrincipal(
        LoginResponse login,
        string tokenId,
        string? rememberedDeviceId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.AdminId),
            new(AdminIdClaim, login.AdminId),
            new(ClaimTypes.Email, login.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(login.FullName) ? login.Email : login.FullName),
            new(ClaimTypes.Role, login.RoleName),
            new(RoleIdClaim, login.RoleId ?? string.Empty),
            new(RoleNameClaim, login.RoleName),
            new(AdminAdminClaim, login.IsAdminAdmin ? "true" : "false"),
            new(StatusClaim, login.Status.ToString()),
            new(ApiTokenClaim, login.Token),
            new(TokenIdClaim, tokenId)
        };

        claims.AddRange(AdminPermissionKeys.ExpandDependencies(login.Permissions)
            .Select(permission => new Claim(PermissionClaim, permission)));
        if (!string.IsNullOrWhiteSpace(rememberedDeviceId))
            claims.Add(new Claim(RememberedDeviceIdClaim, rememberedDeviceId));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    public static AdminSession? ToAdminSession(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var adminId = principal.FindFirstValue(AdminIdClaim)
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var tokenId = principal.FindFirstValue(TokenIdClaim);
        if (string.IsNullOrWhiteSpace(adminId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(tokenId))
            return null;

        var status = AdminUserStatus.Disabled;
        if (!Enum.TryParse(principal.FindFirstValue(StatusClaim), true, out status) ||
            !Enum.IsDefined(status))
            status = AdminUserStatus.Disabled;

        return new AdminSession
        {
            AdminId = adminId,
            Email = email,
            FullName = principal.FindFirstValue(ClaimTypes.Name) ?? email,
            RoleId = principal.FindFirstValue(RoleIdClaim) ?? string.Empty,
            RoleName = principal.FindFirstValue(RoleNameClaim)
                       ?? principal.FindFirstValue(ClaimTypes.Role)
                       ?? string.Empty,
            IsAdminAdmin = string.Equals(principal.FindFirstValue(AdminAdminClaim), "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(principal.FindFirstValue(ClaimTypes.Role), "AdminAdmin", StringComparison.OrdinalIgnoreCase),
            Status = status,
            Permissions = AdminPermissionKeys.ExpandDependencies(principal.FindAll(PermissionClaim)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            TokenId = tokenId,
            RememberedDeviceId = principal.FindFirstValue(RememberedDeviceIdClaim)
        };
    }

    public static string? GetApiToken(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ApiTokenClaim)
            : null;

    public static string? GetAdminId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(AdminIdClaim)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetTokenId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(TokenIdClaim);

    public static bool TryReadJwtMetadata(
        string token,
        out DateTimeOffset expiresUtc,
        out string tokenId)
    {
        return TryReadJwtMetadata(token, out expiresUtc, out tokenId, out _);
    }

    public static bool TryReadJwtMetadata(
        string token,
        out DateTimeOffset expiresUtc,
        out string tokenId,
        out string adminId)
    {
        expiresUtc = default;
        tokenId = string.Empty;
        adminId = string.Empty;

        try
        {
            var segments = token.Split('.');
            if (segments.Length != 3) return false;

            var payload = segments[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = document.RootElement;
            if (!root.TryGetProperty("exp", out var expElement) ||
                !expElement.TryGetInt64(out var expSeconds) ||
                !root.TryGetProperty("jti", out var jtiElement) ||
                !root.TryGetProperty(AdminIdClaim, out var adminIdElement))
                return false;

            tokenId = jtiElement.GetString() ?? string.Empty;
            adminId = adminIdElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(adminId))
                return false;

            expiresUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            return expiresUtc > DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }
}
