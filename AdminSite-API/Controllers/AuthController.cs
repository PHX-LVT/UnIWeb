using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        // POST api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(ApiResult.BadRequest(
                    "Email and password are required."));

            var result = await _service.LoginAsync(dto.Email, dto.Password);
            if (result is null)
                return Unauthorized(ApiResult.Unauthorized<LoginResponseDto>(
                    "Invalid email or password."));

            return Ok(ApiResult.Ok(result, "Login successful."));
        }
     
        [Authorize]
        [HttpPut("PasswordUpdate")]
        public async Task<IActionResult> UpdatePassword([FromBody] PasswordUpdateDto dto)
        {
            var adminId = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminId))
                return Unauthorized(ApiResult.Unauthorized<string>("Invalid session."));

            var admin = await _service.GetByIdAsync(adminId);
            if (admin is null)
                return Unauthorized(ApiResult.Unauthorized<string>("Admin not found."));
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(ApiResult.BadRequest("Current password is required."));
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                return BadRequest(ApiResult.BadRequest("New password must be at least 8 characters."));

            var isValid = _service.VerifyPassword(dto.CurrentPassword, admin.PasswordHash);
            if (!isValid)
                return BadRequest(ApiResult.BadRequest("Current password is incorrect."));

            var newHash = _service.HashPassword(dto.NewPassword);
            await _service.UpdatePasswordAsync(admin.Id, newHash);
           
            return Ok(ApiResult.Ok("Password updated successfully."));
        }

        // POST api/auth/logout
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // JWT is stateless — client discards the token
            // Future: add token to a blocklist if needed
            return Ok(ApiResult.Ok("Logged out successfully."));
        }

        // GET api/auth/session
        [HttpGet("session")]
        [Authorize]
        public async Task<IActionResult> Session()
        {
            var adminId = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized(ApiResult.Unauthorized<SessionResponseDto>());

            var admin = await _service.GetByIdAsync(adminId);
            if (admin is null)
                return Unauthorized(ApiResult.Unauthorized<SessionResponseDto>());

            return Ok(ApiResult.Ok(new SessionResponseDto
            {
                Valid = true,
                AdminId = admin.Id,
                Email = admin.Email
            }));
        }
    }
}
