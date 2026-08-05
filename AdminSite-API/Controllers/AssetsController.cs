using Contracts.Admin;
using Contracts.Auth;
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
        private readonly StoredAssetService _storedAssets;
        private readonly AssetStorageKeyPolicy _keyPolicy;

        public AssetsController(
            R2StorageService storage,
            IOptions<R2StorageSettings> settings,
            SettingsService siteSettings,
            StoredAssetService storedAssets,
            AssetStorageKeyPolicy keyPolicy)
        {
            _storage = storage;
            _settings = settings.Value;
            _siteSettings = siteSettings;
            _storedAssets = storedAssets;
            _keyPolicy = keyPolicy;
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
            if (!CanUploadToFolder(folder))
                return Forbid();

            var inferredKind = ManagedResourceService.InferKindFromUpload(file.FileName, file.ContentType);
            if (string.Equals(inferredKind, "image", StringComparison.OrdinalIgnoreCase))
            {
                return UnprocessableEntity(ApiResult.BadRequest(
                    "Image uploads must use the managed Resource Library upload flow."));
            }
            var allowsPageBuilderVideo =
                (string.Equals(folder, "section-backgrounds", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(folder, "video-blocks", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(inferredKind, "video", StringComparison.OrdinalIgnoreCase);
            if (allowsPageBuilderVideo)
            {
                var resourceSettings = await _siteSettings.GetResourceLibrarySettingsAsync();
                if (file.Length > resourceSettings.MaxVideoBytes)
                    return UnprocessableEntity(ApiResult.BadRequest($"Video must be {resourceSettings.MaxVideoBytes / 1024 / 1024}MB or smaller."));
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
                var signatureOk = allowsPageBuilderVideo
                    ? await UploadSecurityPolicy.HasAllowedManagedResourceSignatureAsync(validationStream, file.FileName, file.ContentType, "video", HttpContext.RequestAborted)
                    : await UploadSecurityPolicy.HasAllowedSignatureAsync(validationStream, file.FileName, file.ContentType, HttpContext.RequestAborted);
                if (!signatureOk)
                {
                    return UnprocessableEntity(ApiResult.BadRequest(UploadSecurityPolicy.InvalidSignatureMessage));
                }
            }

            await using var stream = file.OpenReadStream();
            var upload = await _storage.UploadWithMetadataAsync(stream, file.FileName, file.ContentType, folder, HttpContext.RequestAborted);
            var owner = _keyPolicy.MapLegacyFolder(folder);
            await _storedAssets.RecordReadyAsync(
                upload.AssetId!,
                upload.StorageSchemaVersion,
                owner,
                upload.StorageKey,
                upload.Url,
                file.FileName,
                file.ContentType,
                file.Length,
                ActorId,
                upload.AssetVersion,
                cancellationToken: HttpContext.RequestAborted);
            return Ok(ApiResult.Ok(new AssetUploadResponseDto
            {
                Url = upload.Url,
                StorageKey = upload.StorageKey,
                ContentType = file.ContentType,
                FileName = file.FileName,
                Size = file.Length,
                AssetId = upload.AssetId,
                AssetVersion = upload.AssetVersion,
                StorageSchemaVersion = upload.StorageSchemaVersion
            }, "Asset uploaded."));
        }

        public sealed class AssetUploadRequest
        {
            public IFormFile? File { get; set; }
        }

        private bool CanUploadToFolder(string folder)
        {
            var normalized = folder.Trim().ToLowerInvariant();
            return normalized switch
            {
                "branding" or "footer" =>
                    AdminAuthorization.HasPermission(User, AdminPermissionKeys.ManageSettings),

                "content-hero" or "content-thumbnails" or "content-body" or "content-files" or "managed-resources" =>
                    CanManageContentAssets,

                "sections" or "blocks" or "hero" or "gallery" or "carousel" or "showcase" or "list-items" or
                "highlights" or "section-backgrounds" or "image-blocks" or "video-blocks" or "file-blocks" or "card-blocks" =>
                    AdminAuthorization.HasPermission(User, AdminPermissionKeys.PageBuilder),

                "uploads" =>
                    CanManageContentAssets ||
                    AdminAuthorization.HasPermission(User, AdminPermissionKeys.PageBuilder) ||
                    AdminAuthorization.HasPermission(User, AdminPermissionKeys.ManageSettings),

                _ => false
            };
        }

        private bool CanManageContentAssets =>
            AdminAuthorization.IsAdminAdmin(User) ||
            AdminAuthorization.HasPermission(User, AdminPermissionKeys.CreateEditContent) ||
            AdminAuthorization.HasPermission(User, AdminPermissionKeys.ApproveContent);

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst("sub")?.Value ??
            User.Identity?.Name ??
            "unknown";

    }
}
