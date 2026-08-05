using Contracts.Auth;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FullProject.Models;

[BsonIgnoreExtraElements]
public sealed class AdminAuditEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 2;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string DomainCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)]
    public AdminAuditOutcome Outcome { get; set; } = AdminAuditOutcome.Succeeded;
    [BsonRepresentation(BsonType.String)]
    public AdminAuditSeverity Severity { get; set; } = AdminAuditSeverity.Information;
    public string ActorId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string ActorRoleId { get; set; } = string.Empty;
    public string ActorRoleName { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public string? TargetTypeCode { get; set; }
    public string? TargetId { get; set; }
    public string? TargetLabel { get; set; }
    public int ChangeCount { get; set; }
    public List<string> ChangedFields { get; set; } = new();
    public Dictionary<string, string?> SafeBefore { get; set; } = new();
    public Dictionary<string, string?> SafeAfter { get; set; } = new();
    public string MessageKey { get; set; } = string.Empty;
    public Dictionary<string, string> MessageParameters { get; set; } = new();
    public string ResultMessage { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public string RetentionClass { get; set; } = "standard";
    public string Source { get; set; } = "v2";
}

[BsonIgnoreExtraElements]
public sealed class AdminLoginActivityEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 2;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string EventCode { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)]
    public AdminAuditOutcome Outcome { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? AdminId { get; set; }
    public string AccountEmail { get; set; } = string.Empty;
    public string AccountDisplayName { get; set; } = string.Empty;
    public string AttemptedIdentifierHash { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string MessageKey { get; set; } = string.Empty;
    public string ResultMessage { get; set; } = string.Empty;
    public string RetentionClass { get; set; } = "security";
    public string Source { get; set; } = "v2";
}

[BsonIgnoreExtraElements]
public sealed class AdminLogExportRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string RequestedById { get; set; } = string.Empty;
    public string RequestedByEmail { get; set; } = string.Empty;
    public string LogType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long RowCount { get; set; }
    public bool IncludedArchived { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class AdminLogRetentionLedger
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string RunById { get; set; } = string.Empty;
    public long AuditEventsArchived { get; set; }
    public long LoginEventsArchived { get; set; }
    public long AuditEventsPurged { get; set; }
    public long LoginEventsPurged { get; set; }
    public string Status { get; set; } = "Running";
    public string Error { get; set; } = string.Empty;
}
