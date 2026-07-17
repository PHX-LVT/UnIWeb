using Contracts.Auth;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

namespace FullProject.Services.LogManagement;

public sealed class LogExportService
{
    private readonly IMongoCollection<AdminAuditEvent> _audit;
    private readonly IMongoCollection<AdminLoginActivityEvent> _login;
    private readonly IMongoCollection<AdminAuditEvent> _auditArchive;
    private readonly IMongoCollection<AdminLoginActivityEvent> _loginArchive;
    private readonly IMongoCollection<AdminLogExportRecord> _records;
    private readonly LogManagementQueryService _queries;
    private readonly LogManagementSettings _settings;

    public LogExportService(
        IMongoDatabase database,
        LogManagementQueryService queries,
        IOptions<LogManagementSettings> settings)
    {
        _audit = database.GetCollection<AdminAuditEvent>("admin_audit_events");
        _login = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
        _auditArchive = database.GetCollection<AdminAuditEvent>("admin_audit_event_archives");
        _loginArchive = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_event_archives");
        _records = database.GetCollection<AdminLogExportRecord>("admin_log_export_records");
        _queries = queries;
        _settings = settings.Value;
    }

    public async Task<long> WriteAsync(
        Stream output,
        AdminLogExportRequest request,
        bool includeSensitiveFields,
        CancellationToken cancellationToken)
    {
        return request.LogType.Equals("login-activity", StringComparison.OrdinalIgnoreCase)
            ? await WriteLoginAsync(output, request, includeSensitiveFields, cancellationToken)
            : await WriteAuditAsync(output, request, includeSensitiveFields, cancellationToken);
    }

    public Task RecordAsync(
        AdminLogExportRequest request,
        string actorId,
        string actorEmail,
        long rowCount,
        CancellationToken cancellationToken) =>
        _records.InsertOneAsync(new AdminLogExportRecord
        {
            RequestedById = actorId,
            RequestedByEmail = actorEmail,
            LogType = request.LogType,
            Format = request.Format,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            RowCount = rowCount,
            IncludedArchived = request.IncludeArchived
        }, cancellationToken: cancellationToken);

    private async Task<long> WriteAuditAsync(Stream output, AdminLogExportRequest request, bool includeSensitiveFields, CancellationToken cancellationToken)
    {
        var filter = _queries.BuildAuditFilter(new AdminAuditFilterRequest
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            ActorId = request.ActorId,
            DomainCode = request.DomainCode,
            ActionCode = request.ActionCode,
            Outcome = request.Outcome,
            Search = request.Search
        }, includeCursor: false);
        using var cursor = await _audit.Find(filter)
            .SortByDescending(item => item.OccurredAtUtc)
            .Limit(_settings.ExportMaximumRows)
            .ToCursorAsync(cancellationToken);
        using var archiveCursor = request.IncludeArchived
            ? await _auditArchive.Find(filter)
                .SortByDescending(item => item.OccurredAtUtc)
                .Limit(_settings.ExportMaximumRows)
                .ToCursorAsync(cancellationToken)
            : null;
        var cursors = archiveCursor is null ? [cursor] : new IAsyncCursor<AdminAuditEvent>[] { cursor, archiveCursor };
        return request.Format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? await WriteJsonAsync(output, cursors, item => item.Id, item => MapAuditForExport(item, includeSensitiveFields), _settings.ExportMaximumRows, cancellationToken)
            : await WriteAuditCsvAsync(output, cursors, includeSensitiveFields, _settings.ExportMaximumRows, cancellationToken);
    }

    private async Task<long> WriteLoginAsync(Stream output, AdminLogExportRequest request, bool includeSensitiveFields, CancellationToken cancellationToken)
    {
        var filter = _queries.BuildLoginFilter(new AdminLoginActivityFilterRequest
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            AdminId = request.ActorId,
            EventCode = request.EventCode,
            Outcome = request.Outcome,
            Search = request.Search
        }, includeCursor: false);
        using var cursor = await _login.Find(filter)
            .SortByDescending(item => item.OccurredAtUtc)
            .Limit(_settings.ExportMaximumRows)
            .ToCursorAsync(cancellationToken);
        using var archiveCursor = request.IncludeArchived
            ? await _loginArchive.Find(filter)
                .SortByDescending(item => item.OccurredAtUtc)
                .Limit(_settings.ExportMaximumRows)
                .ToCursorAsync(cancellationToken)
            : null;
        var cursors = archiveCursor is null ? [cursor] : new IAsyncCursor<AdminLoginActivityEvent>[] { cursor, archiveCursor };
        return request.Format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? await WriteJsonAsync(output, cursors, item => item.Id, item => MapLoginForExport(item, includeSensitiveFields), _settings.ExportMaximumRows, cancellationToken)
            : await WriteLoginCsvAsync(output, cursors, includeSensitiveFields, _settings.ExportMaximumRows, cancellationToken);
    }

    private static async Task<long> WriteAuditCsvAsync(
        Stream output,
        IReadOnlyList<IAsyncCursor<AdminAuditEvent>> cursors,
        bool includeSensitiveFields,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync("OccurredAtUtc,DomainCode,ActionCode,Outcome,Severity,ActorEmail,ActorRole,SessionId,TargetType,TargetId,TargetLabel,ChangeCount,Message,IpAddress,CorrelationId");
        long count = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cursor in cursors)
        {
            while (count < maximumRows && await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var item in cursor.Current)
                {
                    if (count >= maximumRows) break;
                    if (!seen.Add(item.Id)) continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(string.Join(',', new[]
                    {
                        Csv(item.OccurredAtUtc.ToString("O")), Csv(item.DomainCode), Csv(item.ActionCode), Csv(item.Outcome.ToString()),
                        Csv(item.Severity.ToString()), Csv(item.ActorEmail), Csv(item.ActorRoleName), Csv(item.SessionId),
                        Csv(item.TargetTypeCode), Csv(item.TargetId), Csv(item.TargetLabel), item.ChangeCount.ToString(),
                        Csv(item.ResultMessage), Csv(includeSensitiveFields ? item.IpAddress : MaskIp(item.IpAddress)),
                        Csv(includeSensitiveFields ? item.CorrelationId : string.Empty)
                    }));
                    count++;
                }
            }
        }
        await writer.FlushAsync(cancellationToken);
        return count;
    }

    private static async Task<long> WriteLoginCsvAsync(
        Stream output,
        IReadOnlyList<IAsyncCursor<AdminLoginActivityEvent>> cursors,
        bool includeSensitiveFields,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync("OccurredAtUtc,EventCode,Outcome,ReasonCode,Account,SessionId,IpAddress,Browser,OperatingSystem,Message,CorrelationId");
        long count = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cursor in cursors)
        {
            while (count < maximumRows && await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var item in cursor.Current)
                {
                    if (count >= maximumRows) break;
                    if (!seen.Add(item.Id)) continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(string.Join(',', new[]
                    {
                        Csv(item.OccurredAtUtc.ToString("O")), Csv(item.EventCode), Csv(item.Outcome.ToString()), Csv(item.ReasonCode),
                        Csv(string.IsNullOrWhiteSpace(item.AccountDisplayName) ? item.AccountEmail : item.AccountDisplayName), Csv(item.SessionId),
                        Csv(includeSensitiveFields ? item.IpAddress : MaskIp(item.IpAddress)), Csv(item.BrowserName),
                        Csv(item.OperatingSystem), Csv(item.ResultMessage), Csv(includeSensitiveFields ? item.CorrelationId : string.Empty)
                    }));
                    count++;
                }
            }
        }
        await writer.FlushAsync(cancellationToken);
        return count;
    }

    private static async Task<long> WriteJsonAsync<TModel, TResponse>(
        Stream output,
        IReadOnlyList<IAsyncCursor<TModel>> cursors,
        Func<TModel, string> getId,
        Func<TModel, TResponse> map,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        await output.WriteAsync("["u8.ToArray(), cancellationToken);
        long count = 0;
        var first = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cursor in cursors)
        {
            while (count < maximumRows && await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var item in cursor.Current)
                {
                    if (count >= maximumRows) break;
                    if (!seen.Add(getId(item))) continue;
                    if (!first) await output.WriteAsync(","u8.ToArray(), cancellationToken);
                    await JsonSerializer.SerializeAsync(output, map(item), cancellationToken: cancellationToken);
                    first = false;
                    count++;
                }
            }
        }
        await output.WriteAsync("]"u8.ToArray(), cancellationToken);
        return count;
    }

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && safe[0] is '=' or '+' or '-' or '@') safe = "'" + safe;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private static AdminAuditEventResponse MapAuditForExport(AdminAuditEvent item, bool includeSensitiveFields)
    {
        var response = LogManagementQueryService.MapAudit(item);
        if (!includeSensitiveFields)
        {
            response.IpAddress = MaskIp(response.IpAddress);
            response.CorrelationId = string.Empty;
            response.RequestPath = string.Empty;
        }
        return response;
    }

    private static AdminLoginActivityEventResponse MapLoginForExport(AdminLoginActivityEvent item, bool includeSensitiveFields)
    {
        var response = LogManagementQueryService.MapLogin(item);
        if (!includeSensitiveFields)
        {
            response.IpAddress = MaskIp(response.IpAddress);
            response.CorrelationId = string.Empty;
        }
        return response;
    }

    private static string MaskIp(string value)
    {
        var segments = value.Split('.');
        if (segments.Length == 4) return $"{segments[0]}.{segments[1]}.{segments[2]}.*";
        var separator = value.LastIndexOf(':');
        return separator > 0 ? value[..separator] + ":*" : string.IsNullOrWhiteSpace(value) ? string.Empty : "masked";
    }
}
