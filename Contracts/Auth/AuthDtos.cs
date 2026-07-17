namespace Contracts.Auth;

public enum AdminUserStatus
{
    Active,
    Disabled,
    Locked
}

public enum AdminSessionRevokeReason
{
    Logout,
    UserDisabled,
    PasswordChanged,
    AdminRevoked,
    RoleChanged,
    AccountDeleted
}

public enum AdminAuditArea
{
    Auth,
    UserManagement,
    Content,
    Settings
}

public enum AdminAuditOutcome
{
    Succeeded,
    Failed,
    Denied,
    Partial,
    NoChange
}

public enum AdminAuditSeverity
{
    Information,
    Warning,
    Critical
}

public static class AdminPermissionKeys
{
    public const string PageBuilder = "page-builder";
    public const string ViewContent = "view-content";
    public const string CreateEditContent = "create-edit-content";
    public const string ApproveContent = "approve-content";

    // Legacy permission keys are retained for one-way role/session migration.
    // They are intentionally absent from All and cannot be assigned again.
    public const string ManageContent = "manage-content";
    public const string PublishContent = "publish-content";
    public const string DeleteContent = "delete-content";

    public const string ManageUsers = "manage-users";
    public const string ManageSettings = "manage-settings";
    // Legacy key. Existing roles/sessions are migrated one-way by
    // ExpandDependencies and the role bootstrap process.
    public const string ViewLogs = "view-logs";
    public const string ViewAuditTrail = "view-audit-trail";
    public const string ViewLoginActivity = "view-login-activity";
    public const string ViewWebsiteActivity = "view-website-activity";
    public const string ExportLogs = "export-logs";
    public const string ViewFormDefinitions = "view-form-definitions";
    public const string EditFormDefinitions = "edit-form-definitions";
    public const string ViewFormSubmissions = "view-form-submissions";
    public const string ManageFormSubmissions = "manage-form-submissions";
    public const string ExportFormSubmissions = "export-form-submissions";

    public static readonly string[] All =
    [
        PageBuilder,
        ViewContent,
        CreateEditContent,
        ApproveContent,
        ManageUsers,
        ManageSettings,
        ViewAuditTrail,
        ViewLoginActivity,
        ViewWebsiteActivity,
        ExportLogs,
        ViewFormDefinitions,
        EditFormDefinitions,
        ViewFormSubmissions,
        ManageFormSubmissions,
        ExportFormSubmissions
    ];

    private static readonly string[] NoRequirements = [];
    private static readonly string[] ContentActionRequirements = [ViewContent];
    private static readonly string[] DefinitionEditRequirements = [ViewFormDefinitions];
    private static readonly string[] SubmissionActionRequirements = [ViewFormSubmissions];

    public static IReadOnlyList<string> GetRequiredPermissions(string permission)
    {
        if (string.Equals(permission, CreateEditContent, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, ApproveContent, StringComparison.OrdinalIgnoreCase))
            return ContentActionRequirements;

        // Active pre-migration sessions continue to resolve safely until the
        // role bootstrap revokes them and requires a fresh login.
        if (string.Equals(permission, ManageContent, StringComparison.OrdinalIgnoreCase))
            return [CreateEditContent];

        if (string.Equals(permission, PublishContent, StringComparison.OrdinalIgnoreCase))
            return [ApproveContent];

        if (string.Equals(permission, EditFormDefinitions, StringComparison.OrdinalIgnoreCase))
            return DefinitionEditRequirements;

        if (string.Equals(permission, ManageFormSubmissions, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(permission, ExportFormSubmissions, StringComparison.OrdinalIgnoreCase))
            return SubmissionActionRequirements;

        if (string.Equals(permission, ViewLogs, StringComparison.OrdinalIgnoreCase))
            return [ViewAuditTrail, ViewLoginActivity, ViewWebsiteActivity];

        return NoRequirements;
    }

    public static bool PermissionImplies(string grantedPermission, string requestedPermission)
    {
        if (string.Equals(grantedPermission, requestedPermission, StringComparison.OrdinalIgnoreCase))
            return true;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(grantedPermission);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var required in GetRequiredPermissions(current))
            {
                if (string.Equals(required, requestedPermission, StringComparison.OrdinalIgnoreCase))
                    return true;
                pending.Enqueue(required);
            }
        }

        return false;
    }

    public static List<string> ExpandDependencies(IEnumerable<string>? permissions)
    {
        var source = (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (source.Contains(ViewLogs))
        {
            source.Add(ViewAuditTrail);
            source.Add(ViewLoginActivity);
            source.Add(ViewWebsiteActivity);
        }

        var allowed = All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expanded = source
            .Where(allowed.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(expanded);

        while (pending.Count > 0)
        {
            foreach (var required in GetRequiredPermissions(pending.Dequeue()))
            {
                if (expanded.Add(required))
                    pending.Enqueue(required);
            }
        }

        return All.Where(expanded.Contains).ToList();
    }

    public static bool IsRequiredByAny(string permission, IEnumerable<string>? selectedPermissions) =>
        (selectedPermissions ?? []).Any(selected =>
            !string.Equals(selected, permission, StringComparison.OrdinalIgnoreCase) &&
            PermissionImplies(selected, permission));
}

public static class AdminExclusiveCapabilityKeys
{
    public const string ManageContentConfiguration = "manage-content-configuration";
    public const string ViewEditAnyDraft = "view-edit-any-draft";
    public const string DirectPublishDraft = "direct-publish-draft";
    public const string ForceReturnContentToDraft = "force-return-content-to-draft";
    public const string DeleteAnyEligibleContent = "delete-any-eligible-content";
    public const string ViewDeletedContent = "view-deleted-content";
    public const string RestoreDeletedContent = "restore-deleted-content";
    public const string PermanentlyDeleteContent = "permanently-delete-content";
    public const string ManageContentWorkflowHistory = "manage-content-workflow-history";
    public const string ManageRoleDefinitions = "manage-role-definitions";
    public const string ManageProtectedAdminAccounts = "manage-protected-admin-accounts";
    public const string ManageLogRetention = "manage-log-retention";

    public static readonly string[] All =
    [
        ManageContentConfiguration,
        ViewEditAnyDraft,
        DirectPublishDraft,
        ForceReturnContentToDraft,
        DeleteAnyEligibleContent,
        ViewDeletedContent,
        RestoreDeletedContent,
        PermanentlyDeleteContent,
        ManageContentWorkflowHistory,
        ManageRoleDefinitions,
        ManageProtectedAdminAccounts,
        ManageLogRetention
    ];
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string AdminId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsAdminAdmin { get; set; }
    public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
    public List<string> Permissions { get; set; } = new();
}

public class PasswordUpdateRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class SessionResponse
{
    public bool Valid { get; set; }
    public string AdminId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsAdminAdmin { get; set; }
    public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
    public List<string> Permissions { get; set; } = new();
}

public class AdminUserCreateRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public List<string> ExtraPermissions { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public bool Active { get; set; } = true;
}

public class AdminUserUpdateRequest
{
    public string? FullName { get; set; }
    public string? RoleId { get; set; }
    public List<string>? ExtraPermissions { get; set; }
    public List<string>? Permissions { get; set; }
    public bool? Active { get; set; }
}

public class AdminPasswordResetRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminBulkDeleteRequest
{
    public List<string> Ids { get; set; } = new();
}

public class AdminUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsAdminAdmin { get; set; }
    public AdminUserStatus Status { get; set; }
    public List<string> RolePermissions { get; set; } = new();
    public List<string> ExtraPermissions { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public int TokenVersion { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminRoleResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public bool IsProtected { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDeleting { get; set; }
    public long UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminRoleCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

public class AdminRoleUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

public class AdminRoleDeleteRequest
{
    public string? ReplacementRoleId { get; set; }
}

public class AdminRoleImpactResponse
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public long AffectedUsers { get; set; }
    public List<string> AddedPermissions { get; set; } = new();
    public List<string> RemovedPermissions { get; set; } = new();
}

public class AdminSessionResponse
{
    public string Id { get; set; } = string.Empty;
    public string AdminId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public AdminSessionRevokeReason? RevokeReason { get; set; }
}

public class AdminLoginActivityResponse
{
    public string Id { get; set; } = string.Empty;
    public string? AdminId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class AdminAuditLogResponse
{
    public string Id { get; set; } = string.Empty;
    public AdminAuditArea Area { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? TargetEmail { get; set; }
    public string Message { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminPagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
}
