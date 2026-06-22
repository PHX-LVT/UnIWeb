namespace Contracts.Auth;

public enum AdminRole
{
    AdminAdmin,
    Manager,
    Writer,
    Viewer
}

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

public static class AdminPermissionKeys
{
    public const string PageBuilder = "page-builder";
    public const string ManageContent = "manage-content";
    public const string PublishContent = "publish-content";
    public const string ManageUsers = "manage-users";
    public const string ManageSettings = "manage-settings";
    public const string DeleteContent = "delete-content";
    public const string ViewLogs = "view-logs";

    public static readonly string[] All =
    [
        PageBuilder,
        ManageContent,
        PublishContent,
        ManageUsers,
        ManageSettings,
        DeleteContent,
        ViewLogs
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
    public AdminRole Role { get; set; } = AdminRole.Viewer;
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
    public AdminRole Role { get; set; } = AdminRole.Viewer;
    public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
    public List<string> Permissions { get; set; } = new();
}

public class AdminUserCreateRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AdminRole Role { get; set; } = AdminRole.Writer;
    public List<string> Permissions { get; set; } = new();
    public bool Active { get; set; } = true;
}

public class AdminUserUpdateRequest
{
    public string? FullName { get; set; }
    public AdminRole? Role { get; set; }
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
    public AdminRole Role { get; set; }
    public AdminUserStatus Status { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int TokenVersion { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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
