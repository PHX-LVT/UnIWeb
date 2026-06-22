using Contracts.Admin;
using FullProject.Models;
using FullProject.Utils;
using GlobalManager.Services.SectionServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/admin/canvas-section-presets")]
    public class CanvasSectionPresetsController : ControllerBase
    {
        private readonly CanvasSectionPresetService _service;

        public CanvasSectionPresetsController(CanvasSectionPresetService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var presets = await _service.GetAllAsync();
            return Ok(ApiResult.Ok(presets.Select(MapToDto).ToList()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CanvasSectionPresetCreateDto dto)
        {
            var preset = await _service.CreateFromSectionAsync(dto);
            if (preset is null)
                return NotFound(ApiResult.NotFound("Canvas section not found."));

            return Ok(ApiResult.Ok(MapToDto(preset), "Canvas preset saved."));
        }

        [HttpPost("{presetId}/apply")]
        public async Task<IActionResult> Apply(string presetId, [FromBody] CanvasSectionPresetApplyDto dto)
        {
            var section = await _service.ApplyAsync(presetId, dto);
            if (section is null)
                return NotFound(ApiResult.NotFound("Canvas preset not found."));

            return Ok(ApiResult.Ok(new { section.Id }, "Canvas preset inserted."));
        }

        [HttpDelete("{presetId}")]
        public async Task<IActionResult> Delete(string presetId)
        {
            var deleted = await _service.DeleteAsync(presetId);
            if (!deleted)
                return NotFound(ApiResult.NotFound("Canvas preset not found."));

            return Ok(ApiResult.Ok("Canvas preset deleted."));
        }

        private static CanvasSectionPresetResponseDto MapToDto(CanvasSectionPreset preset) => new()
        {
            Id = preset.Id,
            Name = preset.Name,
            BlockCount = preset.Blocks.Count,
            CreatedAt = preset.CreatedAt,
            UpdatedAt = preset.UpdatedAt
        };
    }
}
