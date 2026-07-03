using Contracts.Admin;
using Contracts.Auth;
using FullProject.Models;
using FullProject.Utils;
using FullProject.Services.SectionServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
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
            var (preset, error) = await _service.CreateFromSectionAsync(dto);
            if (error is not null)
                return BadRequest(ApiResult.BadRequest(error));

            return Ok(ApiResult.Ok(MapToDto(preset!), "Canvas preset saved."));
        }

        [HttpPost("{presetId}/apply")]
        public async Task<IActionResult> Apply(string presetId, [FromBody] CanvasSectionPresetApplyDto dto)
        {
            var (section, error) = await _service.ApplyAsync(presetId, dto);
            if (error is not null)
                return BadRequest(ApiResult.BadRequest(error));

            return Ok(ApiResult.Ok(new { section!.Id }, "Canvas preset inserted."));
        }

        [HttpDelete("{presetId}")]
        public async Task<IActionResult> Delete(string presetId)
        {
            var deleted = await _service.DeleteAsync(presetId);
            if (!deleted)
                return NotFound(ApiResult.NotFound("Canvas preset not found."));

            return Ok(ApiResult.Ok("Canvas preset deleted."));
        }

        private CanvasSectionPresetResponseDto MapToDto(CanvasSectionPreset preset)
        {
            var compatibility = _service.Compatibility(preset);
            return new CanvasSectionPresetResponseDto
            {
                Id = preset.Id,
                Name = preset.Name,
                BlockCount = preset.Blocks.Count,
                SchemaVersion = preset.SchemaVersion,
                SlotCount = preset.EditableSlots?.Count ?? 0,
                LockPolicy = new CanvasPresetLockPolicyDto
                {
                    LockGeometryOnApply = preset.LockPolicy?.LockGeometryOnApply ?? false,
                    LockContentOutsideSlots = preset.LockPolicy?.LockContentOutsideSlots ?? false
                },
                IsCompatible = compatibility.IsCompatible,
                CompatibilityMessage = compatibility.Message,
                CreatedAt = preset.CreatedAt,
                UpdatedAt = preset.UpdatedAt
            };
        }
    }
}
