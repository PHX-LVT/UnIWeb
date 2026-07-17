using Contracts.Auth;
using FullProject.Security;
using FullProject.Services.LogManagement;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FullProject.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/log-management")]
public sealed class LogManagementController : ControllerBase
{
    private readonly LogManagementQueryService _queries;
    private readonly LogExportService _exports;
    private readonly LogRetentionService _retention;

    public LogManagementController(
        LogManagementQueryService queries,
        LogExportService exports,
        LogRetentionService retention)
    {
        _queries = queries;
        _exports = exports;
        _retention = retention;
    }

    [HttpGet("audit/sessions")]
    public async Task<IActionResult> GetAuditSessions(
        [FromQuery] AdminAuditFilterRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(AdminPermissionKeys.ViewAuditTrail)) return Forbid();
        return Ok(ApiResult.Ok(await _queries.GetAuditSessionsAsync(request, cancellationToken)));
    }

    [HttpGet("audit/sessions/{sessionKey}/events")]
    public async Task<IActionResult> GetAuditSessionEvents(string sessionKey, CancellationToken cancellationToken)
    {
        if (!HasPermission(AdminPermissionKeys.ViewAuditTrail)) return Forbid();
        var items = await _queries.GetSessionEventsAsync(sessionKey, cancellationToken);
        if (!AdminAuthorization.IsAdminAdmin(User)) items.ForEach(RedactAuditSensitiveFields);
        return Ok(ApiResult.Ok(items));
    }

    [HttpGet("audit/events")]
    public async Task<IActionResult> GetAuditEvents(
        [FromQuery] AdminAuditFilterRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(AdminPermissionKeys.ViewAuditTrail)) return Forbid();
        var result = await _queries.GetAuditEventsAsync(request, cancellationToken);
        if (!AdminAuthorization.IsAdminAdmin(User)) result.Items.ForEach(RedactAuditSensitiveFields);
        return Ok(ApiResult.Ok(result));
    }

    [HttpGet("login-activity")]
    public async Task<IActionResult> GetLoginActivity(
        [FromQuery] AdminLoginActivityFilterRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(AdminPermissionKeys.ViewLoginActivity)) return Forbid();
        var result = await _queries.GetLoginActivityAsync(request, cancellationToken);
        if (!AdminAuthorization.IsAdminAdmin(User)) result.Items.ForEach(RedactLoginSensitiveFields);
        return Ok(ApiResult.Ok(result));
    }

    [HttpPost("exports")]
    public async Task<IActionResult> Export(
        [FromBody] AdminLogExportRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(AdminPermissionKeys.ExportLogs)) return Forbid();
        var isLogin = request.LogType.Equals("login-activity", StringComparison.OrdinalIgnoreCase);
        if (isLogin && !HasPermission(AdminPermissionKeys.ViewLoginActivity)) return Forbid();
        if (!isLogin && !HasPermission(AdminPermissionKeys.ViewAuditTrail)) return Forbid();
        if (request.FromUtc == default || request.ToUtc == default || request.FromUtc >= request.ToUtc)
            return BadRequest(ApiResult.BadRequest("A valid export date range is required."));
        if ((request.ToUtc - request.FromUtc) > TimeSpan.FromDays(1095))
            return BadRequest(ApiResult.BadRequest("An export date range cannot exceed three years."));
        if (!request.Format.Equals("csv", StringComparison.OrdinalIgnoreCase) &&
            !request.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResult.BadRequest("Export format must be csv or json."));
        if (!isLogin && !request.LogType.Equals("audit", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResult.BadRequest("Log type must be audit or login-activity."));

        var extension = request.Format.ToLowerInvariant();
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = extension == "json" ? "application/json; charset=utf-8" : "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{request.LogType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}\"";
        var rowCount = await _exports.WriteAsync(
            Response.Body,
            request,
            AdminAuthorization.IsAdminAdmin(User),
            cancellationToken);
        await _exports.RecordAsync(
            request,
            User.FindFirst("adminId")?.Value ?? string.Empty,
            User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            rowCount,
            cancellationToken);
        return new EmptyResult();
    }

    [HttpGet("retention")]
    public async Task<IActionResult> GetRetention(CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        return Ok(ApiResult.Ok(await _retention.GetStatusAsync(cancellationToken)));
    }

    [HttpPost("retention/run")]
    public async Task<IActionResult> RunRetention(CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        try
        {
            var result = await _retention.RunAsync(User.FindFirst("adminId")?.Value ?? string.Empty, cancellationToken);
            return Ok(ApiResult.Ok(result, "Log retention completed."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResult.BadRequest(ex.Message));
        }
    }

    private bool HasPermission(string permission) => AdminAuthorization.HasPermission(User, permission);

    private static void RedactAuditSensitiveFields(AdminAuditEventResponse item)
    {
        item.IpAddress = MaskIp(item.IpAddress);
        item.CorrelationId = string.Empty;
        item.RequestPath = string.Empty;
    }

    private static void RedactLoginSensitiveFields(AdminLoginActivityEventResponse item)
    {
        item.IpAddress = MaskIp(item.IpAddress);
        item.CorrelationId = string.Empty;
    }

    private static string MaskIp(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var segments = value.Split('.');
        if (segments.Length == 4) return $"{segments[0]}.{segments[1]}.{segments[2]}.*";
        var separator = value.LastIndexOf(':');
        return separator > 0 ? value[..separator] + ":*" : "masked";
    }
}
