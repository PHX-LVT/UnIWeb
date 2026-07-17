using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace AdminSite.Services.Authentication;

public sealed class AdminRevalidatingAuthenticationStateProvider
    : RevalidatingServerAuthenticationStateProvider
{
    private readonly AdminApiAuthenticationClient _api;
    private readonly AdminSessionInvalidationService _invalidations;
    private readonly TimeSpan _revalidationInterval;

    public AdminRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        AdminApiAuthenticationClient api,
        AdminSessionInvalidationService invalidations)
        : base(loggerFactory)
    {
        _api = api;
        _invalidations = invalidations;
        var configuredSeconds = configuration.GetValue("Authentication:RevalidationSeconds", 30);
        _revalidationInterval = TimeSpan.FromSeconds(Math.Clamp(configuredSeconds, 15, 300));
    }

    protected override TimeSpan RevalidationInterval => _revalidationInterval;

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        var principal = authenticationState.User;
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
            PublishInvalidation(adminId, tokenId);
            return false;
        }

        var check = await _api.CheckSessionAsync(token, cancellationToken);
        if (check.Status == AdminSessionCheckStatus.Invalid)
        {
            PublishInvalidation(adminId, tokenId);
            return false;
        }

        // A temporary API/network failure must not cause a mass sign-out. Protected
        // API operations remain unavailable, and the next interval tries again.
        return true;
    }

    private void PublishInvalidation(string? adminId, string? tokenId)
    {
        if (!string.IsNullOrWhiteSpace(adminId) && !string.IsNullOrWhiteSpace(tokenId))
            _invalidations.InvalidateToken(adminId, tokenId, "session-expired");
    }
}
