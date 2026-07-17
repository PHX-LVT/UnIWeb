using FullProject.Models;
using FullProject.Security;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers;

[ApiController]
[Route("api/admin/roles")]
[Authorize]
public sealed class AdminRolesController : ControllerBase
{
    private readonly AdminRoleService _roles;
    private readonly AuthService _auth;

    public AdminRolesController(AdminRoleService roles, AuthService auth)
    {
        _roles = roles;
        _auth = auth;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        if (!AdminAuthorization.HasPermission(User, AdminPermissionKeys.ManageUsers)) return Forbid();

        var roles = await _roles.GetRolesAsync();
        var response = new List<AdminRoleResponse>();
        foreach (var role in roles) response.Add(await MapAsync(role));
        return Ok(ApiResult.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] AdminRoleCreateRequest request)
    {
        var actor = await RequireAdminAdminAsync();
        if (actor is null) return Forbid();

        var (role, errors) = await _roles.CreateAsync(request, actor, ClientIp, UserAgent);
        if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<AdminRoleResponse>(errors));
        return Ok(ApiResult.Created(await MapAsync(role!), "Role created."));
    }

    [HttpPost("{id}/impact")]
    public async Task<IActionResult> GetUpdateImpact(string id, [FromBody] AdminRoleUpdateRequest request)
    {
        var actor = await RequireAdminAdminAsync();
        if (actor is null) return Forbid();

        var role = await _roles.GetByIdAsync(id);
        if (role is null) return NotFound(ApiResult.NotFound("Role not found."));
        var next = AdminRoleService.NormalizePermissions(request.Permissions);
        var current = AdminRoleService.NormalizePermissions(role.Permissions);
        return Ok(ApiResult.Ok(new AdminRoleImpactResponse
        {
            RoleId = role.Id,
            RoleName = role.Name,
            AffectedUsers = await _roles.CountUsersAsync(role.Id),
            AddedPermissions = next.Except(current, StringComparer.OrdinalIgnoreCase).ToList(),
            RemovedPermissions = current.Except(next, StringComparer.OrdinalIgnoreCase).ToList()
        }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] AdminRoleUpdateRequest request)
    {
        var actor = await RequireAdminAdminAsync();
        if (actor is null) return Forbid();

        var (role, errors, affectedUsers) = await _roles.UpdateAsync(id, request, actor, ClientIp, UserAgent);
        if (errors.Count > 0) return ToError(errors);
        return Ok(ApiResult.Ok(await MapAsync(role!),
            affectedUsers > 0
                ? $"Role updated. {affectedUsers} affected user session(s) were revoked."
                : "Role updated."));
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task<IActionResult> DeleteRole(string id, [FromBody] AdminRoleDeleteRequest? request)
    {
        var actor = await RequireAdminAdminAsync();
        if (actor is null) return Forbid();

        var (deleted, errors, reassignedUsers) = await _roles.DeleteAsync(
            id,
            request?.ReplacementRoleId,
            actor,
            ClientIp,
            UserAgent);
        if (!deleted) return ToError(errors);
        return Ok(ApiResult.Ok(reassignedUsers,
            reassignedUsers > 0
                ? $"Role deleted and {reassignedUsers} user(s) reassigned."
                : "Role deleted."));
    }

    private async Task<AdminUser?> RequireAdminAdminAsync()
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return null;
        var adminId = User.FindFirst("adminId")?.Value;
        return string.IsNullOrWhiteSpace(adminId) ? null : await _auth.GetByIdAsync(adminId);
    }

    private async Task<AdminRoleResponse> MapAsync(AdminRoleDefinition role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Permissions = AdminRoleService.NormalizePermissions(role.Permissions),
        IsProtected = role.IsProtected,
        IsSystem = role.IsSystem,
        IsDeleting = role.IsDeleting,
        UserCount = await _roles.CountUsersAsync(role.Id),
        CreatedAt = role.CreatedAt,
        UpdatedAt = role.UpdatedAt
    };

    private ObjectResult ToError(List<string> errors)
    {
        if (errors.Contains("Role not found.")) return NotFound(ApiResult.NotFound("Role not found."));
        return UnprocessableEntity(ApiResult.Unprocessable<object>(errors));
    }

    private string ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } ip && ip != "::1"
            ? ip
            : "127.0.0.1";

    private string UserAgent => Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
}
