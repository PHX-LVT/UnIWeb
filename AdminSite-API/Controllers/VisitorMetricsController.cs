using Contracts.Auth;
using FullProject.Security;
using FullProject.Services.Metrics;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/visitor-metrics")]
public sealed class VisitorMetricsController : ControllerBase
{
    private readonly VisitorMetricService _metrics;

    public VisitorMetricsController(VisitorMetricService metrics)
    {
        _metrics = metrics;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? metricType, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!AdminAuthorization.HasPermission(User, AdminPermissionKeys.ViewLogs))
            return Forbid();

        var metrics = await _metrics.GetAsync(metricType, from, to);
        return Ok(ApiResult.Ok(metrics));
    }
}
