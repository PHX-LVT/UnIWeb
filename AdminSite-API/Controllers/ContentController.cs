using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/content")]
    [Authorize]
    public class ContentController : ControllerBase
    {
        private readonly ContentService _service;
        private readonly ContentMappingService _mapping;
        private readonly ContentWorkflowPolicy _workflowPolicy;

        public ContentController(
            ContentService service,
            ContentMappingService mapping,
            ContentWorkflowPolicy workflowPolicy)
        {
            _service = service;
            _mapping = mapping;
            _workflowPolicy = workflowPolicy;
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetTypes()
        {
            if (!_workflowPolicy.CanViewModule(User)) return Forbid();
            var types = await _service.GetTypesAsync();
            return Ok(ApiResult.Ok(types.Select(_mapping.MapType).ToList()));
        }

        [HttpPost("types")]
        public async Task<IActionResult> CreateType([FromBody] ContentTypeCreateDto dto)
        {
            if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();

            var (type, errors) = await _service.CreateTypeAsync(dto);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ContentTypeResponseDto>(errors));

            return Ok(ApiResult.Created(_mapping.MapType(type!), "Content type created."));
        }

        [HttpPut("types/{id}")]
        public async Task<IActionResult> UpdateType(string id, [FromBody] ContentTypeUpdateDto dto)
        {
            if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();

            var type = await _service.UpdateTypeAsync(id, dto);
            if (type is null) return NotFound(ApiResult.NotFound("Content type not found."));

            return Ok(ApiResult.Ok(_mapping.MapType(type), "Content type updated."));
        }

        [HttpDelete("types/{id}")]
        public async Task<IActionResult> DeleteType(string id)
        {
            if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();

            var ok = await _service.DeleteTypeAsync(id);
            if (!ok) return BadRequest(ApiResult.BadRequest("Content type was not found or is already in use."));

            return Ok(ApiResult.Ok("Content type deleted."));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? typeKey = null, [FromQuery] ContentStatus? status = null, [FromQuery] string? scope = null)
        {
            if (!_workflowPolicy.CanViewModule(User)) return Forbid();
            var items = await _service.GetAllAsync(typeKey, status);
            items = _workflowPolicy.ApplyVisibility(User, ActorId, items, scope).ToList();
            return Ok(ApiResult.Ok(items.Select(_mapping.MapItem).ToList()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanRead(User, ActorId, item)) return Forbid();

            return Ok(ApiResult.Ok(_mapping.MapItem(item)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContentCreateDto dto)
        {
            if (!_workflowPolicy.CanCreate(User)) return Forbid();
            if (!AdminAuthorization.IsAdminAdmin(User)) dto.Visible = true;

            var (item, errors) = await _service.CreateAsync(dto, ActorId);
            if (errors.Count > 0) return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));

            return Ok(ApiResult.Created(_mapping.MapItem(item!), "Content item created."));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ContentUpdateDto dto)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanEdit(User, ActorId, existing)) return Forbid();
            if (!AdminAuthorization.IsAdminAdmin(User)) dto.Visible = null;

            var (item, errors) = await _service.UpdateAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(_mapping.MapItem(item!), "Content item updated."));
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> SetStatus(string id, [FromBody] ContentStatusUpdateDto dto)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            var allowed = dto.Status switch
            {
                ContentWorkflowTransition.Draft when existing.Status == ContentStatus.Submitted =>
                    _workflowPolicy.CanWithdraw(User, ActorId, existing),
                ContentWorkflowTransition.Draft =>
                    _workflowPolicy.CanForceReturnToDraft(User, existing),
                ContentWorkflowTransition.Submitted when existing.Status == ContentStatus.Published =>
                    _workflowPolicy.CanReturnPublishedToPending(User, existing),
                ContentWorkflowTransition.Submitted =>
                    _workflowPolicy.CanSubmit(User, ActorId, existing),
                ContentWorkflowTransition.Rejected =>
                    _workflowPolicy.CanReject(User, existing),
                _ => false
            };
            if (!allowed) return Forbid();

            var (item, errors) = await _service.SetStatusAsync(id, dto, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(_mapping.MapItem(item!), "Content status updated."));
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(string id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanPublish(User, existing)) return Forbid();

            var (item, errors) = await _service.PublishAsync(id, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(_mapping.MapItem(item!), "Content published."));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanDelete(User, ActorId, existing)) return Forbid();

            var ok = await _service.DeleteAsync(id, ActorId);
            if (!ok) return NotFound(ApiResult.NotFound("Content item not found."));

            return Ok(ApiResult.Ok("Content item deleted."));
        }

        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(string id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanRestore(User, existing)) return Forbid();

            var (item, errors) = await _service.RestoreAsync(id, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found."))
                    return NotFound(ApiResult.NotFound("Content item not found."));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(_mapping.MapItem(item!), "Content item restored."));
        }

        [HttpPost("permanent-delete")]
        public async Task<IActionResult> PermanentDelete([FromBody] ContentPermanentDeleteDto dto)
        {
            if (!_workflowPolicy.CanPermanentlyDelete(User)) return Forbid();

            var count = await _service.PermanentDeleteAsync(dto.Ids);
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} content item(s) permanently deleted."));
        }


        [HttpGet("{id}/revisions")]
        public async Task<IActionResult> GetRevisions(string id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanViewHistory(User, ActorId, existing)) return Forbid();

            var revisions = await _service.GetRevisionsAsync(id);
            return Ok(ApiResult.Ok(revisions));
        }

        [HttpPost("{id}/revisions/{revisionId}/restore")]
        public async Task<IActionResult> RestoreRevision(string id, string revisionId)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanEdit(User, ActorId, existing)) return Forbid();

            var (item, errors) = await _service.RestoreRevisionAsync(id, revisionId, ActorId);
            if (errors.Count > 0)
            {
                if (errors.Contains("Content item not found.") || errors.Contains("Content revision not found."))
                    return NotFound(ApiResult.NotFound(errors[0]));

                return UnprocessableEntity(ApiResult.Unprocessable<ContentResponseDto>(errors));
            }

            return Ok(ApiResult.Ok(_mapping.MapItem(item!), "Content revision restored."));
        }
        [HttpGet("{stableId}/logs")]
        public async Task<IActionResult> GetLogs(string stableId)
        {
            var item = (await _service.GetAllAsync())
                .FirstOrDefault(content => string.Equals(content.StableId, stableId, StringComparison.OrdinalIgnoreCase));
            if (item is null) return NotFound(ApiResult.NotFound("Content item not found."));
            if (!_workflowPolicy.CanViewHistory(User, ActorId, item)) return Forbid();

            var logs = await _service.GetLogsAsync(stableId);
            return Ok(ApiResult.Ok(logs.Select(_mapping.MapLog).ToList()));
        }

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst("sub")?.Value ??
            User.Identity?.Name ??
            "unknown";

    }
}
