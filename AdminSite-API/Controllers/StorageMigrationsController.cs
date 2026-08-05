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
    private readonly ManagedImageBackfillService _managedImages;

    public StorageMigrationsController(
        StorageMigrationService migrations,
        ManagedImageBackfillService managedImages)
    {
        _migrations = migrations;
        _managedImages = managedImages;
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

    [HttpPost("backfill-managed-images")]
    public async Task<IActionResult> BackfillManagedImages(
        [FromBody] ManagedImageBackfillRequest request,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
        if (!string.IsNullOrWhiteSpace(request.AfterAssetId) &&
            !MongoDB.Bson.ObjectId.TryParse(request.AfterAssetId, out _))
        {
            return UnprocessableEntity(ApiResult.BadRequest("The managed image backfill cursor is invalid."));
        }
        var result = await _managedImages.RunAsync(request, ActorId, cancellationToken);
        var message = request.DryRun
            ? $"Managed image backfill preview found {result.WouldCreateCount} image(s) to register."
            : $"Managed image backfill registered {result.CreatedCount} image(s); {result.FailedCount} failed.";
        return Ok(ApiResult.Ok(result, message));
    }

    private string ActorId =>
        User.FindFirst("adminId")?.Value ??
        User.FindFirst("sub")?.Value ??
        User.Identity?.Name ??
        "unknown";
}
