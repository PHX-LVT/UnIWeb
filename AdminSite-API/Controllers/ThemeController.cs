using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    public class ThemeController : ControllerBase
    {
        private readonly ThemeService _service;

        public ThemeController(ThemeService service)
        {
            _service = service;
        }

        // GET api/admin/global/theme
        [HttpGet("api/admin/global/theme")]
        [Authorize]
        public async Task<IActionResult> GetAdmin()
        {
            var theme = await _service.GetAsync();
            return Ok(ApiResult.Ok(MapToDto(theme)));
        }

        // PUT api/admin/global/theme
        [HttpPut("api/admin/global/theme")]
        [Authorize]
        public async Task<IActionResult> Update([FromBody] ThemeUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(dto);
            return Ok(ApiResult.Ok(MapToDto(updated)));
        }


        private static ThemeResponseDto MapToDto(Models.SiteTheme t) => new()
        {
            FontBody = t.FontBody,
            FontHeading = t.FontHeading,
            TextSizeBase = t.TextSizeBase,
            TextSizeEyebrow = t.TextSizeEyebrow,
            TextSizeHeading = t.TextSizeHeading,
            TextSizeSubheading = t.TextSizeSubheading,
            TextSizeBody = t.TextSizeBody,
            TextSizeSmall = t.TextSizeSmall,
            TextSizeItemTitle = t.TextSizeItemTitle,
            ColorPrimary = t.ColorPrimary,
            ColorAccent = t.ColorAccent,
            ColorBackground = t.ColorBackground,
            ColorText = t.ColorText,
            BorderRadius = t.BorderRadius,
            ButtonSizeScale = t.ButtonSizeScale,
            ButtonTextSize = t.ButtonTextSize,
            AnimationsEnabled = t.AnimationsEnabled,
            AnimationSpeed = t.AnimationSpeed,
            SpacingScale = t.SpacingScale
        };
    }
}
