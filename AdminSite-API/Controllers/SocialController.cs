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
    [Route("api/admin/social")]
    [Authorize(Policy = AdminPermissionKeys.ManageSettings)]
    public class SocialController : ControllerBase
    {
        private readonly SocialButtonsService _service;

        public SocialController(SocialButtonsService service)
        {
            _service = service;
        }

        // GET api/admin/social
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var group = await _service.GetGroupAsync();
            return Ok(ApiResult.Ok(new SocialButtonGroupResponseDto
            {
                GroupVisible = group.GroupVisible,
                Buttons = group.Buttons.Select(MapToDto).ToList()
            }));
        }

        // POST api/admin/social
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SocialButtonCreateDto dto)
        {
            var group = await _service.GetGroupAsync();
            if (group.Buttons.Count >= 4)
                return BadRequest(ApiResult.BadRequest(
                    "Maximum of 4 social buttons allowed."));

            var created = await _service.CreateAsync(dto);
            return Ok(ApiResult.Created(MapToDto(created), "Social button created."));
        }

        // PUT api/admin/social/:buttonId
        [HttpPut("{buttonId}")]
        public async Task<IActionResult> Update(string buttonId, [FromBody] SocialButtonUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(buttonId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Social button not found."));
            return Ok(ApiResult.Ok(MapToDto(updated)));
        }

        // DELETE api/admin/social/:buttonId
        [HttpDelete("{buttonId}")]
        public async Task<IActionResult> Delete(string buttonId)
        {
            var group = await _service.GetGroupAsync();
            if (group.Buttons.Count <= 1)
                return BadRequest(ApiResult.BadRequest(
                    "At least 1 social button must remain."));

            var ok = await _service.DeleteAsync(buttonId);
            if (!ok) return NotFound(ApiResult.NotFound("Social button not found."));
            return Ok(ApiResult.Ok("Social button deleted."));
        }

        // PUT api/admin/social/:buttonId/visibility
        [HttpPut("{buttonId}/visibility")]
        public async Task<IActionResult> SetButtonVisibility(string buttonId,
            [FromBody] VisibilityDto dto)
        {
            var ok = await _service.SetButtonVisibilityAsync(buttonId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Social button not found."));
            return Ok(ApiResult.Ok($"Social button {(dto.Visible ? "shown" : "hidden")}."));
        }

        // PUT api/admin/social/visibility  — toggles entire group
        [HttpPut("visibility")]
        public async Task<IActionResult> SetGroupVisibility([FromBody] VisibilityDto dto)
        {
            await _service.SetGroupVisibilityAsync(dto.Visible);
            return Ok(ApiResult.Ok(
                $"Social group {(dto.Visible ? "shown" : "hidden")}."));
        }

        private static SocialButtonResponseDto MapToDto(SocialButton b) => new()
        {
            Id = b.Id,
            Label = b.Label,
            Icon = b.Icon,
            Href = b.Href,
            Visible = b.Visible,
            Order = b.Order
        };
    }
}
