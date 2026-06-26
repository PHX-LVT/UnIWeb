using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Auth;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/pages/{pageId}/children")]
    [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
    public class ChildPagesController : ControllerBase
    {
        private readonly PageService _pageService;

        public ChildPagesController(PageService pageService)
        {
            _pageService = pageService;
        }

        // GET api/admin/pages/:pageId/children
        [HttpGet]
        public async Task<IActionResult> GetAll(string pageId)
        {
            var parent = await _pageService.GetByIdAsync(pageId);
            if (parent is null) return NotFound(ApiResult.NotFound("Page not found."));

            var children = await _pageService.GetChildrenAsync(pageId);
            return Ok(ApiResult.Ok(children.Select(MapToDto).ToList()));
        }

        // POST api/admin/pages/:pageId/children
        [HttpPost]
        public async Task<IActionResult> Create(string pageId,
            [FromBody] ChildPageCreateDto dto)
        {
            var parent = await _pageService.GetByIdAsync(pageId);
            if (parent is null)
                return NotFound(ApiResult.NotFound("Parent page not found."));

            // Enforce max depth of 1
            if (parent.ParentPageId != null)
                return BadRequest(ApiResult.BadRequest(
                    "Child pages cannot have child pages. Maximum depth is 1."));

            var enName = dto.Name.GetValueOrDefault("en", string.Empty);
            if (string.IsNullOrWhiteSpace(enName))
                return BadRequest(ApiResult.BadRequest("English name is required."));

            var page = new Page
            {
                Name = dto.Name,
                ParentPageId = pageId,
                ParentSlug = parent.Slug,
                Access = dto.Access,
                Visible = dto.Visible,
                Status = PageStatus.Draft,
                Card = new PageCard
                {
                    CardTitle = dto.Name,
                    IsCustomized = false
                },
            };

            var created = await _pageService.CreateAsync(page, enName);
            return Ok(ApiResult.Created(MapToDto(created), "Child page created."));
        }

        // PUT api/admin/pages/:pageId/children/:childId
        [HttpPut("{childId}")]
        public async Task<IActionResult> Update(string pageId, string childId,
            [FromBody] PageUpdateDto dto)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var updated = await _pageService.UpdateAsync(childId, dto);
            return Ok(ApiResult.Ok(MapToDto(updated!), "Child page updated."));
        }

        // DELETE api/admin/pages/:pageId/children/:childId
        [HttpDelete("{childId}")]
        public async Task<IActionResult> Delete(string pageId, string childId)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var ok = await _pageService.DeleteAsync(childId);
            if (!ok) return NotFound(ApiResult.NotFound("Child page not found."));
            return Ok(ApiResult.Ok("Child page deleted."));
        }

        // PUT api/admin/pages/:pageId/children/:childId/visibility
        [HttpPut("{childId}/visibility")]
        public async Task<IActionResult> SetVisibility(string pageId, string childId,
            [FromBody] VisibilityDto dto)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var ok = await _pageService.SetVisibilityAsync(childId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Child page not found."));
            return Ok(ApiResult.Ok($"Child page {(dto.Visible ? "shown" : "hidden")}."));
        }

        // PUT api/admin/pages/:pageId/children/:childId/access
        [HttpPut("{childId}/access")]
        public async Task<IActionResult> SetAccess(string pageId, string childId,
            [FromBody] VisibilityDto dto)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var ok = await _pageService.SetAccessAsync(childId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Child page not found."));
            return Ok(ApiResult.Ok($"Child page access {(dto.Visible ? "enabled" : "disabled")}."));
        }

        // PUT api/admin/pages/:pageId/children/:childId/card
        [HttpPut("{childId}/card")]
        public async Task<IActionResult> UpdateCard(string pageId, string childId,
            [FromBody] PageCardDto dto)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var ok = await _pageService.UpdateCardAsync(childId, dto);
            if (!ok) return NotFound(ApiResult.NotFound("Child page not found."));
            return Ok(ApiResult.Ok("Card updated."));
        }

        // PUT api/admin/pages/:pageId/children/:childId/card/reset
        [HttpPut("{childId}/card/reset")]
        public async Task<IActionResult> ResetCard(string pageId, string childId)
        {
            var child = await _pageService.GetByIdAsync(childId);
            if (child is null || child.ParentPageId != pageId)
                return NotFound(ApiResult.NotFound("Child page not found."));

            var ok = await _pageService.ResetCardAsync(childId);
            if (!ok) return NotFound(ApiResult.NotFound("Child page not found."));
            return Ok(ApiResult.Ok("Card reset to auto-sync from Hero section."));
        }

        // PUT api/admin/pages/:pageId/children/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder(string pageId, [FromBody] ReorderDto dto)
        {
            var ok = await _pageService.ReorderChildrenAsync(pageId, dto.OrderedIds);
            if (!ok) return BadRequest(ApiResult.BadRequest("Reorder failed."));
            return Ok(ApiResult.Ok("Children reordered."));
        }

        private static PageResponseDto MapToDto(Page p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            FullSlug = p.FullSlug ?? p.Slug,
            ParentPageId = p.ParentPageId,
            ParentSlug = p.ParentSlug,
            Access = p.Access,
            Visible = p.Visible,
            Order = p.Order,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Card = p.Card != null ? new PageCardResponseDto
            {
                CardTitle = p.Card.CardTitle,
                CardContent = p.Card.CardContent,
                CardBackgroundType = p.Card.CardBackgroundType,
                CardBackgroundColor = p.Card.CardBackgroundColor,
                CardImageUrl = p.Card.CardImageUrl,
                IsCustomized = p.Card.IsCustomized
            } : null,
            Seo = new PageSeoResponseDto
            {
                MetaTitle = p.Seo.MetaTitle,
                MetaDescription = p.Seo.MetaDescription
            }
        };
    }
}

