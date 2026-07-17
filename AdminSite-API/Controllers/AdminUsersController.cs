using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FullProject.Services.LogManagement;
using Contracts.Auth;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize]
    public class AdminUsersController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly AdminRoleService _roles;
        private readonly LogManagementQueryService _logs;

        public AdminUsersController(AuthService auth, AdminRoleService roles, LogManagementQueryService logs)
        {
            _auth = auth;
            _roles = roles;
            _logs = logs;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

            var users = await _auth.GetUsersAsync();
            var mapped = new List<AdminUserResponse>();
            foreach (var user in users) mapped.Add(await MapUserAsync(user));
            return Ok(ApiResult.Ok(mapped));
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] AdminUserCreateRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

            var (user, errors) = await _auth.CreateUserAsync(dto, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<AdminUserResponse>(errors));

            return Ok(ApiResult.Created(await MapUserAsync(user!), "Admin user created."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] AdminUserUpdateRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

            var (user, errors) = await _auth.UpdateUserAsync(id, dto, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(await MapUserAsync(user!), "Admin user updated."));
        }

        [HttpPost("{id}/enable")]
        public async Task<IActionResult> EnableUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

            var (user, errors) = await _auth.SetUserEnabledAsync(id, true, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(await MapUserAsync(user!), "Admin user enabled."));
        }

        [HttpPost("{id}/disable")]
        public async Task<IActionResult> DisableUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();
            if (actor!.Id == id) return BadRequest(ApiResult.BadRequest("You cannot disable your own account."));

            var (user, errors) = await _auth.SetUserEnabledAsync(id, false, actor, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(await MapUserAsync(user!), "Admin user disabled."));
        }

        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminPasswordResetRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

            var (user, errors) = await _auth.ResetPasswordAsync(id, dto.NewPassword, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(await MapUserAsync(user!), "Password reset. Existing sessions were revoked."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin()) return Forbid();

            var (user, errors) = await _auth.DeleteUserAsync(id, actor!, ClientIp, UserAgent);
            if (errors.Count > 0) return ToErrorResult<AdminUserResponse>(errors);

            return Ok(ApiResult.Ok(await MapUserAsync(user!), "Admin account deleted. Existing sessions were revoked."));
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(
            [FromQuery] string? adminId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var actor = await CurrentAdminAsync();
            if (!CanManageUsers()) return Forbid();

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
            if (!CanViewLoginActivity()) return Forbid();

            var result = await _logs.GetLoginOffsetPageAsync(page, pageSize, adminId, HttpContext.RequestAborted);
            pageSize = Math.Clamp(pageSize, 10, 100);
            var totalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)pageSize));
            return Ok(ApiResult.Ok(new AdminPagedResponse<AdminLoginActivityResponse>
            {
                Items = result.Items.Select(item => MapLoginActivityV2(item, IsAdminAdmin())).ToList(),
                Page = Math.Clamp(page, 1, totalPages),
                PageSize = pageSize,
                TotalCount = result.Total,
                TotalPages = totalPages
            }));
        }

        [HttpPost("sessions/delete")]
        public async Task<IActionResult> DeleteSessions([FromBody] AdminBulkDeleteRequest dto)
        {
            var actor = await CurrentAdminAsync();
            if (!IsAdminAdmin()) return Forbid();

            var count = await _auth.DeleteSessionsAsync(dto.Ids, actor!, ClientIp, UserAgent);
            return Ok(ApiResult.Ok(count, $"Deleted {count} inactive session record(s)."));
        }

        [HttpPost("login-activity/delete")]
        public async Task<IActionResult> DeleteLoginActivity([FromBody] AdminBulkDeleteRequest dto)
        {
            await Task.CompletedTask;
            return StatusCode(StatusCodes.Status410Gone,
                ApiResult.BadRequest("Permanent login-activity deletion was retired. Records are governed by retention policy."));
        }

        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? targetId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var actor = await CurrentAdminAsync();
            if (!CanViewAuditTrail()) return Forbid();

            var result = await _logs.GetAuditOffsetPageAsync(page, pageSize, targetId, HttpContext.RequestAborted);
            pageSize = Math.Clamp(pageSize, 10, 100);
            var totalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)pageSize));
            return Ok(ApiResult.Ok(new AdminPagedResponse<AdminAuditLogResponse>
            {
                Items = result.Items.Select(item => MapAuditV2(item, IsAdminAdmin())).ToList(),
                Page = Math.Clamp(page, 1, totalPages),
                PageSize = pageSize,
                TotalCount = result.Total,
                TotalPages = totalPages
            }));
        }

        [HttpPost("audit/delete")]
        public async Task<IActionResult> DeleteAuditLogs([FromBody] AdminBulkDeleteRequest dto)
        {
            await Task.CompletedTask;
            return StatusCode(StatusCodes.Status410Gone,
                ApiResult.BadRequest("Permanent audit deletion was retired. Audit evidence is immutable and governed by retention policy."));
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

            var activity = await _logs.GetLoginOffsetPageAsync(1, 100, actor.Id, HttpContext.RequestAborted);
            return Ok(ApiResult.Ok(activity.Items.Select(item => MapLoginActivityV2(item, true)).ToList()));
        }

        [HttpGet("me/audit")]
        public async Task<IActionResult> GetMyAuditLogs()
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<List<AdminAuditLogResponse>>());

            var logs = await _logs.GetAuditOffsetPageAsync(1, 100, actor.Id, HttpContext.RequestAborted);
            return Ok(ApiResult.Ok(logs.Items.Select(item => MapAuditV2(item, true)).ToList()));
        }

        private async Task<AdminUser?> CurrentAdminAsync()
        {
            var adminId = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminId)) return null;

            var admin = await _auth.GetByIdAsync(adminId);
            if (admin is not null) AuthService.NormalizeUserDefaults(admin);
            return admin;
        }

        private bool CanManageUsers() =>
            FullProject.Security.AdminAuthorization.HasPermission(User, AdminPermissionKeys.ManageUsers);

        private bool CanViewAuditTrail() =>
            FullProject.Security.AdminAuthorization.HasPermission(User, AdminPermissionKeys.ViewAuditTrail);

        private bool CanViewLoginActivity() =>
            FullProject.Security.AdminAuthorization.HasPermission(User, AdminPermissionKeys.ViewLoginActivity);

        private bool IsAdminAdmin() =>
            FullProject.Security.AdminAuthorization.IsAdminAdmin(User);

        private ObjectResult ToErrorResult<T>(List<string> errors)
        {
            if (errors.Contains("User not found."))
                return NotFound(ApiResult.NotFound("User not found."));
            return UnprocessableEntity(ApiResult.Unprocessable<T>(errors));
        }

        private async Task<AdminUserResponse> MapUserAsync(AdminUser user)
        {
            AuthService.NormalizeUserDefaults(user);
            var role = await _roles.GetRoleForUserAsync(user);
            var rolePermissions = AdminRoleService.NormalizePermissions(role?.Permissions);
            var extras = role is null
                ? AdminRoleService.NormalizePermissions(user.ExtraPermissions)
                : _roles.NormalizeExtraPermissions(user.ExtraPermissions.Count > 0 ? user.ExtraPermissions : user.Permissions, role);
            return new AdminUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                RoleId = role?.Id ?? string.Empty,
                RoleName = role?.Name ?? user.LegacyRole,
                IsAdminAdmin = role?.IsProtected == true,
                Status = user.Status,
                RolePermissions = rolePermissions,
                ExtraPermissions = extras,
                Permissions = await _auth.GetEffectivePermissionsAsync(user),
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

        private static AdminLoginActivityResponse MapLoginActivityV2(AdminLoginActivityEvent activity, bool includeSensitive) => new()
        {
            Id = activity.Id,
            AdminId = activity.AdminId,
            Email = string.IsNullOrWhiteSpace(activity.AccountEmail) ? activity.AccountDisplayName : activity.AccountEmail,
            EventType = activity.EventCode,
            Success = activity.Outcome == AdminAuditOutcome.Succeeded,
            Message = activity.ResultMessage,
            IpAddress = includeSensitive ? activity.IpAddress : MaskIp(activity.IpAddress),
            UserAgent = includeSensitive ? activity.UserAgent : string.Empty,
            BrowserName = activity.BrowserName,
            OperatingSystem = activity.OperatingSystem,
            OccurredAt = activity.OccurredAtUtc
        };

        private static AdminAuditLogResponse MapAuditV2(AdminAuditEvent log, bool includeSensitive) => new()
        {
            Id = log.Id,
            Area = log.DomainCode switch
            {
                "authentication" => AdminAuditArea.Auth,
                "user-management" or "role-management" => AdminAuditArea.UserManagement,
                "content" or "forms" or "page-builder" or "assets" => AdminAuditArea.Content,
                _ => AdminAuditArea.Settings
            },
            Action = log.ActionCode,
            ActorId = log.ActorId,
            ActorEmail = log.ActorEmail,
            TargetId = log.TargetId,
            TargetEmail = log.TargetLabel,
            Message = log.ResultMessage,
            IpAddress = includeSensitive ? log.IpAddress : MaskIp(log.IpAddress),
            UserAgent = includeSensitive ? log.UserAgent : string.Empty,
            CreatedAt = log.OccurredAtUtc
        };

        private static string MaskIp(string value)
        {
            var segments = value.Split('.');
            if (segments.Length == 4) return $"{segments[0]}.{segments[1]}.{segments[2]}.*";
            var separator = value.LastIndexOf(':');
            return separator > 0 ? value[..separator] + ":*" : string.IsNullOrWhiteSpace(value) ? string.Empty : "masked";
        }

        private string ClientIp =>
            HttpContext.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } ip && ip != "::1"
                ? ip
                : "127.0.0.1";

        private string UserAgent =>
            Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
    }
}
