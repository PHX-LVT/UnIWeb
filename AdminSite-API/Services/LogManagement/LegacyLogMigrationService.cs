using Contracts.Auth;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace FullProject.Services.LogManagement;

public sealed class LegacyLogMigrationService : BackgroundService
{
    private readonly IMongoCollection<AdminAuditLog> _legacyAudit;
    private readonly IMongoCollection<AdminLoginActivityRecord> _legacyLogin;
    private readonly IMongoCollection<AdminAuditEvent> _audit;
    private readonly IMongoCollection<AdminLoginActivityEvent> _login;
    private readonly IMongoCollection<AdminLogMigrationCheckpoint> _checkpoints;
    private readonly LogManagementSettings _settings;
    private readonly ILogger<LegacyLogMigrationService> _logger;

    public LegacyLogMigrationService(
        IMongoDatabase database,
        IOptions<LogManagementSettings> settings,
        ILogger<LegacyLogMigrationService> logger)
    {
        _legacyAudit = database.GetCollection<AdminAuditLog>("admin_audit_logs");
        _legacyLogin = database.GetCollection<AdminLoginActivityRecord>("admin_login_activity");
        _audit = database.GetCollection<AdminAuditEvent>("admin_audit_events");
        _login = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
        _checkpoints = database.GetCollection<AdminLogMigrationCheckpoint>("admin_log_archive_checkpoints");
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var auditComplete = await MigrateAuditBatchAsync(stoppingToken);
                var loginComplete = await MigrateLoginBatchAsync(stoppingToken);
                if (auditComplete && loginComplete)
                {
                    _logger.LogInformation("Legacy audit and login-activity backfill complete.");
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Legacy log backfill paused and will retry.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task<bool> MigrateAuditBatchAsync(CancellationToken cancellationToken)
    {
        const string source = "admin_audit_logs";
        var checkpoint = await GetCheckpointAsync(source, cancellationToken);
        if (checkpoint.Completed) return true;
        var filter = string.IsNullOrWhiteSpace(checkpoint.LastLegacyId)
            ? Builders<AdminAuditLog>.Filter.Empty
            : Builders<AdminAuditLog>.Filter.Gt(item => item.Id, checkpoint.LastLegacyId);
        var limit = Math.Clamp(_settings.MigrationBatchSize, 100, 5000);
        var items = await _legacyAudit.Find(filter).SortBy(item => item.Id).Limit(limit).ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            var writes = items
                .Where(item => item.Action is not ("login-success" or "login-denied" or "logout"))
                .Select(item =>
            {
                var mapping = AuditActionCatalog.ResolveLegacy(item.Area.ToString(), item.Action);
                var client = AdminClientInfoParser.Parse(item.UserAgent);
                var replacement = new AdminAuditEvent
                {
                    Id = item.Id,
                    OccurredAtUtc = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc),
                    DomainCode = mapping.DomainCode,
                    ActionCode = mapping.ActionCode,
                    Outcome = item.Action.Contains("denied", StringComparison.OrdinalIgnoreCase)
                        ? AdminAuditOutcome.Denied
                        : AdminAuditOutcome.Succeeded,
                    Severity = mapping.Critical ? AdminAuditSeverity.Warning : AdminAuditSeverity.Information,
                    ActorId = item.ActorId,
                    ActorEmail = item.ActorEmail,
                    ActorDisplayName = item.ActorEmail,
                    TargetTypeCode = mapping.TargetTypeCode,
                    TargetId = item.TargetId,
                    TargetLabel = item.TargetEmail,
                    ChangeCount = item.Action.Contains("denied", StringComparison.OrdinalIgnoreCase) ? 0 : 1,
                    ResultMessage = item.Message,
                    IpAddress = item.IpAddress,
                    UserAgent = item.UserAgent,
                    BrowserName = client.Browser,
                    OperatingSystem = client.OperatingSystem,
                    CorrelationId = $"legacy:{item.Id}",
                    RetentionClass = mapping.Critical ? "critical" : "standard",
                    Source = "legacy",
                    LegacySourceId = item.Id
                };
                return new ReplaceOneModel<AdminAuditEvent>(
                    Builders<AdminAuditEvent>.Filter.Eq(current => current.LegacySourceId, item.Id), replacement)
                { IsUpsert = true };
            }).ToList();
            if (writes.Count > 0)
                await _audit.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
            checkpoint.LastLegacyId = items[^1].Id;
        }
        checkpoint.Completed = items.Count < limit;
        await SaveCheckpointAsync(checkpoint, cancellationToken);
        return checkpoint.Completed;
    }

    private async Task<bool> MigrateLoginBatchAsync(CancellationToken cancellationToken)
    {
        const string source = "admin_login_activity";
        var checkpoint = await GetCheckpointAsync(source, cancellationToken);
        if (checkpoint.Completed) return true;
        var filter = string.IsNullOrWhiteSpace(checkpoint.LastLegacyId)
            ? Builders<AdminLoginActivityRecord>.Filter.Empty
            : Builders<AdminLoginActivityRecord>.Filter.Gt(item => item.Id, checkpoint.LastLegacyId);
        var limit = Math.Clamp(_settings.MigrationBatchSize, 100, 5000);
        var items = await _legacyLogin.Find(filter).SortBy(item => item.Id).Limit(limit).ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            var writes = items.Select(item =>
            {
                var known = !string.IsNullOrWhiteSpace(item.AdminId);
                var client = AdminClientInfoParser.Parse(item.UserAgent);
                var replacement = new AdminLoginActivityEvent
                {
                    Id = item.Id,
                    OccurredAtUtc = DateTime.SpecifyKind(item.OccurredAt, DateTimeKind.Utc),
                    EventCode = item.EventType,
                    Outcome = item.Success ? AdminAuditOutcome.Succeeded : AdminAuditOutcome.Denied,
                    ReasonCode = item.EventType,
                    AdminId = item.AdminId,
                    AccountEmail = known ? item.Email : string.Empty,
                    AccountDisplayName = known ? item.Email : MaskIdentifier(item.Email),
                    AttemptedIdentifierHash = known || string.IsNullOrWhiteSpace(item.Email)
                        ? string.Empty
                        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Email.Trim().ToLowerInvariant()))),
                    CorrelationId = $"legacy:{item.Id}",
                    IpAddress = item.IpAddress,
                    UserAgent = item.UserAgent,
                    BrowserName = string.IsNullOrWhiteSpace(item.BrowserName) ? client.Browser : item.BrowserName,
                    OperatingSystem = string.IsNullOrWhiteSpace(item.OperatingSystem) ? client.OperatingSystem : item.OperatingSystem,
                    ResultMessage = item.Message,
                    Source = "legacy",
                    LegacySourceId = item.Id
                };
                return new ReplaceOneModel<AdminLoginActivityEvent>(
                    Builders<AdminLoginActivityEvent>.Filter.Eq(current => current.LegacySourceId, item.Id), replacement)
                { IsUpsert = true };
            }).ToList();
            await _login.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
            checkpoint.LastLegacyId = items[^1].Id;
        }
        checkpoint.Completed = items.Count < limit;
        await SaveCheckpointAsync(checkpoint, cancellationToken);
        return checkpoint.Completed;
    }

    private async Task<AdminLogMigrationCheckpoint> GetCheckpointAsync(string source, CancellationToken cancellationToken) =>
        await _checkpoints.Find(item => item.SourceName == source).FirstOrDefaultAsync(cancellationToken)
        ?? new AdminLogMigrationCheckpoint { SourceName = source };

    private Task SaveCheckpointAsync(AdminLogMigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        checkpoint.UpdatedAtUtc = DateTime.UtcNow;
        return _checkpoints.ReplaceOneAsync(item => item.SourceName == checkpoint.SourceName, checkpoint,
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    private static string MaskIdentifier(string value)
    {
        var parts = value.Split('@', 2);
        return parts.Length == 2 && parts[0].Length > 0 ? $"{parts[0][0]}***@{parts[1]}" : "Unknown account";
    }
}
