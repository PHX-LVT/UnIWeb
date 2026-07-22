using Contracts.Auth;
using FullProject.Models;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using FullProject.Services.Health;

namespace FullProject.Services.LogManagement;

public sealed class AuditWriteRequest
{
    public string DomainCode { get; init; } = string.Empty;
    public string ActionCode { get; init; } = string.Empty;
    public AdminAuditOutcome Outcome { get; init; } = AdminAuditOutcome.Succeeded;
    public AdminAuditSeverity Severity { get; init; } = AdminAuditSeverity.Information;
    public string? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorDisplayName { get; init; }
    public string? ActorRoleId { get; init; }
    public string? ActorRoleName { get; init; }
    public string? SessionId { get; init; }
    public string? CorrelationId { get; init; }
    public string? BatchId { get; init; }
    public string? TargetTypeCode { get; init; }
    public string? TargetId { get; init; }
    public string? TargetLabel { get; init; }
    public int ChangeCount { get; init; } = 1;
    public IEnumerable<string>? ChangedFields { get; init; }
    public IReadOnlyDictionary<string, string?>? SafeBefore { get; init; }
    public IReadOnlyDictionary<string, string?>? SafeAfter { get; init; }
    public string MessageKey { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string>? MessageParameters { get; init; }
    public string ResultMessage { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestPath { get; init; }
    public string? RequestMethod { get; init; }
    public string RetentionClass { get; init; } = "standard";
}

public sealed class LoginActivityWriteRequest
{
    public string EventCode { get; init; } = string.Empty;
    public AdminAuditOutcome Outcome { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string? AdminId { get; init; }
    public string? AccountEmail { get; init; }
    public string? AccountDisplayName { get; init; }
    public string? AttemptedIdentifier { get; init; }
    public string? SessionId { get; init; }
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string MessageKey { get; init; } = string.Empty;
    public string ResultMessage { get; init; } = string.Empty;
}

public interface IAuditTrailWriter
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}

public interface ILoginActivityWriter
{
    Task WriteAsync(LoginActivityWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuditRedactionPolicy
{
    private static readonly string[] ProhibitedTokens =
        ["password", "secret", "token", "cookie", "authorization", "credential", "form-value", "body"];

    public string Clean(string? value, int maximumLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    public List<string> CleanFields(IEnumerable<string>? fields) =>
        (fields ?? [])
            .Where(field => !string.IsNullOrWhiteSpace(field) &&
                !ProhibitedTokens.Any(token => field.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(field => Clean(field, 100))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

    public Dictionary<string, string?> CleanSnapshot(IReadOnlyDictionary<string, string?>? values) =>
        (values ?? new Dictionary<string, string?>())
            .Where(pair => !ProhibitedTokens.Any(token => pair.Key.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Take(30)
            .ToDictionary(pair => Clean(pair.Key, 100), pair => (string?)Clean(pair.Value, 250));
}

public sealed class AuditTrailWriter : IAuditTrailWriter
{
    private readonly IMongoCollection<AdminAuditEvent> _events;
    private readonly IHttpContextAccessor _http;
    private readonly AuditRedactionPolicy _redaction;
    private readonly LogPersistenceHealthState _health;
    private readonly ILogger<AuditTrailWriter> _logger;

    public AuditTrailWriter(
        IMongoDatabase database,
        IHttpContextAccessor http,
        AuditRedactionPolicy redaction,
        LogPersistenceHealthState health,
        ILogger<AuditTrailWriter> logger)
    {
        _events = database.GetCollection<AdminAuditEvent>("admin_audit_events");
        _http = http;
        _redaction = redaction;
        _health = health;
        _logger = logger;
    }

    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var context = _http.HttpContext;
        var principal = context?.User;
        var userAgent = request.UserAgent ?? context?.Request.Headers.UserAgent.FirstOrDefault();
        var client = AdminClientInfoParser.Parse(userAgent);
        var actorEmail = request.ActorEmail ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
        var item = new AdminAuditEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            DomainCode = _redaction.Clean(request.DomainCode, 80),
            ActionCode = _redaction.Clean(request.ActionCode, 120),
            Outcome = request.Outcome,
            Severity = request.Severity,
            ActorId = _redaction.Clean(request.ActorId ?? principal?.FindFirst("adminId")?.Value, 80),
            ActorEmail = _redaction.Clean(actorEmail, 180),
            ActorDisplayName = _redaction.Clean(request.ActorDisplayName ?? actorEmail, 180),
            ActorRoleId = _redaction.Clean(request.ActorRoleId ?? principal?.FindFirst("roleId")?.Value, 80),
            ActorRoleName = _redaction.Clean(request.ActorRoleName ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, 100),
            SessionId = _redaction.Clean(request.SessionId ?? principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? principal?.FindFirst("jti")?.Value, 100) is { Length: > 0 } session ? session : null,
            CorrelationId = _redaction.Clean(request.CorrelationId ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), 100),
            BatchId = _redaction.Clean(request.BatchId, 100) is { Length: > 0 } batch ? batch : null,
            TargetTypeCode = _redaction.Clean(request.TargetTypeCode, 80) is { Length: > 0 } type ? type : null,
            TargetId = _redaction.Clean(request.TargetId, 160) is { Length: > 0 } target ? target : null,
            TargetLabel = _redaction.Clean(request.TargetLabel, 250) is { Length: > 0 } label ? label : null,
            ChangeCount = Math.Max(0, request.ChangeCount),
            ChangedFields = _redaction.CleanFields(request.ChangedFields),
            SafeBefore = _redaction.CleanSnapshot(request.SafeBefore),
            SafeAfter = _redaction.CleanSnapshot(request.SafeAfter),
            MessageKey = _redaction.Clean(request.MessageKey, 160),
            MessageParameters = (request.MessageParameters ?? new Dictionary<string, string>())
                .Take(20).ToDictionary(pair => _redaction.Clean(pair.Key, 80), pair => _redaction.Clean(pair.Value, 200)),
            ResultMessage = _redaction.Clean(request.ResultMessage, 500),
            IpAddress = _redaction.Clean(request.IpAddress ?? context?.Connection.RemoteIpAddress?.ToString(), 80),
            UserAgent = _redaction.Clean(userAgent, 500),
            BrowserName = client.Browser,
            OperatingSystem = client.OperatingSystem,
            RequestPath = _redaction.Clean(request.RequestPath ?? context?.Request.Path.Value, 300),
            RequestMethod = _redaction.Clean(request.RequestMethod ?? context?.Request.Method, 20),
            RetentionClass = _redaction.Clean(request.RetentionClass, 40)
        };
        try
        {
            await _events.InsertOneAsync(item, cancellationToken: cancellationToken);
            _health.MarkSucceeded();
            if (context is not null) context.Items["AuditTrail.ExplicitWritten"] = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _health.MarkFailed(ex);
            _logger.LogCritical(
                ex,
                "Security audit persistence failed for {DomainCode}/{ActionCode}. CorrelationId: {CorrelationId}",
                item.DomainCode,
                item.ActionCode,
                item.CorrelationId);
        }
    }
}

public sealed class LoginActivityWriter : ILoginActivityWriter
{
    private readonly IMongoCollection<AdminLoginActivityEvent> _events;
    private readonly IHttpContextAccessor _http;
    private readonly AuditRedactionPolicy _redaction;
    private readonly LogPersistenceHealthState _health;
    private readonly ILogger<LoginActivityWriter> _logger;

    public LoginActivityWriter(
        IMongoDatabase database,
        IHttpContextAccessor http,
        AuditRedactionPolicy redaction,
        LogPersistenceHealthState health,
        ILogger<LoginActivityWriter> logger)
    {
        _events = database.GetCollection<AdminLoginActivityEvent>("admin_login_activity_events");
        _http = http;
        _redaction = redaction;
        _health = health;
        _logger = logger;
    }

    public async Task WriteAsync(LoginActivityWriteRequest request, CancellationToken cancellationToken = default)
    {
        var context = _http.HttpContext;
        var userAgent = request.UserAgent ?? context?.Request.Headers.UserAgent.FirstOrDefault();
        var client = AdminClientInfoParser.Parse(userAgent);
        var knownAccount = !string.IsNullOrWhiteSpace(request.AdminId);
        var accountEmail = knownAccount ? _redaction.Clean(request.AccountEmail, 180) : string.Empty;
        var attemptedHash = knownAccount || string.IsNullOrWhiteSpace(request.AttemptedIdentifier)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.AttemptedIdentifier.Trim().ToLowerInvariant())));
        var item = new AdminLoginActivityEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            EventCode = _redaction.Clean(request.EventCode, 100),
            Outcome = request.Outcome,
            ReasonCode = _redaction.Clean(request.ReasonCode, 120),
            AdminId = knownAccount ? _redaction.Clean(request.AdminId, 80) : null,
            AccountEmail = accountEmail,
            AccountDisplayName = knownAccount
                ? _redaction.Clean(request.AccountDisplayName ?? request.AccountEmail, 180)
                : MaskIdentifier(request.AttemptedIdentifier),
            AttemptedIdentifierHash = attemptedHash,
            SessionId = _redaction.Clean(request.SessionId, 100) is { Length: > 0 } session ? session : null,
            CorrelationId = _redaction.Clean(request.CorrelationId ?? context?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), 100),
            IpAddress = _redaction.Clean(request.IpAddress ?? context?.Connection.RemoteIpAddress?.ToString(), 80),
            UserAgent = _redaction.Clean(userAgent, 500),
            BrowserName = client.Browser,
            OperatingSystem = client.OperatingSystem,
            MessageKey = _redaction.Clean(request.MessageKey, 160),
            ResultMessage = _redaction.Clean(request.ResultMessage, 500)
        };
        try
        {
            await _events.InsertOneAsync(item, cancellationToken: cancellationToken);
            _health.MarkSucceeded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _health.MarkFailed(ex);
            _logger.LogCritical(
                ex,
                "Security login-activity persistence failed for {EventCode}. CorrelationId: {CorrelationId}",
                item.EventCode,
                item.CorrelationId);
        }
    }

    private static string MaskIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown account";
        var parts = value.Trim().Split('@', 2);
        if (parts.Length != 2) return "Unknown account";
        var local = parts[0];
        var visible = local.Length == 0 ? "*" : local[..1] + new string('*', Math.Min(5, Math.Max(1, local.Length - 1)));
        return $"{visible}@{parts[1]}";
    }
}

internal static class AdminClientInfoParser
{
    public static (string Browser, string OperatingSystem) Parse(string? userAgent)
    {
        var value = userAgent ?? string.Empty;
        var browser = value.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" :
            value.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
            value.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
            value.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari" : "Unknown";
        var os = value.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
            value.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
            value.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || value.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS" :
            value.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS" :
            value.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" : "Unknown";
        return (browser, os);
    }
}
