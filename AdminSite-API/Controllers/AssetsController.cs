using Contracts.Admin;
using FullProject.Services;
using FullProject.Settings;
using FullProject.Utils;
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

        public AssetsController(R2StorageService storage, IOptions<R2StorageSettings> settings)
        {
            _storage = storage;
            _settings = settings.Value;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] AssetUploadRequest request, [FromQuery] string folder = "uploads")
        {
            if (!_settings.IsConfigured)
                return UnprocessableEntity(ApiResult.BadRequest("R2 storage is not configured."));

            var file = request.File;
            if (file is null || file.Length == 0)
                return BadRequest(ApiResult.BadRequest("No file was uploaded."));

            if (file.Length > _settings.MaxUploadBytes)
                return UnprocessableEntity(ApiResult.BadRequest($"File must be {_settings.MaxUploadBytes / 1024 / 1024}MB or smaller."));

            if (!IsAllowedContentType(file.ContentType))
                return UnprocessableEntity(ApiResult.BadRequest("Only image and PDF uploads are supported here."));

            await using var stream = file.OpenReadStream();
            var url = await _storage.UploadAsync(stream, file.FileName, file.ContentType, folder, HttpContext.RequestAborted);
            return Ok(ApiResult.Ok(new AssetUploadResponseDto
            {
                Url = url,
                ContentType = file.ContentType,
                FileName = file.FileName,
                Size = file.Length
            }, "Asset uploaded."));
        }

        public sealed class AssetUploadRequest
        {
            public IFormFile? File { get; set; }
        }

        private static bool IsAllowedContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return false;
            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        }
    }
}
