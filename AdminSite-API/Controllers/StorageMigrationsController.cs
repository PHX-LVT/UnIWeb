using Contracts.Admin;
using Contracts.Api;
using FullProject.Security;
using FullProject.Services.AssetService;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers;

[ApiController]
[Route("api/admin/storage-migrations")]
[Authorize]
public sealed class StorageMigrationsController : ControllerBase
{
    private readonly StorageMigrationService _migrations;

    public StorageMigrationsController(StorageMigrationService migrations)
    {
        _migrations = migrations;
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory(CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        return Ok(ApiResult.Ok(await _migrations.InventoryAsync(cancellationToken)));
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Plan([FromBody] StorageMigrationPlanRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        var result = await _migrations.PlanAsync(request, cancellationToken);
        return Ok(ApiResult.Ok(result, request.DryRun ? "Storage migration dry run completed." : "Storage migration plan saved."));
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] StorageMigrationExecuteRequest request, CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        var result = await _migrations.ExecuteAsync(request, ActorId, cancellationToken);
        return Ok(ApiResult.Ok(result, $"Completed {result.CompletedCount} migration(s); {result.FailedCount} failed."));
    }

    private string ActorId =>
        User.FindFirst("adminId")?.Value ??
        User.FindFirst("sub")?.Value ??
        User.Identity?.Name ??
        "unknown";
}
