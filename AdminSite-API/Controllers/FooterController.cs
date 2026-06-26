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
    [Route("api/admin/footer")]
    [Authorize(Policy = AdminPermissionKeys.ManageSettings)]
    public class FooterController : ControllerBase
    {
        private readonly FooterService _service;

        public FooterController(FooterService service)
        {
            _service = service;
        }

        // GET api/admin/footer
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var footer = await _service.GetAsync();
            return Ok(ApiResult.Ok(MapToDto(footer)));
        }

        // PUT api/admin/footer
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] FooterUpdateDto dto)
        {
            var updated = await _service.UpdateCompanyNameAsync(dto.CompanyName);
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer identity saved."));
        }

        [HttpPost("groups")]
        public async Task<IActionResult> CreateGroup([FromBody] FooterGroupCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Label))
                return BadRequest(ApiResult.BadRequest("Group label is required."));
            var updated = await _service.CreateGroupAsync(dto.Label);
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer group created."));
        }

        [HttpPut("groups/{groupId}")]
        public async Task<IActionResult> UpdateGroup(string groupId, [FromBody] FooterGroupUpdateDto dto)
        {
            var updated = await _service.UpdateGroupAsync(groupId, dto.Label);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer group not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer group saved."));
        }

        [HttpDelete("groups/{groupId}")]
        public async Task<IActionResult> DeleteGroup(string groupId)
        {
            var updated = await _service.DeleteGroupAsync(groupId);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer group not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer group deleted."));
        }

        [HttpPut("groups/{groupId}/visibility")]
        public async Task<IActionResult> SetGroupVisibility(string groupId, [FromBody] VisibilityDto dto)
        {
            var updated = await _service.SetGroupVisibilityAsync(groupId, dto.Visible);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer group not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), $"Footer group {(dto.Visible ? "shown" : "hidden")}."));
        }

        [HttpPost("groups/{groupId}/links")]
        public async Task<IActionResult> CreateLink(string groupId, [FromBody] FooterLinkCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Label))
                return BadRequest(ApiResult.BadRequest("Link label is required."));
            var updated = await _service.CreateLinkAsync(groupId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer group not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer link created."));
        }

        [HttpPut("groups/{groupId}/links/{linkId}")]
        public async Task<IActionResult> UpdateLink(string groupId, string linkId, [FromBody] FooterLinkUpdateDto dto)
        {
            var updated = await _service.UpdateLinkAsync(groupId, linkId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer link not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer link saved."));
        }

        [HttpDelete("groups/{groupId}/links/{linkId}")]
        public async Task<IActionResult> DeleteLink(string groupId, string linkId)
        {
            var updated = await _service.DeleteLinkAsync(groupId, linkId);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer link not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer link deleted."));
        }

        [HttpPut("groups/{groupId}/links/{linkId}/visibility")]
        public async Task<IActionResult> SetLinkVisibility(string groupId, string linkId, [FromBody] VisibilityDto dto)
        {
            var updated = await _service.SetLinkVisibilityAsync(groupId, linkId, dto.Visible);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer link not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), $"Footer link {(dto.Visible ? "shown" : "hidden")}."));
        }

        // ReorderGroups stays the same — no model needed
        // PUT api/admin/footer/groups/reorder
        [HttpPut("groups/reorder")]
        public async Task<IActionResult> ReorderGroups([FromBody] ReorderDto dto)
        {
            var updated = await _service.ReorderGroupsAsync(dto.OrderedIds);
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer groups reordered."));
        }

        [HttpPut("groups/{groupId}/links/reorder")]
        public async Task<IActionResult> ReorderLinks(string groupId, [FromBody] ReorderDto dto)
        {
            var updated = await _service.ReorderLinksAsync(groupId, dto.OrderedIds);
            if (updated is null) return NotFound(ApiResult.NotFound("Footer group or links not found."));
            return Ok(ApiResult.Ok(MapToDto(updated), "Footer links reordered."));
        }
        // ── Mapping ───────────────────────────────────────────

        private static FooterResponseDto MapToDto(Footer f) => new()
        {
            CompanyName = f.CompanyName,
            Groups = f.Groups.Select(g => new FooterGroupResponseDto
            {
                Id = g.Id,
                Label = g.Label,
                Visible = g.Visible,
                Order = g.Order,
                Links = g.Links.Select(l => new FooterLinkResponseDto
                {
                    Id = l.Id,
                    Label = l.Label,
                    Href = l.Href,
                    Visible = l.Visible,
                    Order = l.Order
                }).ToList()
            }).ToList()
        };
    }
}
