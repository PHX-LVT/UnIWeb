using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/global/buttons")]
    [Authorize]
    public class GlobalButtonsController : ControllerBase
    {
        private readonly GlobalButtonsService _service;

        public GlobalButtonsController(GlobalButtonsService service)
        {
            _service = service;
        }

        // GET api/admin/global/buttons
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var buttons = await _service.GetAllAsync();
            return Ok(ApiResult.Ok(buttons.Select(MapToDto).ToList()));
        }

        // POST api/admin/global/buttons
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GlobalButtonCreateDto dto)
        {
            var all = await _service.GetAllAsync();
            if (all.Count >= 4)
                return BadRequest(ApiResult.BadRequest(
                    "Maximum of 4 global buttons allowed."));

            var created = await _service.CreateAsync(dto);
            return Ok(ApiResult.Created(MapToDto(created), "Global button created."));
        }

        // PUT api/admin/global/buttons/:buttonId
        [HttpPut("{buttonId}")]
        public async Task<IActionResult> Update(string buttonId, [FromBody] GlobalButtonUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(buttonId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Button not found."));
            return Ok(ApiResult.Ok(MapToDto(updated)));
        }

        // DELETE api/admin/global/buttons/:buttonId
        [HttpDelete("{buttonId}")]
        public async Task<IActionResult> Delete(string buttonId)
        {
            var ok = await _service.DeleteAsync(buttonId);
            if (!ok) return NotFound(ApiResult.NotFound("Button not found."));
            return Ok(ApiResult.Ok("Button deleted."));
        }

        // PUT api/admin/global/buttons/:buttonId/visibility
        [HttpPut("{buttonId}/visibility")]
        public async Task<IActionResult> SetVisibility(string buttonId,
            [FromBody] VisibilityDto dto)
        {
            var ok = await _service.SetVisibilityAsync(buttonId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Button not found."));
            return Ok(ApiResult.Ok($"Button {(dto.Visible ? "shown" : "hidden")}."));
        }

        // PUT api/admin/global/buttons/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder([FromBody] ReorderDto dto)
        {
            await _service.ReorderAsync(dto.OrderedIds);
            return Ok(ApiResult.Ok("Buttons reordered."));
        }

        private static GlobalButtonResponseDto MapToDto(GlobalButton b) => new()
        {
            Id = b.Id,
            LabelText = b.LabelText,
            Action = b.Action,
            Href = b.Href,
            FormDefinitionId = b.FormDefinitionId,
            Position = b.Position,
            Visible = b.Visible,
            Order = b.Order
        };
    }
}
