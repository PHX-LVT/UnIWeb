using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AdminSite.Services.Authentication;
using Contracts.Auth;

namespace AdminSite.ComponentTests;

public sealed class AdminAuthenticationPolicyTests
{
    [Fact]
    public void PrincipalRoundTrip_PreservesSessionAndRememberedDeviceIdentity()
    {
        var login = Login();
        var principal = AdminAuthConstants.CreatePrincipal(login, "token-1", "device-1");

        var session = AdminAuthConstants.ToAdminSession(principal);

        Assert.NotNull(session);
        Assert.Equal(login.AdminId, session.AdminId);
        Assert.Equal(login.Email, session.Email);
        Assert.Equal(login.RoleId, session.RoleId);
        Assert.Equal("token-1", session.TokenId);
        Assert.Equal("device-1", session.RememberedDeviceId);
        Assert.Equal(AdminUserStatus.Active, session.Status);
        Assert.Contains(AdminPermissionKeys.ViewContent, session.Permissions);
    }

    [Fact]
    public void ToAdminSession_RejectsUnauthenticatedPrincipal() =>
        Assert.Null(AdminAuthConstants.ToAdminSession(new ClaimsPrincipal(new ClaimsIdentity())));

    [Fact]
    public void ToAdminSession_RejectsPrincipalWithoutTokenIdentity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(AdminAuthConstants.AdminIdClaim, "admin-1"),
                new Claim(ClaimTypes.Email, "admin@example.test")
            ],
            AdminAuthConstants.Scheme));

        Assert.Null(AdminAuthConstants.ToAdminSession(principal));
    }

    [Fact]
    public void GetApiToken_ReturnsTokenOnlyForAuthenticatedPrincipal()
    {
        var principal = AdminAuthConstants.CreatePrincipal(Login(), "token-1");

        Assert.Equal("jwt-value", AdminAuthConstants.GetApiToken(principal));
        Assert.Null(AdminAuthConstants.GetApiToken(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void TryReadJwtMetadata_ReadsUnexpiredRequiredClaims()
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var token = Jwt(expires, "token-1", "admin-1");

        var result = AdminAuthConstants.TryReadJwtMetadata(token, out var actualExpiry, out var tokenId, out var adminId);

        Assert.True(result);
        Assert.Equal(expires.ToUnixTimeSeconds(), actualExpiry.ToUnixTimeSeconds());
        Assert.Equal("token-1", tokenId);
        Assert.Equal("admin-1", adminId);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("one.two.three.four")]
    [InlineData("e30.e30.signature")]
    public void TryReadJwtMetadata_RejectsMalformedOrIncompletePayload(string token) =>
        Assert.False(AdminAuthConstants.TryReadJwtMetadata(token, out _, out _, out _));

    [Fact]
    public void TryReadJwtMetadata_RejectsExpiredToken()
    {
        var token = Jwt(DateTimeOffset.UtcNow.AddMinutes(-1), "token-1", "admin-1");

        Assert.False(AdminAuthConstants.TryReadJwtMetadata(token, out _, out _, out _));
    }

    private static LoginResponse Login() => new()
    {
        Token = "jwt-value",
        AdminId = "admin-1",
        Email = "admin@example.test",
        FullName = "Admin User",
        RoleId = "role-1",
        RoleName = "Editor",
        Status = AdminUserStatus.Active,
        Permissions = [AdminPermissionKeys.CreateEditContent]
    };

    private static string Jwt(DateTimeOffset expires, string tokenId, string adminId)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            exp = expires.ToUnixTimeSeconds(),
            jti = tokenId,
            adminId
        }));
        return $"{header}.{payload}.signature";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
