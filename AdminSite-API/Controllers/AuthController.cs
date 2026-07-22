using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _service;
        private readonly AdminRoleService _roles;

        public AuthController(AuthService service, AdminRoleService roles)
        {
            _service = service;
            _roles = roles;
        }

        [AllowAnonymous]
        [EnableRateLimiting("admin-login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResult.BadRequest("Email and password are required."));

            var result = await _service.LoginAsync(
                dto.Email,
                dto.Password,
                dto.RememberDevice,
                dto.ExistingRememberedDeviceCredential,
                ClientIp,
                UserAgent);
            if (result is null)
                return Unauthorized(ApiResult.Unauthorized<LoginResponseDto>("Invalid email or password."));

            return Ok(ApiResult.Ok(result, "Login successful."));
        }

        [AllowAnonymous]
        [EnableRateLimiting("admin-remembered-device")]
        [HttpPost("remembered-device/exchange")]
        public async Task<IActionResult> ExchangeRememberedDevice(
            [FromBody] RememberedDeviceExchangeRequest dto,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.Credential))
                return Unauthorized(ApiResult.Unauthorized<LoginResponseDto>("Remembered device is invalid."));

            var result = await _service.ExchangeRememberedDeviceAsync(
                dto.Credential,
                ClientIp,
                UserAgent,
                cancellationToken);
            if (result.Login is null)
                return Unauthorized(ApiResult.Unauthorized<LoginResponseDto>("Remembered device is invalid or expired."));

            return Ok(ApiResult.Ok(result.Login, "Remembered device authenticated."));
        }

        [AllowAnonymous]
        [EnableRateLimiting("admin-remembered-device")]
        [HttpPost("remembered-device/revoke")]
        public async Task<IActionResult> RevokeRememberedDevice(
            [FromBody] RememberedDeviceExchangeRequest dto,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(dto.Credential))
            {
                _ = await _service.RevokeRememberedDeviceCredentialAsync(
                    dto.Credential,
                    "self",
                    AdminRememberedDeviceRevokeReason.UserRequested,
                    cancellationToken: cancellationToken);
            }

            // Do not disclose whether a bearer credential existed.
            return Ok(ApiResult.Ok("Remembered device cleared."));
        }

        [Authorize]
        [HttpPut("password")]
        [HttpPut("PasswordUpdate")]
        public async Task<IActionResult> UpdatePassword([FromBody] PasswordUpdateDto dto)
        {
            var admin = await CurrentAdminAsync();
            if (admin is null)
                return Unauthorized(ApiResult.Unauthorized<string>("Invalid session."));

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(ApiResult.BadRequest("Current password is required."));
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                return BadRequest(ApiResult.BadRequest("New password must be at least 8 characters."));

            var isValid = _service.VerifyPassword(dto.CurrentPassword, admin.PasswordHash);
            if (!isValid)
                return BadRequest(ApiResult.BadRequest("Current password is incorrect."));

            await _service.UpdateOwnPasswordAsync(admin, _service.HashPassword(dto.NewPassword), ClientIp, UserAgent);
            return Ok(ApiResult.Ok("Password updated successfully. Please sign in again."));
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest? request)
        {
            var adminId = User.FindFirst("adminId")?.Value ?? string.Empty;
            var tokenId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ??
                          User.FindFirst("jti")?.Value ??
                          string.Empty;

            await _service.LogoutAsync(
                adminId,
                tokenId,
                request?.RememberedDeviceCredential,
                ClientIp,
                UserAgent);
            return Ok(ApiResult.Ok("Logged out successfully."));
        }

        [HttpGet("session")]
        [Authorize]
        public async Task<IActionResult> Session()
        {
            var admin = await CurrentAdminAsync();
            if (admin is null)
                return Unauthorized(ApiResult.Unauthorized<SessionResponseDto>());

            AuthService.NormalizeUserDefaults(admin);
            var role = await _roles.GetRoleForUserAsync(admin);
            if (role is null)
                return Unauthorized(ApiResult.Unauthorized<SessionResponseDto>("Assigned role is unavailable."));
            return Ok(ApiResult.Ok(new SessionResponseDto
            {
                Valid = true,
                AdminId = admin.Id,
                Email = admin.Email,
                FullName = string.IsNullOrWhiteSpace(admin.FullName) ? admin.Email : admin.FullName,
                RoleId = role.Id,
                RoleName = role.Name,
                IsAdminAdmin = role.IsProtected,
                Status = admin.Status,
                Permissions = await _service.GetEffectivePermissionsAsync(admin)
            }));
        }

        private async Task<FullProject.Models.AdminUser?> CurrentAdminAsync()
        {
            var adminId = User.FindFirst("adminId")?.Value;
            return string.IsNullOrWhiteSpace(adminId)
                ? null
                : await _service.GetByIdAsync(adminId);
        }

        private string ClientIp =>
            HttpContext.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } ip && ip != "::1"
                ? ip
                : "127.0.0.1";

        private string UserAgent =>
            Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
    }
}
