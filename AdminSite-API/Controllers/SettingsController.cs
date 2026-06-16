using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/settings")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _service;

        public SettingsController(SettingsService service)
        {
            _service = service;
        }

        // GET api/admin/settings/languages
        [HttpGet("languages")]
        public async Task<IActionResult> GetLanguages()
        {
            var settings = await _service.GetAsync();
            return Ok(ApiResult.Ok(new SiteSettingsResponseDto
            {
                DefaultLanguage = settings.DefaultLanguage,
                Languages = settings.Languages.Select(l => new LanguageResponseDto
                {
                    Slug = l.Slug,
                    Label = l.Label,
                    NativeName = l.NativeName,
                    Active = l.Active,
                    AdminEnabled = l.AdminEnabled,
                    UserEnabled = l.UserEnabled,
                    IsFallback = l.Slug == settings.DefaultLanguage,
                    Protected = l.Slug == settings.DefaultLanguage,
                    Direction = l.Direction,
                    Order = l.Order
                }).ToList()
            }));
        }

        // PUT api/admin/settings/languages
        [HttpPut("languages")]
        public async Task<IActionResult> UpdateLanguages([FromBody] SiteSettingsUpdateDto dto)
        {
            // EN must always remain active — enforce server-side
            await _service.UpdateAsync(dto);
            return Ok(ApiResult.Ok("Language settings saved."));
        }

        // GET api/admin/settings/admin-appearance
        [HttpGet("admin-appearance")]
        public async Task<IActionResult> GetAdminAppearance()
        {
            var preset = await _service.GetAdminAppearancePresetAsync();
            return Ok(ApiResult.Ok(new AdminAppearanceResponseDto { Preset = preset }));
        }

        // PUT api/admin/settings/admin-appearance
        [HttpPut("admin-appearance")]
        public async Task<IActionResult> UpdateAdminAppearance([FromBody] AdminAppearanceUpdateDto dto)
        {
            var preset = await _service.UpdateAdminAppearancePresetAsync(dto.Preset);
            return Ok(ApiResult.Ok(new AdminAppearanceResponseDto { Preset = preset }, "Admin appearance saved."));
        }

        // GET api/admin/settings/glossary
        [HttpGet("glossary")]
        public async Task<IActionResult> GetGlossary()
        {
            var terms = await _service.GetGlossaryAsync();
            return Ok(ApiResult.Ok(terms.Select(t => new GlossaryTermResponseDto
            {
                Id = t.Id,
                Term = t.Term,
                Description = t.Description
            }).ToList()));
        }

        // POST api/admin/settings/glossary
        [HttpPost("glossary")]
        public async Task<IActionResult> CreateTerm([FromBody] GlossaryTermCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Term))
                return BadRequest(ApiResult.BadRequest("Term is required."));

            var created = await _service.CreateTermAsync(dto);
            return Ok(ApiResult.Created(new GlossaryTermResponseDto
            {
                Id = created.Id,
                Term = created.Term,
                Description = created.Description
            }, "Term created."));
        }

        // PUT api/admin/settings/glossary/:termId
        [HttpPut("glossary/{termId}")]
        public async Task<IActionResult> UpdateTerm(string termId,
            [FromBody] GlossaryTermUpdateDto dto)
        {
            var ok = await _service.UpdateTermAsync(termId, dto);
            if (!ok) return NotFound(ApiResult.NotFound("Term not found."));
            return Ok(ApiResult.Ok("Term updated."));
        }

        // DELETE api/admin/settings/glossary/:termId
        [HttpDelete("glossary/{termId}")]
        public async Task<IActionResult> DeleteTerm(string termId)
        {
            var ok = await _service.DeleteTermAsync(termId);
            if (!ok) return NotFound(ApiResult.NotFound("Term not found."));
            return Ok(ApiResult.Ok("Term deleted."));
        }
    }
}
