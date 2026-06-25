using Contracts.Api;
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
using System.Text.Json;

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

        public ManagedResourcesController(
            ManagedResourceService resources,
            ManagedResourceAlbumService albums,
            R2StorageService storage,
            IOptions<R2StorageSettings> settings,
            SettingsService siteSettings)
        {
            _resources = resources;
            _albums = albums;
            _storage = storage;
            _settings = settings.Value;
            _siteSettings = siteSettings;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? kind = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = true, [FromQuery] string? albumId = null)
        {
            if (!IsResourceLibraryReader) return Forbid();
            if (!IsContentManager) includeInactive = false;

            var resources = await _resources.GetAllAsync(kind, search, includeInactive, albumId);
            var usageCounts = await _resources.GetUsageCountsAsync(resources);
            return Ok(ApiResult.Ok(resources.Select(resource =>
                MapResource(resource, usageCounts.GetValueOrDefault(resource.Id))).ToList()));
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
            await Task.CompletedTask;
            return UnprocessableEntity(ApiResult.BadRequest(
                "Create Resource Library resources by uploading files. Direct URLs belong in sections, blocks, carousel, backgrounds, or content fields."));
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
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(250 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] ManagedResourceUploadRequest request)
        {
            if (!IsResourceUploader) return Forbid();

            var validation = await ValidateUploadAsync(request);
            if (validation.Error is not null) return validation.Error;

            var file = validation.File!;
            var inferredKind = validation.Kind!;
            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadWithMetadataAsync(stream, file.FileName, file.ContentType, "managed-resources", HttpContext.RequestAborted);
            var dto = _resources.BuildUploadCreateDto(upload.Url, upload.StorageKey, inferredKind, file.FileName, file.ContentType, file.Length, request.AlbumId);
            var (resource, errors) = await _resources.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ManagedResourceResponseDto>(errors));

            return Ok(ApiResult.Created(MapResource(resource!, 0), "Resource uploaded."));
        }

        [HttpPost("upload-batch")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
        public async Task<IActionResult> UploadBatch([FromForm] ManagedResourceBatchUploadRequest request)
        {
            if (!IsResourceUploader) return Forbid();

            if (request.Files.Count == 0)
                return BadRequest(ApiResult.BadRequest("No files were uploaded."));

            var resourceNames = ParseUploadNames(request.ResourceNamesJson);
            var results = new List<ManagedResourceUploadResultDto>();

            for (var i = 0; i < request.Files.Count; i++)
            {
                var file = request.Files[i];
                var validation = await ValidateUploadFileAsync(file, request.Kind);
                if (!string.IsNullOrWhiteSpace(validation.ErrorMessage))
                {
                    results.Add(FailedUploadResult(i, file.FileName, validation.ErrorMessage));
                    continue;
                }

                try
                {
                    var validatedFile = validation.File!;
                    var inferredKind = validation.Kind!;
                    await using var stream = validatedFile.OpenReadStream();
                    var upload = await _storage.UploadWithMetadataAsync(
                        stream,
                        validatedFile.FileName,
                        validatedFile.ContentType,
                        "managed-resources",
                        HttpContext.RequestAborted);

                    var dto = _resources.BuildUploadCreateDto(
                        upload.Url,
                        upload.StorageKey,
                        inferredKind,
                        validatedFile.FileName,
                        validatedFile.ContentType,
                        validatedFile.Length,
                        request.AlbumId);

                    var uploadName = CleanUploadName(resourceNames.ElementAtOrDefault(i), validatedFile.FileName);
                    dto.Name = LocalizedUploadValue(uploadName);
                    dto.Description = LocalizedUploadValue(string.Empty);

                    var (resource, errors) = await _resources.CreateAsync(dto, ActorId);
                    if (errors.Count > 0)
                    {
                        results.Add(FailedUploadResult(i, validatedFile.FileName, string.Join(" ", errors)));
                        continue;
                    }

                    results.Add(new ManagedResourceUploadResultDto
                    {
                        Index = i,
                        FileName = validatedFile.FileName,
                        Success = true,
                        Resource = MapResource(resource!, 0)
                    });
                }
                catch (Exception ex)
                {
                    results.Add(FailedUploadResult(i, file.FileName, $"Storage upload failed: {ex.Message}"));
                }
            }

            var response = new ManagedResourceUploadBatchResponseDto
            {
                Results = results,
                SuccessCount = results.Count(r => r.Success),
                FailedCount = results.Count(r => !r.Success)
            };

            var message = response.SuccessCount > 0
                ? $"Uploaded {response.SuccessCount} resource(s)."
                : "No resources were uploaded.";
            return Ok(ApiResult.Ok(response, message));
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
            public string? AlbumId { get; set; }
        }

        public sealed class ManagedResourceBatchUploadRequest
        {
            public List<IFormFile> Files { get; set; } = new();
            public string? Kind { get; set; }
            public string? AlbumId { get; set; }
            public string? ResourceNamesJson { get; set; }
        }

        private async Task<(IFormFile? File, string? Kind, IActionResult? Error)> ValidateUploadAsync(
            ManagedResourceUploadRequest request,
            string? requiredKind = null)
        {
            var validation = await ValidateUploadFileAsync(request.File, request.Kind, requiredKind);
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
            string? requiredKind = null)
        {
            if (!_settings.IsConfigured)
                return (null, null, "R2 storage is not configured.", StatusCodes.Status422UnprocessableEntity);

            if (file is null || file.Length == 0)
                return (null, null, "No file was uploaded.", StatusCodes.Status400BadRequest);

            var inferredKind = ManagedResourceService.InferKindFromUpload(file.FileName, file.ContentType);
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

            if (!UploadSecurityPolicy.IsAllowedManagedResourceUpload(file.FileName, file.ContentType, inferredKind, resourceSettings))
                return (null, null, UploadSecurityPolicy.UnsupportedManagedResourceUploadMessage, StatusCodes.Status422UnprocessableEntity);

            await using var validationStream = file.OpenReadStream();
            if (!await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(validationStream, file.FileName, file.ContentType, inferredKind, HttpContext.RequestAborted))
                return (null, null, UploadSecurityPolicy.InvalidSignatureMessage, StatusCodes.Status422UnprocessableEntity);

            return (file, inferredKind, null, StatusCodes.Status200OK);
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

        private static ManagedResourceUploadResultDto FailedUploadResult(int index, string fileName, string error) => new()
        {
            Index = index,
            FileName = fileName,
            Success = false,
            Error = error
        };

        private static List<string> ParseUploadNames(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

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
