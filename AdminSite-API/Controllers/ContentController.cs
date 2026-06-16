using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/content")]
    [Authorize]
    public class ContentController : ControllerBase
    {
        private readonly ContentService _service;

        public ContentController(ContentService service)
        {
            _service = service;
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetTypes()
        {
            var types = await _service.GetTypesAsync();
            return Ok(ApiResult.Ok(types.Select(MapType).ToList()));
        }

        [HttpPost("types")]
        public async Task<IActionResult> CreateType([FromBody] ContentTypeCreateDto dto)
        {
            var (type, errors) = await _service.CreateTypeAsync(dto);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ContentTypeResponseDto>(errors));

            return Ok(ApiResult.Created(MapType(type!), "Content type created."));
        }

        [HttpPut("types/{id}")]
        public async Task<IActionResult> UpdateType(string id, [FromBody] ContentTypeUpdateDto dto)
        {
            var type = await _service.UpdateTypeAsync(id, dto);
            if (type is null) return NotFound(ApiResult.NotFound("Content type not found."));

            return Ok(ApiResult.Ok(MapType(type), "Content type updated."));
        }

        [HttpDelete("types/{id}")]
        public async Task<IActionResult> DeleteType(string id)
        {
            var ok = await _service.DeleteTypeAsync(id);
            if (!ok) return BadRequest(ApiResult.BadRequest("Content type was not found or is already in use."));

            return Ok(ApiResult.Ok("Content type deleted."));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? typeKey = null, [FromQuery] ContentStatus? status = null)
        {
            var items = await _service.GetAllAsync(typeKey, status);
            return Ok(ApiResult.Ok(items.Select(MapItem).ToList()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item is null) return NotFound(ApiResult.NotFound("Content item not found."));

            return Ok(ApiResult.Ok(MapItem(item)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContentCreateDto dto)
        {
            var (item, errors) = await _service.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));

            return Ok(ApiResult.Created(MapItem(item!), "Content item created."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ContentUpdateDto dto)
        {
            var (item, errors) = await _service.UpdateAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(MapItem(item!), "Content item updated."));
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> SetStatus(string id, [FromBody] ContentStatusUpdateDto dto)
        {
            var (item, errors) = await _service.SetStatusAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(MapItem(item!), "Content status updated."));
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(string id)
        {
            var (item, errors) = await _service.PublishAsync(id, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(MapItem(item!), "Content published."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var ok = await _service.DeleteAsync(id, ActorId);
            if (!ok) return NotFound(ApiResult.NotFound("Content item not found."));

            return Ok(ApiResult.Ok("Content item deleted."));
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(string id)
        {
            var (item, errors) = await _service.RestoreAsync(id, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(MapItem(item!), "Content item restored."));
        }

        [HttpPost("permanent-delete")]
        public async Task<IActionResult> PermanentDelete([FromBody] ContentPermanentDeleteDto dto)
        {
            var count = await _service.PermanentDeleteAsync(dto.Ids);
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} content item(s) permanently deleted."));
        }

        [HttpGet("{stableId}/logs")]
        public async Task<IActionResult> GetLogs(string stableId)
        {
            var logs = await _service.GetLogsAsync(stableId);
            return Ok(ApiResult.Ok(logs.Select(MapLog).ToList()));
        }

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst("sub")?.Value ??
            User.Identity?.Name ??
            "unknown";

        private static ContentTypeResponseDto MapType(ContentType type) => new()
        {
            Id = type.Id,
            Key = type.Key,
            Name = type.Name,
            Description = type.Description,
            RequiresBody = type.RequiresBody,
            RequiresHeroImage = type.RequiresHeroImage,
            RequiresFile = type.RequiresFile,
            RequiresVideoUrl = type.RequiresVideoUrl,
            AllowsAttachments = type.AllowsAttachments,
            ClickBehavior = type.ClickBehavior,
            Visible = type.Visible,
            Order = type.Order,
            CreatedAt = type.CreatedAt,
            UpdatedAt = type.UpdatedAt
        };

        private static ContentResponseDto MapItem(ContentItem item) => new()
        {
            Id = item.Id,
            StableId = item.StableId,
            ContentTypeKey = item.ContentTypeKey,
            Slug = item.Slug,
            Title = item.Title,
            Summary = item.Summary,
            BodyHtml = item.BodyHtml,
            HeroImageUrl = item.HeroImageUrl,
            HeroImageAlt = item.HeroImageAlt,
            ThumbnailUrl = item.ThumbnailUrl,
            VideoUrl = item.VideoUrl,
            ExternalUrl = item.ExternalUrl,
            TemplateKey = item.TemplateKey,
            Tags = item.Tags,
            Attachments = item.Attachments.Select(MapAttachment).ToList(),
            Status = item.Status,
            Visible = item.Visible,
            AuthorId = item.AuthorId,
            UpdatedById = item.UpdatedById,
            PublishedById = item.PublishedById,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            SubmittedAt = item.SubmittedAt,
            PublishedAt = item.PublishedAt
        };

        private static ContentAttachmentDto MapAttachment(ContentAttachment attachment) => new()
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            Url = attachment.Url,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes
        };

        private static ContentAuditLogResponseDto MapLog(ContentAuditLog log) => new()
        {
            Id = log.Id,
            ContentStableId = log.ContentStableId,
            Action = log.Action,
            ActorId = log.ActorId,
            Message = log.Message,
            CreatedAt = log.CreatedAt
        };
    }
}
