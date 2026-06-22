using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize]
    public class AdminUsersController : ControllerBase
    {
        private readonly AuthService _auth;

        public AdminUsersController(AuthService auth)
        {
            _auth = auth;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();

            var users = await _auth.GetUsersAsync();
            return Ok(ApiResult.Ok(users.Select(MapUser).ToList()));
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminUserCreateRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();

            var (user, errors) = await _auth.CreateUserAsync(dto, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<AdminUserResponse>(errors));

            return Ok(ApiResult.Created(MapUser(user!), "Admin user created."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] AdminUserUpdateRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();

            var (user, errors) = await _auth.UpdateUserAsync(id, dto, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(MapUser(user!), "Admin user updated."));
        }

        [HttpPost("{id}/enable")]
        public async Task<IActionResult> EnableUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();

            var (user, errors) = await _auth.SetUserEnabledAsync(id, true, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(MapUser(user!), "Admin user enabled."));
        }

        [HttpPost("{id}/disable")]
        public async Task<IActionResult> DisableUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();
            if (actor!.Id == id) return BadRequest(ApiResult.BadRequest("You cannot disable your own account."));

            var (user, errors) = await _auth.SetUserEnabledAsync(id, false, actor, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(MapUser(user!), "Admin user disabled."));
        }

        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminPasswordResetRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers(actor)) return Forbid();

            var (user, errors) = await _auth.ResetPasswordAsync(id, dto.NewPassword, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(MapUser(user!), "Password reset. Existing sessions were revoked."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin(actor)) return Forbid();

            var (user, errors) = await _auth.DeleteUserAsync(id, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(MapUser(user!), "Admin account deleted. Existing sessions were revoked."));
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(
            [FromQuery] string? adminId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var actor = await CurrentAdminAsync();
            if (!CanViewLogs(actor)) return Forbid();

            var result = await _auth.GetSessionsPageAsync(page, pageSize, adminId);
            var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
            return Ok(ApiResult.Ok(new AdminPagedResponse<AdminSessionResponse>
            {
                Items = result.Items.Select(MapSession).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = totalPages
            }));
        }

        [HttpGet("login-activity")]
        public async Task<IActionResult> GetLoginActivity(
            [FromQuery] string? adminId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var actor = await CurrentAdminAsync();
            if (!CanViewLogs(actor)) return Forbid();

            var result = await _auth.GetLoginActivityPageAsync(page, pageSize, adminId);
            var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
            return Ok(ApiResult.Ok(new AdminPagedResponse<AdminLoginActivityResponse>
            {
                Items = result.Items.Select(MapLoginActivity).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = totalPages
            }));
        }

        [HttpPost("sessions/delete")]
        public async Task<IActionResult> DeleteSessions([FromBody] AdminBulkDeleteRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin(actor)) return Forbid();

            var count = await _auth.DeleteSessionsAsync(dto.Ids, actor!, ClientIp, UserAgent);
            return Ok(ApiResult.Ok(count, $"Deleted {count} inactive session record(s)."));
        }

        [HttpPost("login-activity/delete")]
        public async Task<IActionResult> DeleteLoginActivity([FromBody] AdminBulkDeleteRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin(actor)) return Forbid();

            var count = await _auth.DeleteLoginActivityAsync(dto.Ids, actor!, ClientIp, UserAgent);
            return Ok(ApiResult.Ok(count, $"Deleted {count} login activity log(s)."));
        }

        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? targetId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var actor = await CurrentAdminAsync();
            if (!CanViewLogs(actor)) return Forbid();

            var result = await _auth.GetAuditLogsPageAsync(page, pageSize, targetId);
            var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
            return Ok(ApiResult.Ok(new AdminPagedResponse<AdminAuditLogResponse>
            {
                Items = result.Items.Select(MapAudit).ToList(),
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = totalPages
            }));
        }

        [HttpPost("audit/delete")]
        public async Task<IActionResult> DeleteAuditLogs([FromBody] AdminBulkDeleteRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin(actor)) return Forbid();

            var count = await _auth.DeleteAuditLogsAsync(dto.Ids, actor!, ClientIp, UserAgent);
            return Ok(ApiResult.Ok(count, $"Deleted {count} audit log(s)."));
        }

        [HttpGet("me/sessions")]
        public async Task<IActionResult> GetMySessions()
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<List<AdminSessionResponse>>());

            var sessions = await _auth.GetSessionsAsync(actor.Id);
            return Ok(ApiResult.Ok(sessions.Select(MapSession).ToList()));
        }

        [HttpGet("me/login-activity")]
        public async Task<IActionResult> GetMyLoginActivity()
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<List<AdminLoginActivityResponse>>());

            var activity = await _auth.GetLoginActivityAsync(actor.Id);
            return Ok(ApiResult.Ok(activity.Select(MapLoginActivity).ToList()));
        }

        [HttpGet("me/audit")]
        public async Task<IActionResult> GetMyAuditLogs()
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<List<AdminAuditLogResponse>>());

            var logs = await _auth.GetAuditLogsAsync(actor.Id);
            return Ok(ApiResult.Ok(logs.Select(MapAudit).ToList()));
        }

        private async Task<AdminUser?> CurrentAdminAsync()
        {
            var adminId = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminId)) return null;

            var admin = await _auth.GetByIdAsync(adminId);
            if (admin is not null) AuthService.NormalizeUserDefaults(admin);
            return admin;
        }

        private static bool CanManageUsers(AdminUser? user) =>
            user is not null &&
            (user.Role == AdminRole.AdminAdmin ||
             AuthService.GetEffectivePermissions(user).Contains(AdminPermissionKeys.ManageUsers));

        private static bool CanViewLogs(AdminUser? user) =>
            user is not null &&
            (user.Role == AdminRole.AdminAdmin ||
             AuthService.GetEffectivePermissions(user).Contains(AdminPermissionKeys.ViewLogs));

        private static bool IsAdminAdmin(AdminUser? user) =>
            user?.Role == AdminRole.AdminAdmin;

        private ObjectResult ToErrorResult<T>(List<string> errors)
        {
            if (errors.Contains("User not found."))
                return NotFound(ApiResult.NotFound("User not found."));
            return UnprocessableEntity(ApiResult.Unprocessable<T>(errors));
        }

        private static AdminUserResponse MapUser(AdminUser user)
        {
            AuthService.NormalizeUserDefaults(user);
            return new AdminUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                Status = user.Status,
                Permissions = user.Permissions,
                TokenVersion = user.TokenVersion,
                FailedLoginAttempts = user.FailedLoginAttempts,
                LockedUntil = user.LockedUntil,
                LastLoginAt = user.LastLoginAt,
                LastLoginIp = user.LastLoginIp,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        private static AdminSessionResponse MapSession(AdminSessionRecord session) => new()
        {
            Id = session.Id,
            AdminId = session.AdminId,
            Email = session.Email,
            TokenId = session.TokenId,
            LoginAt = session.LoginAt,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            BrowserName = session.BrowserName,
            OperatingSystem = session.OperatingSystem,
            IsRevoked = session.IsRevoked,
            RevokedAt = session.RevokedAt,
            RevokeReason = session.RevokeReason
        };

        private static AdminLoginActivityResponse MapLoginActivity(AdminLoginActivityRecord activity) => new()
        {
            Id = activity.Id,
            AdminId = activity.AdminId,
            Email = activity.Email,
            EventType = activity.EventType,
            Success = activity.Success,
            Message = activity.Message,
            IpAddress = activity.IpAddress,
            UserAgent = activity.UserAgent,
            BrowserName = activity.BrowserName,
            OperatingSystem = activity.OperatingSystem,
            OccurredAt = activity.OccurredAt
        };

        private static AdminAuditLogResponse MapAudit(AdminAuditLog log) => new()
        {
            Id = log.Id,
            Area = log.Area,
            Action = log.Action,
            ActorId = log.ActorId,
            ActorEmail = log.ActorEmail,
            TargetId = log.TargetId,
            TargetEmail = log.TargetEmail,
            Message = log.Message,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CreatedAt = log.CreatedAt
        };

        private string ClientIp =>
            HttpContext.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } ip && ip != "::1"
                ? ip
                : "127.0.0.1";

        private string UserAgent =>
            Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
    }
}
