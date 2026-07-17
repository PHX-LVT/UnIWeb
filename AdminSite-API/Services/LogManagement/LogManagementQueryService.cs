using Contracts.Auth;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.RegularExpressions;

namespace FullProject.Services.LogManagement;

public sealed class LogManagementQueryService
{
    private readonly IMongoCollection<AdminAuditEvent> _audit;
    private readonly IMongoCollection<AdminLoginActivityEvent> _login;

    public LogManagementQueryService(IMongoDatabase database)
    {
        _audit = database.GetCollection<AdminAuditEvent>("admin_audit_events");
        _login = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
    }

    public async Task<AdminLogCursorPageResponse<AdminAuditEventResponse>> GetAuditEventsAsync(
        AdminAuditFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 10, 100);
        var filter = BuildAuditFilter(request, includeCursor: true);
        var items = await _audit.Find(filter)
            .Sort(Builders<AdminAuditEvent>.Sort
                .Descending(item => item.OccurredAtUtc)
                .Descending(item => item.Id))
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new AdminLogCursorPageResponse<AdminAuditEventResponse>
        {
            Items = items.Select(MapAudit).ToList(),
            HasMore = hasMore,
            NextCursor = hasMore && items.Count > 0 ? EncodeCursor(items[^1].OccurredAtUtc, items[^1].Id) : null
        };
    }

    public async Task<AdminLogCursorPageResponse<AdminAuditSessionSummaryResponse>> GetAuditSessionsAsync(
        AdminAuditFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 10, 100);
        var stages = new List<BsonDocument>
        {
            new("$match", BuildAuditMatch(request)),
            new("$sort", new BsonDocument { { "OccurredAtUtc", -1 }, { "_id", -1 } }),
            new("$addFields", new BsonDocument("SessionKey",
                new BsonDocument("$ifNull", new BsonArray
                {
                    "$SessionId",
                    new BsonDocument("$concat", new BsonArray
                    {
                        "event:", new BsonDocument("$toString", "$_id")
                    })
                }))),
            new("$group", new BsonDocument
            {
                { "_id", "$SessionKey" },
                { "SessionId", new BsonDocument("$first", "$SessionId") },
                { "FirstEventAtUtc", new BsonDocument("$min", "$OccurredAtUtc") },
                { "LastEventAtUtc", new BsonDocument("$max", "$OccurredAtUtc") },
                { "ActorId", new BsonDocument("$first", "$ActorId") },
                { "ActorEmail", new BsonDocument("$first", "$ActorEmail") },
                { "ActorDisplayName", new BsonDocument("$first", "$ActorDisplayName") },
                { "ActorRoleName", new BsonDocument("$first", "$ActorRoleName") },
                { "EventCount", new BsonDocument("$sum", 1) },
                { "SucceededCount", CountOutcome(AdminAuditOutcome.Succeeded) },
                { "FailedCount", CountOutcome(AdminAuditOutcome.Failed) },
                { "DeniedCount", CountOutcome(AdminAuditOutcome.Denied) },
                { "PartialCount", CountOutcome(AdminAuditOutcome.Partial) },
                { "DomainCodes", new BsonDocument("$addToSet", "$DomainCode") }
            })
        };

        if (TryDecodeCursor(request.Cursor, out var cursorAt, out var cursorKey))
        {
            stages.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("LastEventAtUtc", new BsonDocument("$lt", cursorAt)),
                new BsonDocument
                {
                    { "LastEventAtUtc", cursorAt },
                    { "_id", new BsonDocument("$lt", cursorKey) }
                }
            })));
        }
        stages.Add(new BsonDocument("$sort", new BsonDocument { { "LastEventAtUtc", -1 }, { "_id", -1 } }));
        stages.Add(new BsonDocument("$limit", pageSize + 1));
        stages.Add(new BsonDocument("$lookup", new BsonDocument
        {
            { "from", "admin_sessions" },
            { "localField", "SessionId" },
            { "foreignField", "TokenId" },
            { "as", "SessionRecord" }
        }));
        stages.Add(new BsonDocument("$addFields", new BsonDocument
        {
            { "SessionStartedAtUtc", new BsonDocument("$ifNull", new BsonArray
                { new BsonDocument("$arrayElemAt", new BsonArray { "$SessionRecord.LoginAt", 0 }), "$FirstEventAtUtc" }) },
            { "SessionLastActivityAtUtc", new BsonDocument("$ifNull", new BsonArray
                { new BsonDocument("$arrayElemAt", new BsonArray { "$SessionRecord.LastActivityAt", 0 }), "$LastEventAtUtc" }) }
        }));

        var pipeline = PipelineDefinition<AdminAuditEvent, BsonDocument>.Create(stages);
        var documents = await _audit.Aggregate(pipeline).ToListAsync(cancellationToken);
        var hasMore = documents.Count > pageSize;
        if (hasMore) documents.RemoveAt(documents.Count - 1);
        var items = documents.Select(MapSummary).ToList();
        return new AdminLogCursorPageResponse<AdminAuditSessionSummaryResponse>
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore && items.Count > 0
                ? EncodeCursor(items[^1].LastEventAtUtc, items[^1].SessionKey)
                : null
        };
    }

    public async Task<List<AdminAuditEventResponse>> GetSessionEventsAsync(
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<AdminAuditEvent> filter;
        if (sessionKey.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            var id = sessionKey[6..];
            filter = Builders<AdminAuditEvent>.Filter.Eq(item => item.Id, id);
        }
        else
        {
            filter = Builders<AdminAuditEvent>.Filter.Eq(item => item.SessionId, sessionKey);
        }

        var events = await _audit.Find(filter)
            .SortBy(item => item.OccurredAtUtc)
            .Limit(1000)
            .ToListAsync(cancellationToken);
        return events.Select(MapAudit).ToList();
    }

    public async Task<AdminLogCursorPageResponse<AdminLoginActivityEventResponse>> GetLoginActivityAsync(
        AdminLoginActivityFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 10, 100);
        var filter = BuildLoginFilter(request, includeCursor: true);
        var items = await _login.Find(filter)
            .Sort(Builders<AdminLoginActivityEvent>.Sort
                .Descending(item => item.OccurredAtUtc)
                .Descending(item => item.Id))
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new AdminLogCursorPageResponse<AdminLoginActivityEventResponse>
        {
            Items = items.Select(MapLogin).ToList(),
            HasMore = hasMore,
            NextCursor = hasMore && items.Count > 0 ? EncodeCursor(items[^1].OccurredAtUtc, items[^1].Id) : null
        };
    }

    public async Task<(List<AdminAuditEvent> Items, long Total)> GetAuditOffsetPageAsync(
        int page,
        int pageSize,
        string? targetId,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        page = Math.Max(1, page);
        var filter = string.IsNullOrWhiteSpace(targetId)
            ? Builders<AdminAuditEvent>.Filter.Empty
            : Builders<AdminAuditEvent>.Filter.Eq(item => item.TargetId, targetId);
        var total = await _audit.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await _audit.Find(filter).SortByDescending(item => item.OccurredAtUtc)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(List<AdminLoginActivityEvent> Items, long Total)> GetLoginOffsetPageAsync(
        int page,
        int pageSize,
        string? adminId,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        page = Math.Max(1, page);
        var filter = string.IsNullOrWhiteSpace(adminId)
            ? Builders<AdminLoginActivityEvent>.Filter.Empty
            : Builders<AdminLoginActivityEvent>.Filter.Eq(item => item.AdminId, adminId);
        var total = await _login.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await _login.Find(filter).SortByDescending(item => item.OccurredAtUtc)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    internal FilterDefinition<AdminAuditEvent> BuildAuditFilter(AdminAuditFilterRequest request, bool includeCursor)
    {
        var builder = Builders<AdminAuditEvent>.Filter;
        var filter = builder.Empty;
        if (request.FromUtc is not null) filter &= builder.Gte(item => item.OccurredAtUtc, request.FromUtc.Value);
        if (request.ToUtc is not null) filter &= builder.Lte(item => item.OccurredAtUtc, request.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(request.ActorId)) filter &= builder.Eq(item => item.ActorId, request.ActorId);
        if (!string.IsNullOrWhiteSpace(request.DomainCode)) filter &= builder.Eq(item => item.DomainCode, request.DomainCode);
        if (!string.IsNullOrWhiteSpace(request.ActionCode)) filter &= builder.Eq(item => item.ActionCode, request.ActionCode);
        if (request.Outcome is not null) filter &= builder.Eq(item => item.Outcome, request.Outcome.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(request.Search.Trim()), "i");
            filter &= builder.Or(
                builder.Regex(item => item.ActorEmail, regex),
                builder.Regex(item => item.ActorDisplayName, regex),
                builder.Regex(item => item.ActionCode, regex),
                builder.Regex(item => item.DomainCode, regex),
                builder.Regex(item => item.TargetLabel!, regex),
                builder.Regex(item => item.ResultMessage, regex));
        }
        if (includeCursor && TryDecodeCursor(request.Cursor, out var at, out var id) && ObjectId.TryParse(id, out _))
        {
            filter &= builder.Or(
                builder.Lt(item => item.OccurredAtUtc, at),
                builder.And(builder.Eq(item => item.OccurredAtUtc, at), builder.Lt(item => item.Id, id)));
        }
        return filter;
    }

    internal FilterDefinition<AdminLoginActivityEvent> BuildLoginFilter(AdminLoginActivityFilterRequest request, bool includeCursor)
    {
        var builder = Builders<AdminLoginActivityEvent>.Filter;
        var filter = builder.Empty;
        if (request.FromUtc is not null) filter &= builder.Gte(item => item.OccurredAtUtc, request.FromUtc.Value);
        if (request.ToUtc is not null) filter &= builder.Lte(item => item.OccurredAtUtc, request.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(request.AdminId)) filter &= builder.Eq(item => item.AdminId, request.AdminId);
        if (!string.IsNullOrWhiteSpace(request.EventCode)) filter &= builder.Eq(item => item.EventCode, request.EventCode);
        if (request.Outcome is not null) filter &= builder.Eq(item => item.Outcome, request.Outcome.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(request.Search.Trim()), "i");
            filter &= builder.Or(
                builder.Regex(item => item.AccountEmail, regex),
                builder.Regex(item => item.AccountDisplayName, regex),
                builder.Regex(item => item.EventCode, regex),
                builder.Regex(item => item.IpAddress, regex));
        }
        if (includeCursor && TryDecodeCursor(request.Cursor, out var at, out var id) && ObjectId.TryParse(id, out _))
        {
            filter &= builder.Or(
                builder.Lt(item => item.OccurredAtUtc, at),
                builder.And(builder.Eq(item => item.OccurredAtUtc, at), builder.Lt(item => item.Id, id)));
        }
        return filter;
    }

    private static BsonDocument BuildAuditMatch(AdminAuditFilterRequest request)
    {
        var match = new BsonDocument();
        var dates = new BsonDocument();
        if (request.FromUtc is not null) dates["$gte"] = request.FromUtc.Value;
        if (request.ToUtc is not null) dates["$lte"] = request.ToUtc.Value;
        if (dates.ElementCount > 0) match["OccurredAtUtc"] = dates;
        if (!string.IsNullOrWhiteSpace(request.ActorId)) match["ActorId"] = request.ActorId;
        if (!string.IsNullOrWhiteSpace(request.DomainCode)) match["DomainCode"] = request.DomainCode;
        if (!string.IsNullOrWhiteSpace(request.ActionCode)) match["ActionCode"] = request.ActionCode;
        if (request.Outcome is not null) match["Outcome"] = request.Outcome.Value.ToString();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var regex = new BsonRegularExpression(Regex.Escape(request.Search.Trim()), "i");
            match["$or"] = new BsonArray
            {
                new BsonDocument("ActorEmail", regex),
                new BsonDocument("ActorDisplayName", regex),
                new BsonDocument("ActionCode", regex),
                new BsonDocument("DomainCode", regex),
                new BsonDocument("TargetLabel", regex),
                new BsonDocument("ResultMessage", regex)
            };
        }
        return match;
    }

    private static BsonDocument CountOutcome(AdminAuditOutcome outcome) =>
        new("$sum", new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$Outcome", outcome.ToString() }), 1, 0
        }));

    public static AdminAuditEventResponse MapAudit(AdminAuditEvent item) => new()
    {
        Id = item.Id,
        OccurredAtUtc = item.OccurredAtUtc,
        DomainCode = item.DomainCode,
        ActionCode = item.ActionCode,
        Outcome = item.Outcome,
        Severity = item.Severity,
        ActorId = item.ActorId,
        ActorEmail = item.ActorEmail,
        ActorDisplayName = item.ActorDisplayName,
        ActorRoleName = item.ActorRoleName,
        SessionId = item.SessionId,
        CorrelationId = item.CorrelationId,
        BatchId = item.BatchId,
        TargetTypeCode = item.TargetTypeCode,
        TargetId = item.TargetId,
        TargetLabel = item.TargetLabel,
        ChangeCount = item.ChangeCount,
        ChangedFields = item.ChangedFields,
        Message = item.ResultMessage,
        IpAddress = item.IpAddress,
        BrowserName = item.BrowserName,
        OperatingSystem = item.OperatingSystem,
        RequestPath = item.RequestPath,
        RequestMethod = item.RequestMethod,
        IsLegacy = item.Source == "legacy"
    };

    public static AdminLoginActivityEventResponse MapLogin(AdminLoginActivityEvent item) => new()
    {
        Id = item.Id,
        OccurredAtUtc = item.OccurredAtUtc,
        EventCode = item.EventCode,
        Outcome = item.Outcome,
        ReasonCode = item.ReasonCode,
        AdminId = item.AdminId,
        AccountDisplay = string.IsNullOrWhiteSpace(item.AccountDisplayName) ? item.AccountEmail : item.AccountDisplayName,
        SessionId = item.SessionId,
        CorrelationId = item.CorrelationId,
        IpAddress = item.IpAddress,
        BrowserName = item.BrowserName,
        OperatingSystem = item.OperatingSystem,
        Message = item.ResultMessage,
        IsLegacy = item.Source == "legacy"
    };

    private static AdminAuditSessionSummaryResponse MapSummary(BsonDocument document)
    {
        var first = document["FirstEventAtUtc"].ToUniversalTime();
        var last = document["LastEventAtUtc"].ToUniversalTime();
        var sessionStarted = document.GetValue("SessionStartedAtUtc", document["FirstEventAtUtc"]).ToUniversalTime();
        var sessionLastActivity = document.GetValue("SessionLastActivityAtUtc", document["LastEventAtUtc"]).ToUniversalTime();
        if (sessionLastActivity < last) sessionLastActivity = last;
        var failed = document.GetValue("FailedCount", 0).ToInt32();
        var denied = document.GetValue("DeniedCount", 0).ToInt32();
        var partial = document.GetValue("PartialCount", 0).ToInt32();
        return new AdminAuditSessionSummaryResponse
        {
            SessionKey = document["_id"].AsString,
            SessionId = document.GetValue("SessionId", BsonNull.Value).IsString ? document["SessionId"].AsString : null,
            FirstEventAtUtc = first,
            LastEventAtUtc = last,
            SessionStartedAtUtc = sessionStarted,
            SessionLastActivityAtUtc = sessionLastActivity,
            DurationSeconds = Math.Max(0, (long)(sessionLastActivity - sessionStarted).TotalSeconds),
            ActorId = document.GetValue("ActorId", "").AsString,
            ActorEmail = document.GetValue("ActorEmail", "").AsString,
            ActorDisplayName = document.GetValue("ActorDisplayName", "").AsString,
            ActorRoleName = document.GetValue("ActorRoleName", "").AsString,
            EventCount = document.GetValue("EventCount", 0).ToInt32(),
            SucceededCount = document.GetValue("SucceededCount", 0).ToInt32(),
            FailedCount = failed,
            DeniedCount = denied,
            DomainCodes = document.GetValue("DomainCodes", new BsonArray()).AsBsonArray.Where(value => value.IsString).Select(value => value.AsString).Order().ToList(),
            OverallOutcome = failed > 0 ? AdminAuditOutcome.Failed : denied > 0 ? AdminAuditOutcome.Denied : partial > 0 ? AdminAuditOutcome.Partial : AdminAuditOutcome.Succeeded
        };
    }

    private static string EncodeCursor(DateTime occurredAtUtc, string id)
    {
        var payload = $"{occurredAtUtc.ToUniversalTime().Ticks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? cursor, out DateTime occurredAtUtc, out string id)
    {
        occurredAtUtc = default;
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(normalized)).Split('|', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks)) return false;
            occurredAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            id = parts[1];
            return true;
        }
        catch
        {
            return false;
        }
    }
}
