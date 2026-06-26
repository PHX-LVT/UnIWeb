using FullProject.DTOs;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Auth;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/global/branding")]
    [Authorize(Policy = AdminPermissionKeys.ManageSettings)]
    public class BrandingController : ControllerBase
    {
        private readonly BrandingService _service;

        public BrandingController(BrandingService service)
        {
            _service = service;
        }

        // GET api/admin/global/branding
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var branding = await _service.GetAsync();
            return Ok(ApiResult.Ok(new BrandingResponseDto
            {
                CompanyName = branding.CompanyName,
                LogoUrl = branding.LogoUrl,
                Href = branding.Href
            }));
        }

        // PUT api/admin/global/branding
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] BrandingUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(dto);
            return Ok(ApiResult.Ok(new BrandingResponseDto
            {
                CompanyName = updated.CompanyName,
                LogoUrl = updated.LogoUrl,
                Href = updated.Href
            }, "Branding saved."));
        }
    }
}

