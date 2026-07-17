using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Contracts.Global;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Auth;
using FullProject.Services.FormServices;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/global/theme")]
    [Authorize(Policy = AdminPermissionKeys.ManageSettings)]
    public class ThemeController : ControllerBase
    {
        private readonly ThemeService _service;
        private readonly FormDefinitionService _forms;

        public ThemeController(ThemeService service, FormDefinitionService forms)
        {
            _service = service;
            _forms = forms;
        }

        // GET api/admin/global/theme
        [HttpGet]
        public async Task<IActionResult> GetAdmin()
        {
            var theme = await _service.GetAsync();
            return Ok(ApiResult.Ok(MapToDto(theme)));
        }

        // PUT api/admin/global/theme
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ThemeUpdateDto dto)
        {
            var validationErrors = ValidateFonts(dto);
            if (validationErrors.Count > 0)
                return BadRequest(ApiResult.BadRequest("Theme contains unsupported font values.", validationErrors));

            var updated = await _service.UpdateAsync(dto);
            await _forms.ReflowThemeInheritedFormsAsync(updated.SpacingScale);
            return Ok(ApiResult.Ok(MapToDto(updated)));
        }


        private static ThemeResponseDto MapToDto(Models.SiteTheme t) => new()
        {
            FontBody = ThemeFontCatalog.NormalizeNameOrDefault(t.FontBody),
            FontHeading = ThemeFontCatalog.NormalizeNameOrDefault(t.FontHeading),
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
            ButtonStyle = t.ButtonStyle,
            ButtonColorRole = t.ButtonColorRole,
            ButtonRadius = t.ButtonRadius,
            ButtonSizeScale = t.ButtonSizeScale,
            ButtonTextSize = t.ButtonTextSize,
            AnimationsEnabled = t.AnimationsEnabled,
            AnimationSpeed = t.AnimationSpeed,
            SpacingScale = t.SpacingScale
        };

        private static List<string> ValidateFonts(ThemeUpdateDto dto)
        {
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(dto.FontBody) && !ThemeFontCatalog.IsAllowed(dto.FontBody))
                errors.Add("Body font is not supported.");

            if (!string.IsNullOrWhiteSpace(dto.FontHeading) && !ThemeFontCatalog.IsAllowed(dto.FontHeading))
                errors.Add("Heading font is not supported.");

            return errors;
        }
    }
}
