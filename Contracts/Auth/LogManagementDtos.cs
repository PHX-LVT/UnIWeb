namespace Contracts.Auth;

public sealed class AdminAuditEventResponse
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string DomainCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    public AdminAuditOutcome Outcome { get; set; }
    public AdminAuditSeverity Severity { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string ActorRoleName { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public string? TargetTypeCode { get; set; }
    public string? TargetId { get; set; }
    public string? TargetLabel { get; set; }
    public int ChangeCount { get; set; }
    public List<string> ChangedFields { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public bool IsLegacy { get; set; }
}

public sealed class AdminAuditSessionSummaryResponse
{
    public string SessionKey { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public DateTime FirstEventAtUtc { get; set; }
    public DateTime LastEventAtUtc { get; set; }
    public DateTime SessionStartedAtUtc { get; set; }
    public DateTime SessionLastActivityAtUtc { get; set; }
    public long DurationSeconds { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string ActorRoleName { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int DeniedCount { get; set; }
    public List<string> DomainCodes { get; set; } = new();
    public AdminAuditOutcome OverallOutcome { get; set; }
}

public sealed class AdminLoginActivityEventResponse
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public AdminAuditOutcome Outcome { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? AdminId { get; set; }
    public string AccountDisplay { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsLegacy { get; set; }
}

public sealed class AdminLogCursorPageResponse<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}

public sealed class AdminAuditFilterRequest
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? ActorId { get; set; }
    public string? DomainCode { get; set; }
    public string? ActionCode { get; set; }
    public AdminAuditOutcome? Outcome { get; set; }
    public string? Search { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 50;
}

public sealed class AdminLoginActivityFilterRequest
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? AdminId { get; set; }
    public string? EventCode { get; set; }
    public AdminAuditOutcome? Outcome { get; set; }
    public string? Search { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 50;
}

public sealed class AdminLogExportRequest
{
    public string LogType { get; set; } = "audit";
    public string Format { get; set; } = "csv";
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string? ActorId { get; set; }
    public string? DomainCode { get; set; }
    public string? ActionCode { get; set; }
    public string? EventCode { get; set; }
    public AdminAuditOutcome? Outcome { get; set; }
    public string? Search { get; set; }
    public bool IncludeArchived { get; set; } = true;
}

public sealed class AdminLogRetentionStatusResponse
{
    public int AuditActiveDays { get; set; }
    public int AuditTotalDays { get; set; }
    public int LoginActiveDays { get; set; }
    public int LoginTotalDays { get; set; }
    public int CriticalSecurityTotalDays { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public long AuditEventsArchived { get; set; }
    public long LoginEventsArchived { get; set; }
    public long AuditEventsPurged { get; set; }
    public long LoginEventsPurged { get; set; }
    public string Status { get; set; } = "NeverRun";
}
