using AdminSite.Services.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminSite.Pages.Auth;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminLogoutModel : PageModel
{
    private readonly AdminApiAuthenticationClient _authenticationClient;
    private readonly AdminSessionInvalidationService _invalidations;

    public AdminLogoutModel(
        AdminApiAuthenticationClient authenticationClient,
        AdminSessionInvalidationService invalidations)
    {
        _authenticationClient = authenticationClient;
        _invalidations = invalidations;
    }

    public IActionResult OnGet() => LocalRedirect(Url.Content("~/"));

    public async Task<IActionResult> OnPostAsync(
        string? reason,
        bool revokeCurrent = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = AdminSessionInvalidationService.NormalizeReason(reason);
        var token = AdminAuthConstants.GetApiToken(User);
        var adminId = AdminAuthConstants.GetAdminId(User);
        var tokenId = AdminAuthConstants.GetTokenId(User);
        var rememberedDeviceCredential = Request.Cookies[AdminAuthConstants.RememberedDeviceCookieName];

        // Expire the browser ticket regardless of API availability. The API
        // revocation attempt below is deliberately bounded to keep logout responsive.
        await HttpContext.SignOutAsync(AdminAuthConstants.Scheme);

        var accessRevoked = false;
        if (revokeCurrent)
        {
            Response.Cookies.Delete(
                AdminAuthConstants.RememberedDeviceCookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Path = "/"
                });

            if (!string.IsNullOrWhiteSpace(token))
                accessRevoked = await _authenticationClient.TryLogoutAsync(
                    token,
                    rememberedDeviceCredential,
                    cancellationToken);
            else
                await _authenticationClient.TryRevokeRememberedDeviceAsync(
                    rememberedDeviceCredential,
                    cancellationToken);
        }

        // Notify other live circuits only after the API has revoked both the
        // session and remembered-device credential. Publishing earlier can make
        // this browser submit a second, non-revoking logout and cancel this response.
        if (accessRevoked &&
            !string.IsNullOrWhiteSpace(adminId) &&
            !string.IsNullOrWhiteSpace(tokenId))
        {
            _invalidations.InvalidateToken(adminId, tokenId, normalizedReason);
        }

        var loginUrl = Url.Page("/AdminLogin", values: new { reason = normalizedReason })
                       ?? $"/login?reason={Uri.EscapeDataString(normalizedReason)}";
        return LocalRedirect(loginUrl);
    }
}
