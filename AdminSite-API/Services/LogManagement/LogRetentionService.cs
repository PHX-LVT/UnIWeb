using Contracts.Auth;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FullProject.Services.LogManagement;

public sealed class LogRetentionService
{
    private static readonly SemaphoreSlim RunLock = new(1, 1);
    private readonly IMongoCollection<AdminAuditEvent> _audit;
    private readonly IMongoCollection<AdminLoginActivityEvent> _login;
    private readonly IMongoCollection<AdminAuditEvent> _auditArchive;
    private readonly IMongoCollection<AdminLoginActivityEvent> _loginArchive;
    private readonly IMongoCollection<AdminLogRetentionLedger> _ledger;
    private readonly LogManagementSettings _settings;

    public LogRetentionService(IMongoDatabase database, IOptions<LogManagementSettings> settings)
    {
        _audit = database.GetCollection<AdminAuditEvent>("admin_audit_events");
        _login = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
        _auditArchive = database.GetCollection<AdminAuditEvent>("admin_audit_event_archives");
        _loginArchive = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_event_archives");
        _ledger = database.GetCollection<AdminLogRetentionLedger>("admin_log_retention_ledger");
        _settings = settings.Value;
    }

    public async Task<AdminLogRetentionStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var item = await _ledger.Find(_ => true).SortByDescending(run => run.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return WithPolicy(item is null ? new AdminLogRetentionStatusResponse() : Map(item));
    }

    public async Task<AdminLogRetentionStatusResponse> RunAsync(string actorId, CancellationToken cancellationToken = default)
    {
        if (!await RunLock.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("A log retention run is already in progress.");

        var run = new AdminLogRetentionLedger { RunById = actorId };
        await _ledger.InsertOneAsync(run, cancellationToken: cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            run.AuditEventsArchived = await ArchiveAuditAsync(now.AddDays(-_settings.AuditActiveDays), cancellationToken);
            run.LoginEventsArchived = await ArchiveLoginAsync(now.AddDays(-_settings.LoginActiveDays), cancellationToken);
            var auditPurge = await _auditArchive.DeleteManyAsync(
                Builders<AdminAuditEvent>.Filter.Or(
                    Builders<AdminAuditEvent>.Filter.And(
                        Builders<AdminAuditEvent>.Filter.Ne(item => item.RetentionClass, "critical"),
                        Builders<AdminAuditEvent>.Filter.Lt(item => item.OccurredAtUtc, now.AddDays(-_settings.AuditTotalDays))),
                    Builders<AdminAuditEvent>.Filter.And(
                        Builders<AdminAuditEvent>.Filter.Eq(item => item.RetentionClass, "critical"),
                        Builders<AdminAuditEvent>.Filter.Lt(item => item.OccurredAtUtc, now.AddDays(-_settings.CriticalSecurityTotalDays)))),
                cancellationToken);
            run.AuditEventsPurged = auditPurge.DeletedCount;
            run.LoginEventsPurged = (await _loginArchive.DeleteManyAsync(
                item => item.OccurredAtUtc < now.AddDays(-_settings.LoginTotalDays), cancellationToken)).DeletedCount;
            run.Status = "Completed";
            run.CompletedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.Error = ex.Message.Length <= 500 ? ex.Message : ex.Message[..500];
            run.CompletedAtUtc = DateTime.UtcNow;
            throw;
        }
        finally
        {
            await _ledger.ReplaceOneAsync(item => item.Id == run.Id, run, cancellationToken: CancellationToken.None);
            RunLock.Release();
        }
        return WithPolicy(Map(run));
    }

    private async Task<long> ArchiveAuditAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        long total = 0;
        var batchSize = Math.Clamp(_settings.RetentionBatchSize, 100, 10_000);
        while (true)
        {
            var items = await _audit.Find(item => item.OccurredAtUtc < cutoff)
                .SortBy(item => item.OccurredAtUtc).Limit(batchSize).ToListAsync(cancellationToken);
            if (items.Count == 0) return total;
            var writes = items.Select(item => new ReplaceOneModel<AdminAuditEvent>(
                Builders<AdminAuditEvent>.Filter.Eq(current => current.Id, item.Id), item) { IsUpsert = true }).ToList();
            await _auditArchive.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
            var ids = items.Select(item => item.Id).ToList();
            var verified = await _auditArchive.CountDocumentsAsync(item => ids.Contains(item.Id), cancellationToken: cancellationToken);
            if (verified != ids.Count) throw new InvalidOperationException("Audit archive verification failed; active records were retained.");
            total += (await _audit.DeleteManyAsync(item => ids.Contains(item.Id), cancellationToken)).DeletedCount;
        }
    }

    private async Task<long> ArchiveLoginAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        long total = 0;
        var batchSize = Math.Clamp(_settings.RetentionBatchSize, 100, 10_000);
        while (true)
        {
            var items = await _login.Find(item => item.OccurredAtUtc < cutoff)
                .SortBy(item => item.OccurredAtUtc).Limit(batchSize).ToListAsync(cancellationToken);
            if (items.Count == 0) return total;
            var writes = items.Select(item => new ReplaceOneModel<AdminLoginActivityEvent>(
                Builders<AdminLoginActivityEvent>.Filter.Eq(current => current.Id, item.Id), item) { IsUpsert = true }).ToList();
            await _loginArchive.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
            var ids = items.Select(item => item.Id).ToList();
            var verified = await _loginArchive.CountDocumentsAsync(item => ids.Contains(item.Id), cancellationToken: cancellationToken);
            if (verified != ids.Count) throw new InvalidOperationException("Login archive verification failed; active records were retained.");
            total += (await _login.DeleteManyAsync(item => ids.Contains(item.Id), cancellationToken)).DeletedCount;
        }
    }

    private static AdminLogRetentionStatusResponse Map(AdminLogRetentionLedger run) => new()
    {
        LastRunAtUtc = run.CompletedAtUtc ?? run.StartedAtUtc,
        AuditEventsArchived = run.AuditEventsArchived,
        LoginEventsArchived = run.LoginEventsArchived,
        AuditEventsPurged = run.AuditEventsPurged,
        LoginEventsPurged = run.LoginEventsPurged,
        Status = run.Status
    };

    private AdminLogRetentionStatusResponse WithPolicy(AdminLogRetentionStatusResponse response)
    {
        response.AuditActiveDays = _settings.AuditActiveDays;
        response.AuditTotalDays = _settings.AuditTotalDays;
        response.LoginActiveDays = _settings.LoginActiveDays;
        response.LoginTotalDays = _settings.LoginTotalDays;
        response.CriticalSecurityTotalDays = _settings.CriticalSecurityTotalDays;
        return response;
    }
}
