using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AdminSite.Services.Authentication;

public sealed class AdminCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly AdminApiAuthenticationClient _api;
    private readonly AdminSessionInvalidationService _invalidations;

    public AdminCookieAuthenticationEvents(
        AdminApiAuthenticationClient api,
        AdminSessionInvalidationService invalidations)
    {
        _api = api;
        _invalidations = invalidations;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // The sign-out endpoint must remain reachable with a recently revoked
        // ticket so it can validate its antiforgery token and expire the cookie.
        if (context.Request.Path.StartsWithSegments("/auth/logout"))
            return;

        var principal = context.Principal;
        if (principal is null)
        {
            await RejectAsync(context, null, null, "session-expired");
            return;
        }

        var token = AdminAuthConstants.GetApiToken(principal);
        var adminId = AdminAuthConstants.GetAdminId(principal);
        var tokenId = AdminAuthConstants.GetTokenId(principal);

        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(adminId) ||
            string.IsNullOrWhiteSpace(tokenId) ||
            !AdminAuthConstants.TryReadJwtMetadata(
                token,
                out _,
                out var jwtTokenId,
                out var jwtAdminId) ||
            !string.Equals(tokenId, jwtTokenId, StringComparison.Ordinal) ||
            !string.Equals(adminId, jwtAdminId, StringComparison.Ordinal))
        {
            await RejectAsync(context, adminId, tokenId, "session-expired");
            return;
        }

        var check = await _api.CheckSessionAsync(token, context.HttpContext.RequestAborted);
        if (check.Status == AdminSessionCheckStatus.Invalid)
            await RejectAsync(context, adminId, tokenId, "session-expired");
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsBlazorTransport(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsBlazorTransport(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(
            $"{context.Request.PathBase}{context.Options.LoginPath}?reason=access-denied");
        return Task.CompletedTask;
    }

    private async Task RejectAsync(
        CookieValidatePrincipalContext context,
        string? adminId,
        string? tokenId,
        string reason)
    {
        context.RejectPrincipal();
        if (!string.IsNullOrWhiteSpace(adminId) && !string.IsNullOrWhiteSpace(tokenId))
            _invalidations.InvalidateToken(adminId, tokenId, reason);

        await context.HttpContext.SignOutAsync(AdminAuthConstants.Scheme);
    }

    private static bool IsBlazorTransport(PathString path) =>
        path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase);
}
