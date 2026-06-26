using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using FullProject.Security;
using FullProject.Utils;
using FullProject.Services.PublishAndResetService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Contracts.Auth;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/pages")]
    [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
    public class PagesController : ControllerBase
    {
        private readonly PageService _service;
        private readonly PublishService _publishService;
        private readonly ResetService _resetService;

        public PagesController(PageService service, PublishService publishService, ResetService resetService)
        {
            _service = service;
            _publishService = publishService;
            _resetService = resetService;
        }

        // GET api/admin/pages
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var pages = await _service.GetAllAsync();
            return Ok(ApiResult.Ok(pages.Select(MapToDto).ToList()));
        }

        // GET api/admin/pages/:pageId
        [HttpGet("{pageId}")]
        public async Task<IActionResult> GetById(string pageId)
        {
            var page = await _service.GetByIdAsync(pageId);
            if (page is null) return NotFound(ApiResult.NotFound("Page not found."));
            return Ok(ApiResult.Ok(MapToDto(page)));
        }

        // POST api/admin/pages
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PageCreateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var enName = dto.Name.GetValueOrDefault("en", string.Empty);
            if (string.IsNullOrWhiteSpace(enName))
                return BadRequest(ApiResult.BadRequest("English name is required."));

            // Auto-fill missing language names with English value
            foreach (var code in new[] { "vi", "cn" })
                if (!dto.Name.ContainsKey(code) || string.IsNullOrEmpty(dto.Name[code]))
                    dto.Name[code] = enName;

          //  var seoMetaTitle = new Dictionary<string, string>(dto.Name);
            //var seoMetaDesc = new Dictionary<string, string>();

           // if (dto.Seo?.MetaTitle is not null)
            //    seoMetaTitle = AutoFillLanguage(dto.Seo.MetaTitle);
           // if (dto.Seo?.MetaDescription is not null)
            //    seoMetaDesc = AutoFillLanguage(dto.Seo.MetaDescription);

            var page = new Page
            {
                Name = dto.Name,
                Access = dto.Access,
                Visible = dto.Visible,
                Status = PageStatus.Draft,
                Seo = new PageSeo
                {
                    MetaTitle = new Dictionary<string, string>(),
                    MetaDescription = new Dictionary<string, string>()
                }
            };

            var created = await _service.CreateAsync(page, enName);
            return CreatedAtAction(nameof(GetById),
                new { pageId = created.Id },
                ApiResult.Created(MapToDto(created), "Page created."));
        }

        // PUT api/admin/pages/:pageId
        [HttpPut("{pageId}")]
        public async Task<IActionResult> Update(string pageId, [FromBody] PageUpdateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var existing = await _service.GetByIdAsync(pageId);
            if (existing is null) return NotFound(ApiResult.NotFound("Page not found."));

            var updated = await _service.UpdateAsync(pageId, dto, ActorId);
            return Ok(ApiResult.Ok(MapToDto(updated!), "Page updated."));
        }

        // DELETE api/admin/pages/:pageId
        [HttpDelete("{pageId}")]
        public async Task<IActionResult> Delete(string pageId)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.DeleteAsync(pageId);
            if (!ok) return NotFound(ApiResult.NotFound("Page not found."));
            return Ok(ApiResult.Ok("Page deleted."));
        }

        // PUT api/admin/pages/:pageId/visibility
        [HttpPut("{pageId}/visibility")]
        public async Task<IActionResult> SetVisibility(string pageId, [FromBody] VisibilityDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.SetVisibilityAsync(pageId, dto.Visible, ActorId);
            if (!ok) return NotFound(ApiResult.NotFound("Page not found."));
            return Ok(ApiResult.Ok($"Page {(dto.Visible ? "shown" : "hidden")} in navigation."));
        }

        // PUT api/admin/pages/:pageId/access
        [HttpPut("{pageId}/access")]
        public async Task<IActionResult> SetAccess(string pageId, [FromBody] VisibilityDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.SetAccessAsync(pageId, dto.Visible, ActorId);
            if (!ok) return NotFound(ApiResult.NotFound("Page not found."));
            return Ok(ApiResult.Ok($"Page access {(dto.Visible ? "enabled" : "disabled")}."));
        }

        // PUT api/admin/pages/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder([FromBody] ReorderDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.ReorderAsync(dto.OrderedIds);
            if (!ok) return BadRequest(ApiResult.BadRequest("Reorder failed."));
            return Ok(ApiResult.Ok("Pages reordered."));
        }


        [HttpGet("{pageId}/revisions")]
        public async Task<IActionResult> GetRevisions(string pageId)
        {
            if (!CanUsePageBuilder) return Forbid();

            var existing = await _service.GetByIdAsync(pageId);
            if (existing is null) return NotFound(ApiResult.NotFound("Page not found."));

            var revisions = await _service.GetRevisionsAsync(pageId);
            return Ok(ApiResult.Ok(revisions));
        }

        [HttpPost("{pageId}/revisions/{revisionId}/restore")]
        public async Task<IActionResult> RestoreRevision(string pageId, string revisionId)
        {
            if (!CanUsePageBuilder) return Forbid();

            var restored = await _service.RestoreRevisionAsync(pageId, revisionId, ActorId);
            if (restored is null) return NotFound(ApiResult.NotFound("Page revision not found."));

            return Ok(ApiResult.Ok(MapToDto(restored), "Page revision restored."));
        }
        // POST api/admin/pages/:pageId/publish
        [HttpPost("{pageId}/publish")]
        public async Task<IActionResult> Publish(string pageId)
        {
            if (!CanUsePageBuilder) return Forbid();
            var result = await _publishService.PublishPageAsync(pageId);
            if (!result.Success)
                return BadRequest(ApiResult.BadRequest(result.Message ?? "Publish failed."));
            return Ok(ApiResult.Ok(new { result.PublishedAt }, result.Message));
        }
        // POST api/admin/pages/:pageId/reset
        [HttpPost("{pageId}/reset")]
        public async Task<IActionResult> Reset(string pageId)
        {
            if (!CanUsePageBuilder) return Forbid();
            var result = await _resetService.ResetPageAsync(pageId);
            if (!result.Success)
                return BadRequest(ApiResult.BadRequest(result.Message ?? "Reset failed."));
            return Ok(ApiResult.Ok(result.Message));
        }
        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool CanUsePageBuilder => AdminAuthorization.CanUsePageBuilder(User);

        private string ActorId =>
            User.FindFirst("adminId")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User.Identity?.Name ??
            "unknown";

        private static Dictionary<string, string> AutoFillLanguage(
            Dictionary<string, string> dict)
        {
            var en = dict.GetValueOrDefault("en", string.Empty);
            foreach (var code in new[] { "vi", "cn" })
                if (!dict.ContainsKey(code) || string.IsNullOrEmpty(dict[code]))
                    dict[code] = en;
            return dict;
        }

        private static PageResponseDto MapToDto(Page p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Access = p.Access,
            Visible = p.Visible,
            Order = p.Order,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Seo = new PageSeoResponseDto
            {
                MetaTitle = p.Seo.MetaTitle,
                MetaDescription = p.Seo.MetaDescription
            },
            ParentPageId = p.ParentPageId,
            ParentSlug = p.ParentSlug,
            FullSlug = p.FullSlug ?? p.Slug,
            Card = p.Card != null ? new PageCardResponseDto
            {
                CardTitle = p.Card.CardTitle,
                CardContent = p.Card.CardContent,
                CardBackgroundType = p.Card.CardBackgroundType,
                CardBackgroundColor = p.Card.CardBackgroundColor,
                CardImageUrl = p.Card.CardImageUrl,
                IsCustomized = p.Card.IsCustomized
            } : null
        };
    }
}
