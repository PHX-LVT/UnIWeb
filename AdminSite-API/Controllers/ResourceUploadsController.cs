using Contracts.Admin;
using FullProject.Security;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FullProject.Controllers;

[ApiController]
[Route("api/admin/resource-uploads")]
[Authorize]
public sealed class ResourceUploadsController : ControllerBase
{
    private readonly ResourceUploadSessionService _uploads;
    private readonly ILogger<ResourceUploadsController> _logger;

    public ResourceUploadsController(ResourceUploadSessionService uploads, ILogger<ResourceUploadsController> logger)
    {
        _uploads = uploads;
        _logger = logger;
    }

    [HttpGet("capabilities")]
    [EnableRateLimiting("admin-resource-upload-read")]
    public async Task<IActionResult> Capabilities()
    {
        if (!CanUseResourceLibrary) return Forbid();
        return Ok(ApiResult.Ok(await _uploads.GetCapabilitiesAsync()));
    }

    [HttpPost]
    [EnableRateLimiting("admin-resource-upload-initiate")]
    public Task<IActionResult> Initiate([FromBody] ResourceUploadInitiateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ReplaceResourceId) && !IsContentManager)
            return Task.FromResult<IActionResult>(Forbid());
        return ExecuteAsync(async () => Created(string.Empty, ApiResult.Created(
            await _uploads.InitiateAsync(request, ActorId, HttpContext.RequestAborted),
            "Upload session created.")));
    }

    [HttpGet("{id}")]
    [EnableRateLimiting("admin-resource-upload-read")]
    public Task<IActionResult> Get(string id) =>
        ExecuteAsync(async () => Ok(ApiResult.Ok(await _uploads.GetAsync(id, ActorId, HttpContext.RequestAborted))));

    [HttpPost("{id}/parts")]
    [EnableRateLimiting("admin-resource-upload-mutation")]
    public Task<IActionResult> Parts(string id, [FromBody] ResourceUploadPartUrlsRequest request) =>
        ExecuteAsync(async () => Ok(ApiResult.Ok(await _uploads.CreatePartUrlsAsync(id, request, ActorId, HttpContext.RequestAborted))));

    [HttpPost("{id}/complete")]
    [EnableRateLimiting("admin-resource-upload-mutation")]
    public Task<IActionResult> Complete(string id, [FromBody] ResourceUploadCompleteRequest request) =>
        ExecuteAsync(async () => Ok(ApiResult.Ok(
            await _uploads.CompleteAsync(id, request, ActorId, HttpContext.RequestAborted),
            "Upload verified and added to the Resource Library.")));

    [HttpPost("{id}/abort")]
    [EnableRateLimiting("admin-resource-upload-mutation")]
    public Task<IActionResult> Abort(string id) =>
        ExecuteAsync(async () => Ok(ApiResult.Ok(
            await _uploads.AbortAsync(id, ActorId, HttpContext.RequestAborted),
            "Upload cancelled.")));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        if (!CanUpload) return Forbid();
        try { return await action(); }
        catch (ResourceUploadException exception)
        {
            return StatusCode(exception.StatusCode, ApiResult.BadRequest(exception.Message, [exception.Code]));
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Resource upload operation failed for actor {ActorId}", ActorId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResult.ServerError("The upload operation failed."));
        }
    }

    private string ActorId =>
        User.FindFirst("adminId")?.Value ?? User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "unknown";

    private bool IsContentManager =>
        AdminAuthorization.IsAdminAdmin(User) || AdminAuthorization.HasPermission(User, AdminPermissionKeys.ApproveContent);

    private bool CanUseResourceLibrary =>
        IsContentManager || AdminAuthorization.HasPermission(User, AdminPermissionKeys.CreateEditContent);

    private bool CanUpload => CanUseResourceLibrary;
}
