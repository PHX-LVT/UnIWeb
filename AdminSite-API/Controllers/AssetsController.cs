using Contracts.Admin;
using FullProject.Security;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Utils;
using FullProject.Services.AssetService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/assets")]
    [Authorize]
    public class AssetsController : ControllerBase
    {
        private readonly R2StorageService _storage;
        private readonly R2StorageSettings _settings;
        private readonly SettingsService _siteSettings;

        public AssetsController(
            R2StorageService storage,
            IOptions<R2StorageSettings> settings,
            SettingsService siteSettings)
        {
            _storage = storage;
            _settings = settings.Value;
            _siteSettings = siteSettings;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(250 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] AssetUploadRequest request, [FromQuery] string folder = "uploads")
        {
            if (!_settings.IsConfigured)
                return UnprocessableEntity(ApiResult.BadRequest("R2 storage is not configured."));

            var file = request.File;
            if (file is null || file.Length == 0)
                return BadRequest(ApiResult.BadRequest("No file was uploaded."));

            if (!UploadSecurityPolicy.IsAllowedFolder(folder))
                return UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.UnsupportedFolderMessage));

            var inferredKind = ManagedResourceService.InferKindFromUpload(file.FileName, file.ContentType);
            var allowsSectionBackgroundVideo = string.Equals(folder, "section-backgrounds", StringComparison.OrdinalIgnoreCase) &&
                                                string.Equals(inferredKind, "video", StringComparison.OrdinalIgnoreCase);
            if (allowsSectionBackgroundVideo)
            {
                var resourceSettings = await _siteSettings.GetResourceLibrarySettingsAsync();
                if (file.Length > resourceSettings.MaxVideoBytes)
                    return UnprocessableEntity(ApiResult.BadRequest($"Video backgrounds must be {resourceSettings.MaxVideoBytes / 1024 / 1024}MB or smaller."));
                if (file.Length > _settings.MaxUploadBytes)
                    return UnprocessableEntity(ApiResult.BadRequest($"Storage is configured for uploads up to {_settings.MaxUploadBytes / 1024 / 1024}MB. Ask an Admin to raise the storage cap."));
                if (!UploadSecurityPolicy.IsAllowedManagedResourceUpload(file.FileName, file.ContentType, "video", resourceSettings))
                    return UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.UnsupportedManagedResourceUploadMessage));
            }
            else
            {
                if (file.Length > _settings.MaxUploadBytes)
                    return UnprocessableEntity(ApiResult.BadRequest($"File must be {_settings.MaxUploadBytes / 1024 / 1024}MB or smaller."));
                if (!UploadSecurityPolicy.IsAllowedUpload(file.FileName, file.ContentType))
                    return UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.UnsupportedUploadMessage));
            }

            await using (var validationStream = file.OpenReadStream())
            {
                var signatureOk = allowsSectionBackgroundVideo
                    ? await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(validationStream, file.FileName, file.ContentType, "video", HttpContext.RequestAborted)
                    : await UploadSecurityPolicy.HasAllowedSignatureAsync(validationStream, file.FileName, file.ContentType, HttpContext.RequestAborted);
                if (!signatureOk)
                {
                    return UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.InvalidSignatureMessage));
                }
            }

            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadWithMetadataAsync(stream, file.FileName, file.ContentType, folder, HttpContext.RequestAborted);
            return Ok(ApiResult.Ok(new AssetUploadResponseDto
            {
                Url = upload.Url,
                StorageKey = upload.StorageKey,
                ContentType = file.ContentType,
                FileName = file.FileName,
                Size = file.Length
            }, "Asset uploaded."));
        }

        public sealed class AssetUploadRequest
        {
            public IFormFile? File { get; set; }
        }

    }
}
