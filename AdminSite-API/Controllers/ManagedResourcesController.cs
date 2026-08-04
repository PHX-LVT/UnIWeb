using Contracts.Api;
using Contracts.Admin;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Utils;
using FullProject.Services.AssetService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly ManagedResourceAlbumService _albums;
        private readonly R2StorageService _storage;
        private readonly R2StorageSettings _settings;
        private readonly SettingsService _siteSettings;
        private readonly ILogger<ManagedResourcesController> _logger;
        private readonly StoredAssetService _storedAssets;

        public ManagedResourcesController(
            ManagedResourceService resources,
            ManagedResourceAlbumService albums,
            R2StorageService storage,
            IOptions<R2StorageSettings> settings,
            SettingsService siteSettings,
            StoredAssetService storedAssets,
            ILogger<ManagedResourcesController> logger)
        {
            _resources = resources;
            _albums = albums;
            _storage = storage;
            _settings = settings.Value;
            _siteSettings = siteSettings;
            _storedAssets = storedAssets;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? kind = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = true, [FromQuery] string? albumId = null, [FromQuery] string? purpose = null, [FromQuery] string? usage = null)
        {
            if (!IsResourceLibraryReader) return Forbid();
            if (!IsContentManager) includeInactive = false;

            var resources = await _resources.GetAllAsync(kind, search, includeInactive, albumId, purpose);
            var usageCounts = await _resources.GetUsageCountsAsync(resources);
            var mapped = resources.Select(resource =>
                MapResource(resource, usageCounts.GetValueOrDefault(resource.Id)));
            mapped = usage?.Trim().ToLowerInvariant() switch
            {
                "in-use" => mapped.Where(resource => resource.UsageCount > 0),
                "unused" => mapped.Where(resource => resource.UsageCount == 0),
                _ => mapped
            };
            return Ok(ApiResult.Ok(mapped.ToList()));
        }

        [HttpGet("albums")]
        public async Task<IActionResult> GetAlbums([FromQuery] string? scope = null)
        {
            if (!IsResourceLibraryReader) return Forbid();
            if (!string.IsNullOrWhiteSpace(scope) && ManagedResourceAlbumService.NormalizeScope(scope) is null)
                return UnprocessableEntity(ApiResult.BadRequest("Album type must be media or file."));

            var albums = await _albums.GetAllAsync(scope);
            var counts = await _albums.GetResourceCountsAsync(albums);
            return Ok(ApiResult.Ok(albums.Select(album =>
                MapAlbum(album, counts.GetValueOrDefault(album.Id))).ToList()));
        }

        [HttpPost("albums")]
        public async Task<IActionResult> CreateAlbum([FromBody] ResourceAlbumCreateDto dto)
        {
            if (!IsContentManager) return Forbid();

            var (album, errors) = await _albums.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ResourceAlbumResponseDto>(errors));

            return Ok(ApiResult.Created(MapAlbum(album!, 0), "Album created."));
        }

        [HttpPut("albums/{id}")]
        public async Task<IActionResult> UpdateAlbum(string id, [FromBody] ResourceAlbumUpdateDto dto)
        {
            if (!IsContentManager) return Forbid();

            var (album, errors) = await _albums.UpdateAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Album not found.")) return NotFound(ApiResult.NotFound("Album not found."));
                return UnprocessableEntity(ApiResult.Unprocessable<ResourceAlbumResponseDto>(errors));
            }

            var count = await _albums.GetResourceCountAsync(album!.Id);
            return Ok(ApiResult.Ok(MapAlbum(album!, count), "Album updated."));
        }

        [HttpDelete("albums/{id}")]
        public async Task<IActionResult> DeleteAlbum(string id)
        {
            if (!IsContentManager) return Forbid();

            var (deleted, count, errors) = await _albums.DeleteAsync(id);
            if (errors.Count > 0)
            {
                if (errors.Contains("Album not found.")) return NotFound(ApiResult.NotFound("Album not found."));
                return Conflict(ApiResult.BadRequest(errors[0], errors));
            }

            return Ok(ApiResult.Ok(new { deleted, resourceCount = count }, "Album deleted."));
        }

        [HttpPost("albums/{id}/resources")]
        public async Task<IActionResult> AssignResourcesToAlbum(string id, [FromBody] ResourceAlbumAssignResourcesDto dto)
        {
            if (!IsResourceUploader) return Forbid();

            var requestedCount = dto.ResourceIds
                .Where(resourceId => !string.IsNullOrWhiteSpace(resourceId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var (updatedCount, errors) = await _resources.AssignToAlbumAsync(id, dto.ResourceIds, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Album not found.")) return NotFound(ApiResult.NotFound("Album not found."));
                return UnprocessableEntity(ApiResult.Unprocessable<ResourceAlbumAssignResourcesResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(new ResourceAlbumAssignResourcesResponseDto
            {
                AlbumId = id,
                RequestedCount = requestedCount,
                UpdatedCount = updatedCount
            }, $"Added {updatedCount} resource(s) to album."));
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

        [HttpPost("bulk-move")]
        public async Task<IActionResult> BulkMove([FromBody] ResourceBulkMoveRequest request)
        {
            if (!IsResourceUploader) return Forbid();
            var ids = request.ResourceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var (updatedCount, errors) = await _resources.MoveToAlbumAsync(request.AlbumId, ids, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Album not found.")) return NotFound(ApiResult.NotFound("Album not found."));
                return UnprocessableEntity(ApiResult.Unprocessable<ResourceBulkMoveResult>(errors));
            }
            return Ok(ApiResult.Ok(new ResourceBulkMoveResult
            {
                AlbumId = string.IsNullOrWhiteSpace(request.AlbumId) ? null : request.AlbumId.Trim(),
                RequestedCount = ids.Count,
                UpdatedCount = updatedCount
            }, $"Moved {updatedCount} resource(s)."));
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDelete([FromBody] ResourceBulkDeleteRequest request)
        {
            if (!IsContentManager) return Forbid();
            var ids = request.ResourceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(101)
                .ToList();
            if (ids.Count == 0)
                return UnprocessableEntity(ApiResult.BadRequest("Choose at least one resource."));
            if (ids.Count > 100)
                return UnprocessableEntity(ApiResult.BadRequest("Delete no more than 100 resources at a time."));

            var result = new ResourceBulkDeleteResult { RequestedCount = ids.Count };
            foreach (var id in ids)
            {
                var (deleted, usage, errors) = await _resources.DeleteAsync(id);
                var item = new ResourceBulkDeleteItemResult
                {
                    ResourceId = id,
                    Deleted = deleted,
                    UsageCount = usage?.UsageCount ?? 0,
                    Error = errors.FirstOrDefault()
                };
                result.Items.Add(item);
                if (deleted) result.DeletedCount++;
                else if (item.UsageCount > 0) result.BlockedCount++;
                else result.FailedCount++;
            }
            return Ok(ApiResult.Ok(result, $"Deleted {result.DeletedCount} resource(s)."));
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
            if (!IsResourceUploader) return Forbid();

            var kind = _resources.NormalizeKind(dto.Kind);
            var source = (dto.Source ?? string.Empty).Trim().ToLowerInvariant();
            if (kind != "video" || source != "external-url")
            {
                return UnprocessableEntity(ApiResult.BadRequest(
                    "Create Resource Library images and files by uploading files. Video resources can also use a YouTube link."));
            }

            var (resource, errors) = await _resources.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));

            return Ok(ApiResult.Created(MapResource(resource!, 0), "YouTube video resource added."));
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

                return Conflict(new ApiResponse<ManagedResourceDeleteResultDto>
                {
                    Success = false,
                    StatusCode = StatusCodes.Status409Conflict,
                    Message = errors[0],
                    Errors = errors,
                    Data = new ManagedResourceDeleteResultDto
                    {
                        Deleted = false,
                        UsageCount = usage?.UsageCount ?? 0,
                        Usage = usage
                    }
                });
            }

            var result = new ManagedResourceDeleteResultDto
            {
                Deleted = deleted,
                UsageCount = usage?.UsageCount ?? 0,
                Usage = usage
            };
            return Ok(ApiResult.Ok(result, "Resource deleted."));
        }

        [HttpPost("upload")]
        [EnableRateLimiting("admin-resource-upload-initiate")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(3L * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] ManagedResourceUploadRequest request)
        {
            if (!IsResourceUploader) return Forbid();
            if (!string.Equals(request.Purpose, "custom-icon", StringComparison.OrdinalIgnoreCase))
                return UnprocessableEntity(ApiResult.BadRequest("Resource Library files must use the direct upload session API."));

            var validation = await ValidateUploadAsync(request);
            if (validation.Error is not null) return validation.Error;

            var file = validation.File!;
            var inferredKind = validation.Kind!;
            R2UploadResult? upload = null;
            try
            {
                await using var input = file.OpenReadStream();
                Stream uploadStream = input;
                var uploadName = file.FileName;
                var uploadType = file.ContentType;
                long uploadLength = file.Length;
                MemoryStream? processedStream = null;
                var processed = await CustomIconUploadPolicy.ReadAndValidateAsync(input, file.FileName, file.ContentType, file.Length, HttpContext.RequestAborted);
                if (!processed.Success)
                    return UnprocessableEntity(ApiResult.BadRequest(processed.Error ?? "Custom Icon upload is invalid."));
                processedStream = new MemoryStream(processed.Bytes!, writable: false);
                uploadStream = processedStream;
                uploadName = processed.FileName;
                uploadType = processed.ContentType;
                uploadLength = processed.Bytes!.LongLength;

                await using (processedStream)
                {
                    upload = await _storage.UploadWithMetadataAsync(uploadStream, uploadName, uploadType, "custom-icons", HttpContext.RequestAborted);
                }
                var dto = _resources.BuildUploadCreateDto(upload.Url, upload.StorageKey, inferredKind, uploadName, uploadType, uploadLength, request.AlbumId, request.Purpose);
                if (!string.IsNullOrWhiteSpace(request.ResourceName))
                    dto.Name = LocalizedUploadValue(CleanUploadName(request.ResourceName, uploadName));
                dto.OriginalSourceUrl = request.OriginalSourceUrl;
                dto.LicenseName = request.LicenseName;
                dto.Attribution = request.Attribution;
                var reservedResourceId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                var (resource, errors) = await _resources.CreateUploadedAsync(
                    dto,
                    ActorId,
                    reservedResourceId,
                    upload.AssetId!,
                    upload.AssetVersion,
                    upload.StorageSchemaVersion);
                if (errors.Count > 0)
                {
                    await TryDeleteUploadedAssetAsync(upload, "resource-create-validation-failed");
                    return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));
                }

                try
                {
                    await _storedAssets.RecordReadyAsync(
                        upload.AssetId!,
                        upload.StorageSchemaVersion,
                        new AssetStorageOwner("icons", "custom-icon", "catalogue", "icon"),
                        upload.StorageKey,
                        upload.Url,
                        uploadName,
                        uploadType,
                        uploadLength,
                        ActorId,
                        upload.AssetVersion,
                        resource!.Id,
                        cancellationToken: HttpContext.RequestAborted);
                }
                catch (Exception registryException)
                {
                    _logger.LogError(registryException, "Custom Icon {ResourceId} uploaded but its storage registry record could not be saved.", resource!.Id);
                }

                return Ok(ApiResult.Created(MapResource(resource!, 0), "Resource uploaded."));
            }
            catch
            {
                await TryDeleteUploadedAssetAsync(upload, "resource-create-exception");
                throw;
            }
        }

        public sealed class ManagedResourceUploadRequest
        {
            public IFormFile? File { get; set; }
            public string? Kind { get; set; }
            public string? AlbumId { get; set; }
            public string? ResourceName { get; set; }
            public string? Purpose { get; set; }
            public string? OriginalSourceUrl { get; set; }
            public string? LicenseName { get; set; }
            public string? Attribution { get; set; }
        }

        private async Task<(IFormFile? File, string? Kind, IActionResult? Error)> ValidateUploadAsync(
            ManagedResourceUploadRequest request,
            string? requiredKind = null,
            string? requiredPurpose = null)
        {
            var validation = await ValidateUploadFileAsync(
                request.File,
                request.Kind,
                requiredKind,
                requiredPurpose ?? request.Purpose);
            if (string.IsNullOrWhiteSpace(validation.ErrorMessage))
                return (validation.File, validation.Kind, null);

            IActionResult error = validation.StatusCode == StatusCodes.Status400BadRequest
                ? BadRequest(ApiResult.BadRequest(validation.ErrorMessage))
                : UnprocessableEntity(ApiResult.BadRequest(validation.ErrorMessage));
            return (null, null, error);
        }

        private async Task<(IFormFile? File, string? Kind, string? ErrorMessage, int StatusCode)> ValidateUploadFileAsync(
            IFormFile? file,
            string? kind,
            string? requiredKind = null,
            string? purpose = null)
        {
            if (!_settings.IsConfigured)
                return (null, null, "R2 storage is not configured.", StatusCodes.Status422UnprocessableEntity);

            if (file is null || file.Length == 0)
                return (null, null, "No file was uploaded.", StatusCodes.Status400BadRequest);

            var inferredKind = ManagedResourceService.InferKindFromUpload(file.FileName, file.ContentType);
            var customIcon = string.Equals(purpose, "custom-icon", StringComparison.OrdinalIgnoreCase);
            if (customIcon)
            {
                inferredKind = "image";
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var maximum = extension == ".svg" ? CustomIconUploadPolicy.MaximumSvgBytes : CustomIconUploadPolicy.MaximumRasterBytes;
                if (extension is not (".png" or ".webp" or ".jpg" or ".jpeg" or ".svg"))
                    return (null, null, "Custom Icons must be PNG, WebP, JPEG, or SVG files.", StatusCodes.Status422UnprocessableEntity);
                if (file.Length > maximum)
                    return (null, null, $"Custom Icons must be {maximum / 1024}KB or smaller.", StatusCodes.Status422UnprocessableEntity);
            }
            var requestedKind = _resources.NormalizeKind(requiredKind ?? kind, allowEmpty: string.IsNullOrWhiteSpace(requiredKind));
            if (!string.IsNullOrWhiteSpace(requestedKind) &&
                !string.Equals(requestedKind, inferredKind, StringComparison.OrdinalIgnoreCase))
            {
                var message = string.IsNullOrWhiteSpace(requiredKind)
                    ? $"Uploaded file does not match the selected {requestedKind} resource type."
                    : $"Replacement file must be a {requestedKind} resource.";
                return (null, null, message, StatusCodes.Status422UnprocessableEntity);
            }

            var resourceSettings = await _siteSettings.GetResourceLibrarySettingsAsync();
            var maxBytes = MaxBytesForKind(resourceSettings, inferredKind);
            if (file.Length > maxBytes)
                return (null, null, $"{DisplayKind(inferredKind)} resources must be {maxBytes / 1024 / 1024}MB or smaller.", StatusCodes.Status422UnprocessableEntity);

            if (file.Length > _settings.MaxUploadBytes)
                return (null, null, $"Storage is configured for uploads up to {_settings.MaxUploadBytes / 1024 / 1024}MB. Ask an Admin to raise the storage cap.", StatusCodes.Status422UnprocessableEntity);

            if (!customIcon && !UploadSecurityPolicy.IsAllowedManagedResourceUpload(file.FileName, file.ContentType, inferredKind, resourceSettings))
                return (null, null, UploadSecurityPolicy.UnsupportedManagedResourceUploadMessage, StatusCodes.Status422UnprocessableEntity);

            if (!customIcon)
            {
                await using var validationStream = file.OpenReadStream();
                if (!await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(validationStream, file.FileName, file.ContentType, inferredKind, HttpContext.RequestAborted))
                    return (null, null, UploadSecurityPolicy.InvalidSignatureMessage, StatusCodes.Status422UnprocessableEntity);
            }

            return (file, inferredKind, null, StatusCodes.Status200OK);
        }

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst("sub")?.Value ??
            User.Identity?.Name ??
            "unknown";

        private bool IsContentManager =>
            FullProject.Security.AdminAuthorization.IsAdminAdmin(User) ||
            FullProject.Security.AdminAuthorization.HasPermission(User, AdminPermissionKeys.ApproveContent);

        private bool IsWriter =>
            FullProject.Security.AdminAuthorization.HasPermission(User, AdminPermissionKeys.CreateEditContent) &&
            !IsContentManager;

        private bool IsResourceLibraryReader =>
            IsContentManager || IsWriter;

        private bool IsResourceUploader =>
            IsResourceLibraryReader;

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

        private static string CleanUploadName(string? requestedName, string fileName)
        {
            var clean = (requestedName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(clean)) return clean;

            var fallback = Path.GetFileNameWithoutExtension(fileName);
            return string.IsNullOrWhiteSpace(fallback) ? fileName : fallback;
        }

        private static Dictionary<string, string> LocalizedUploadValue(string value) =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = value,
                ["vi"] = value,
                ["cn"] = value
            };

        private async Task TryDeleteUploadedAssetAsync(R2UploadResult? upload, string reason)
        {
            if (upload is null || string.IsNullOrWhiteSpace(upload.Url)) return;

            try
            {
                var deleted = await _storage.DeleteAsync(upload.Url);
                if (!deleted)
                {
                    _logger.LogWarning(
                        "Uploaded resource asset rollback skipped or failed. Reason: {Reason}. Url: {Url}. StorageKey: {StorageKey}",
                        reason,
                        upload.Url,
                        upload.StorageKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Uploaded resource asset rollback failed. Reason: {Reason}. Url: {Url}. StorageKey: {StorageKey}",
                    reason,
                    upload.Url,
                    upload.StorageKey);
            }
        }

        private static ManagedResourceResponseDto MapResource(ManagedResource resource, int usageCount = 0) => new()
        {
            Id = resource.Id,
            AssetId = resource.AssetId,
            AssetVersion = resource.AssetVersion,
            StorageSchemaVersion = resource.StorageSchemaVersion,
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
            Purpose = resource.Purpose,
            OriginalSourceUrl = resource.OriginalSourceUrl,
            LicenseName = resource.LicenseName,
            Attribution = resource.Attribution,
            DeletionState = resource.DeletionState,
            Tags = resource.Tags,
            AlbumId = resource.AlbumId,
            Active = resource.Active,
            UsageCount = usageCount,
            IsInUse = usageCount > 0,
            CreatedById = resource.CreatedById,
            UpdatedById = resource.UpdatedById,
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt
        };

        private static ResourceAlbumResponseDto MapAlbum(ResourceAlbum album, int resourceCount = 0) => new()
        {
            Id = album.Id,
            Scope = album.Scope,
            Name = album.Name,
            ResourceCount = resourceCount,
            CreatedById = album.CreatedById,
            UpdatedById = album.UpdatedById,
            CreatedAt = album.CreatedAt,
            UpdatedAt = album.UpdatedAt
        };
    }
}
