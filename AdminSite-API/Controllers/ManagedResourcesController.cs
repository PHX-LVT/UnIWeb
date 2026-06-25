using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Utils;
using GlobalManager.Services.AssetService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/resources")]
    [Authorize]
    public class ManagedResourcesController : ControllerBase
    {
        private readonly ManagedResourceService _resources;
        private readonly R2StorageService _storage;
        private readonly R2StorageSettings _settings;
        private readonly SettingsService _siteSettings;

        public ManagedResourcesController(
            ManagedResourceService resources,
            R2StorageService storage,
            IOptions<R2StorageSettings> settings,
            SettingsService siteSettings)
        {
            _resources = resources;
            _storage = storage;
            _settings = settings.Value;
            _siteSettings = siteSettings;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? kind = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = true)
        {
            if (!IsResourceLibraryReader) return Forbid();
            if (!IsContentManager) includeInactive = false;

            var resources = await _resources.GetAllAsync(kind, search, includeInactive);
            var usageCounts = await _resources.GetUsageCountsAsync(resources);
            return Ok(ApiResult.Ok(resources.Select(resource =>
                MapResource(resource, usageCounts.GetValueOrDefault(resource.Id))).ToList()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            if (!IsResourceLibraryReader) return Forbid();

            var resource = await _resources.GetByIdAsync(id);
            if (resource is null) return NotFound(ApiResult.NotFound("Resource not found."));
            if (!IsContentManager && !resource.Active) return NotFound(ApiResult.NotFound("Resource not found."));
            var usage = await _resources.GetUsageAsync(resource.Id);
            return Ok(ApiResult.Ok(MapResource(resource, usage.UsageCount)));
        }

        [HttpGet("{id}/usage")]
        public async Task<IActionResult> GetUsage(string id)
        {
            if (!IsResourceLibraryReader) return Forbid();

            var resource = await _resources.GetByIdAsync(id);
            if (resource is null) return NotFound(ApiResult.NotFound("Resource not found."));
            if (!IsContentManager && !resource.Active) return NotFound(ApiResult.NotFound("Resource not found."));

            var usage = await _resources.GetUsageAsync(resource.Id);
            return Ok(ApiResult.Ok(usage));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ManagedResourceCreateDto dto)
        {
            if (!IsContentManager) return Forbid();
            if (string.Equals(_resources.NormalizeKind(dto.Kind), "video", StringComparison.OrdinalIgnoreCase))
                return UnprocessableEntity(ApiResult.BadRequest("Create Resource Library videos by uploading a video file."));

            var (resource, errors) = await _resources.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));
            return Ok(ApiResult.Created(MapResource(resource!, 0), "Resource created."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ManagedResourceUpdateDto dto)
        {
            if (!IsContentManager) return Forbid();

            var (resource, errors) = await _resources.UpdateAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Resource not found.")) return NotFound(ApiResult.NotFound("Resource not found."));
                return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));
            }

            var usage = await _resources.GetUsageAsync(resource!.Id);
            return Ok(ApiResult.Ok(MapResource(resource!, usage.UsageCount), "Resource updated."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!IsContentManager) return Forbid();

            var (deleted, usage, errors) = await _resources.DeleteAsync(id);
            if (errors.Count > 0)
            {
                if (errors.Contains("Resource not found."))
                    return NotFound(ApiResult.NotFound("Resource not found."));

                return Conflict(ApiResult.BadRequest(errors[0], errors));
            }

            return Ok(ApiResult.Ok(new { deleted, usage?.UsageCount }, "Resource deleted."));
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(250 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] ManagedResourceUploadRequest request)
        {
            if (!IsContentManager) return Forbid();

            var validation = await ValidateUploadAsync(request);
            if (validation.Error is not null) return validation.Error;

            var file = validation.File!;
            var inferredKind = validation.Kind!;
            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadWithMetadataAsync(stream, file.FileName, file.ContentType, "managed-resources", HttpContext.RequestAborted);
            var dto = _resources.BuildUploadCreateDto(upload.Url, upload.StorageKey, inferredKind, file.FileName, file.ContentType, file.Length);
            var (resource, errors) = await _resources.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));

            return Ok(ApiResult.Created(MapResource(resource!, 0), "Resource uploaded."));
        }

        [HttpPost("{id}/replace")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(250 * 1024 * 1024)]
        public async Task<IActionResult> ReplaceUpload(string id, [FromForm] ManagedResourceUploadRequest request)
        {
            if (!IsContentManager) return Forbid();

            var existing = await _resources.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Resource not found."));

            var validation = await ValidateUploadAsync(request, existing.Kind);
            if (validation.Error is not null) return validation.Error;

            var file = validation.File!;
            var inferredKind = validation.Kind!;
            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadWithMetadataAsync(stream, file.FileName, file.ContentType, "managed-resources", HttpContext.RequestAborted);
            var (resource, updatedDocuments, errors) = await _resources.ReplaceUploadAsync(
                id,
                upload.Url,
                upload.StorageKey,
                inferredKind,
                file.FileName,
                file.ContentType,
                file.Length,
                ActorId);

            if (errors.Count > 0)
            {
                if (errors.Contains("Resource not found.")) return NotFound(ApiResult.NotFound("Resource not found."));
                return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));
            }

            var usage = await _resources.GetUsageAsync(resource!.Id);
            var message = updatedDocuments > 0
                ? $"Resource file replaced. {updatedDocuments} current record(s) updated."
                : "Resource file replaced.";
            return Ok(ApiResult.Ok(MapResource(resource!, usage.UsageCount), message));
        }

        public sealed class ManagedResourceUploadRequest
        {
            public IFormFile? File { get; set; }
            public string? Kind { get; set; }
        }

        private async Task<(IFormFile? File, string? Kind, IActionResult? Error)> ValidateUploadAsync(
            ManagedResourceUploadRequest request,
            string? requiredKind = null)
        {
            if (!_settings.IsConfigured)
                return (null, null, UnprocessableEntity(ApiResult.BadRequest("R2 storage is not configured.")));

            var file = request.File;
            if (file is null || file.Length == 0)
                return (null, null, BadRequest(ApiResult.BadRequest("No file was uploaded.")));

            var inferredKind = ManagedResourceService.InferKindFromUpload(file.FileName, file.ContentType);
            var requestedKind = _resources.NormalizeKind(requiredKind ?? request.Kind, allowEmpty: string.IsNullOrWhiteSpace(requiredKind));
            if (!string.IsNullOrWhiteSpace(requestedKind) &&
                !string.Equals(requestedKind, inferredKind, StringComparison.OrdinalIgnoreCase))
            {
                var message = string.IsNullOrWhiteSpace(requiredKind)
                    ? $"Uploaded file does not match the selected {requestedKind} resource type."
                    : $"Replacement file must be a {requestedKind} resource.";
                return (null, null, UnprocessableEntity(ApiResult.BadRequest(message)));
            }

            var resourceSettings = await _siteSettings.GetResourceLibrarySettingsAsync();
            var maxBytes = MaxBytesForKind(resourceSettings, inferredKind);
            if (file.Length > maxBytes)
                return (null, null, UnprocessableEntity(ApiResult.BadRequest($"{DisplayKind(inferredKind)} resources must be {maxBytes / 1024 / 1024}MB or smaller.")));

            if (file.Length > _settings.MaxUploadBytes)
                return (null, null, UnprocessableEntity(ApiResult.BadRequest($"Storage is configured for uploads up to {_settings.MaxUploadBytes / 1024 / 1024}MB. Ask an Admin to raise the storage cap.")));

            if (!UploadSecurityPolicy.IsAllowedManagedResourceUpload(file.FileName, file.ContentType, inferredKind, resourceSettings))
                return (null, null, UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.UnsupportedManagedResourceUploadMessage)));

            await using var validationStream = file.OpenReadStream();
            if (!await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(validationStream, file.FileName, file.ContentType, inferredKind, HttpContext.RequestAborted))
                return (null, null, UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.InvalidSignatureMessage)));

            return (file, inferredKind, null);
        }

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst("sub")?.Value ??
            User.Identity?.Name ??
            "unknown";

        private AdminRole ActorRole =>
            Enum.TryParse<AdminRole>(User.FindFirst(ClaimTypes.Role)?.Value, true, out var role)
                ? role
                : AdminRole.Viewer;

        private bool IsContentManager =>
            ActorRole is AdminRole.AdminAdmin or AdminRole.Manager;

        private bool IsWriter =>
            ActorRole == AdminRole.Writer;

        private bool IsResourceLibraryReader =>
            IsContentManager || IsWriter;

        private static long MaxBytesForKind(ResourceLibrarySettings settings, string kind) =>
            kind switch
            {
                "image" => settings.MaxImageBytes,
                "video" => settings.MaxVideoBytes,
                _ => settings.MaxFileBytes
            };

        private static string DisplayKind(string kind) =>
            kind switch
            {
                "image" => "Image",
                "video" => "Video",
                _ => "File"
            };

        private static ManagedResourceResponseDto MapResource(ManagedResource resource, int usageCount = 0) => new()
        {
            Id = resource.Id,
            Kind = resource.Kind,
            Name = resource.Name,
            Description = resource.Description,
            Url = resource.Url,
            StorageKey = resource.StorageKey,
            ThumbnailUrl = resource.ThumbnailUrl,
            FileName = resource.FileName,
            ContentType = resource.ContentType,
            SizeBytes = resource.SizeBytes,
            Source = resource.Source,
            Tags = resource.Tags,
            Active = resource.Active,
            UsageCount = usageCount,
            IsInUse = usageCount > 0,
            CreatedById = resource.CreatedById,
            UpdatedById = resource.UpdatedById,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt
        };
    }
}
