using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _service;

        public AuthController(AuthService service)
        {
            _service = service;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResult.BadRequest("Email and password are required."));

            var result = await _service.LoginAsync(dto.Email, dto.Password, ClientIp, UserAgent);
            if (result is null)
                return Unauthorized(ApiResult.Unauthorized<LoginResponseDto>("Invalid email or password."));

            return Ok(ApiResult.Ok(result, "Login successful."));
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
        public async Task<IActionResult> Logout()
        {
            var adminId = User.FindFirst("adminId")?.Value ?? string.Empty;
            var tokenId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ??
                          User.FindFirst("jti")?.Value ??
                          string.Empty;

            await _service.LogoutAsync(adminId, tokenId, ClientIp, UserAgent);
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
            return Ok(ApiResult.Ok(new SessionResponseDto
            {
                Valid = true,
                AdminId = admin.Id,
                Email = admin.Email,
                FullName = string.IsNullOrWhiteSpace(admin.FullName) ? admin.Email : admin.FullName,
                Role = admin.Role,
                Status = admin.Status,
                Permissions = AuthService.GetEffectivePermissions(admin)
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
