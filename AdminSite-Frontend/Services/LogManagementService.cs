using System.Globalization;

namespace AdminSite.Services;

public sealed class LogManagementService
{
    private readonly IHttpService _http;

    public LogManagementService(IHttpService http)
    {
        _http = http;
    }

    public Task<ApiResponse<AdminLogCursorPageResponse<AdminAuditSessionSummaryResponse>>> GetAuditSessionsAsync(
        AdminAuditFilterRequest request) =>
        _http.GetAsync<AdminLogCursorPageResponse<AdminAuditSessionSummaryResponse>>(
            $"api/admin/log-management/audit/sessions{AuditQuery(request)}");

    public Task<ApiResponse<List<AdminAuditEventResponse>>> GetSessionEventsAsync(string sessionKey) =>
        _http.GetAsync<List<AdminAuditEventResponse>>(
            $"api/admin/log-management/audit/sessions/{Uri.EscapeDataString(sessionKey)}/events");

    public Task<ApiResponse<AdminLogCursorPageResponse<AdminAuditEventResponse>>> GetAuditEventsAsync(
        AdminAuditFilterRequest request) =>
        _http.GetAsync<AdminLogCursorPageResponse<AdminAuditEventResponse>>(
            $"api/admin/log-management/audit/events{AuditQuery(request)}");

    public Task<ApiResponse<AdminLogCursorPageResponse<AdminLoginActivityEventResponse>>> GetLoginActivityAsync(
        AdminLoginActivityFilterRequest request) =>
        _http.GetAsync<AdminLogCursorPageResponse<AdminLoginActivityEventResponse>>(
            $"api/admin/log-management/login-activity{LoginQuery(request)}");

    public Task<FileDownloadResult> ExportAsync(AdminLogExportRequest request) =>
        _http.PostFileDownloadAsync("api/admin/log-management/exports", request);

    public Task<ApiResponse<AdminLogRetentionStatusResponse>> GetRetentionAsync() =>
        _http.GetAsync<AdminLogRetentionStatusResponse>("api/admin/log-management/retention");

    public Task<ApiResponse<AdminLogRetentionStatusResponse>> RunRetentionAsync() =>
        _http.PostAsync<AdminLogRetentionStatusResponse>("api/admin/log-management/retention/run", new { });

    private static string AuditQuery(AdminAuditFilterRequest request)
    {
        var values = Common(request.FromUtc, request.ToUtc, request.ActorId, request.Outcome, request.Search, request.Cursor, request.PageSize);
        Add(values, "domainCode", request.DomainCode);
        Add(values, "actionCode", request.ActionCode);
        return values.Count == 0 ? string.Empty : "?" + string.Join('&', values);
    }

    private static string LoginQuery(AdminLoginActivityFilterRequest request)
    {
        var values = Common(request.FromUtc, request.ToUtc, request.AdminId, request.Outcome, request.Search, request.Cursor, request.PageSize);
        if (!string.IsNullOrWhiteSpace(request.AdminId))
        {
            values.RemoveAll(value => value.StartsWith("actorId=", StringComparison.Ordinal));
            Add(values, "adminId", request.AdminId);
        }
        Add(values, "eventCode", request.EventCode);
        return values.Count == 0 ? string.Empty : "?" + string.Join('&', values);
    }

    private static List<string> Common(
        DateTime? from,
        DateTime? to,
        string? actorId,
        AdminAuditOutcome? outcome,
        string? search,
        string? cursor,
        int pageSize)
    {
        var values = new List<string>();
        if (from is not null) Add(values, "fromUtc", from.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (to is not null) Add(values, "toUtc", to.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(values, "actorId", actorId);
        if (outcome is not null) Add(values, "outcome", outcome.Value.ToString());
        Add(values, "search", search);
        Add(values, "cursor", cursor);
        values.Add($"pageSize={Math.Clamp(pageSize, 10, 100)}");
        return values;
    }

    private static void Add(List<string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) values.Add($"{key}={Uri.EscapeDataString(value)}");
    }
}
