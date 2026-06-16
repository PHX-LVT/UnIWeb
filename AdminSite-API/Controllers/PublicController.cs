using FullProject.DTOs;
using FullProject.Services.PublicService;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FullProject.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/public")]
    public class PublicController : ControllerBase
    {
        private readonly PublicMetadataService _metadata;
        private readonly PublicPageAssemblyService _pages;
        private readonly PublicFormSubmissionHandler _forms;

        public PublicController(
            PublicMetadataService metadata,
            PublicPageAssemblyService pages,
            PublicFormSubmissionHandler forms)
        {
            _metadata = metadata;
            _pages = pages;
            _forms = forms;
        }

        [HttpGet("navigation")]
        public async Task<IActionResult> GetNavigation() =>
            Ok(ApiResult.Ok(await _metadata.GetNavigationAsync()));

        [HttpGet("branding")]
        public async Task<IActionResult> GetBranding() =>
            Ok(ApiResult.Ok(await _metadata.GetBrandingAsync()));

        [HttpGet("footer")]
        public async Task<IActionResult> GetFooter() =>
            Ok(ApiResult.Ok(await _metadata.GetFooterAsync()));

        [HttpGet("social")]
        public async Task<IActionResult> GetSocial() =>
            Ok(ApiResult.Ok(await _metadata.GetSocialAsync()));

        [HttpGet("global-buttons")]
        public async Task<IActionResult> GetGlobalButtons() =>
            Ok(ApiResult.Ok(await _metadata.GetGlobalButtonsAsync()));

        [HttpGet("theme")]
        public async Task<IActionResult> GetTheme() =>
            Ok(ApiResult.Ok(await _metadata.GetThemeAsync()));

        [HttpGet("languages")]
        public async Task<IActionResult> GetLanguages() =>
            Ok(ApiResult.Ok(await _metadata.GetLanguagesAsync()));

        [HttpGet("pages/{slug}")]
        public async Task<IActionResult> GetPage(string slug)
        {
            var page = await _pages.GetPageResponseAsync(slug);
            return page is null
                ? NotFound(ApiResult.NotFound("Page not found."))
                : Ok(ApiResult.Ok(page));
        }

        [HttpGet("pages/{parentSlug}/{childSlug}")]
        public async Task<IActionResult> GetChildPage(string parentSlug, string childSlug)
        {
            var page = await _pages.GetChildPageResponseAsync(parentSlug, childSlug);
            return page is null
                ? NotFound(ApiResult.NotFound("Page not found."))
                : Ok(ApiResult.Ok(page));
        }

        [HttpGet("content/{typeKey}/{slug}")]
        public async Task<IActionResult> GetContentPage(string typeKey, string slug)
        {
            var page = await _pages.GetContentPageAsync(typeKey, slug);
            return page is null
                ? NotFound(ApiResult.NotFound("Content not found."))
                : Ok(ApiResult.Ok(page));
        }

        [HttpPost("pages/{slug}/sections/{sectionId}/blocks/{blockId}/form/submit")]
        [EnableRateLimiting("public-form")]
        [RequestSizeLimit(32_768)]
        public Task<IActionResult> SubmitForm(
            string slug,
            string sectionId,
            string blockId,
            [FromBody] FormSubmitDto dto) =>
            _forms.SubmitPageFormAsync(slug, sectionId, blockId, dto);

        [HttpPost("pages/{parentSlug}/{childSlug}/sections/{sectionId}/blocks/{blockId}/form/submit")]
        [EnableRateLimiting("public-form")]
        [RequestSizeLimit(32_768)]
        public Task<IActionResult> SubmitChildPageForm(
            string parentSlug,
            string childSlug,
            string sectionId,
            string blockId,
            [FromBody] FormSubmitDto dto) =>
            _forms.SubmitChildPageFormAsync(parentSlug, childSlug, sectionId, blockId, dto);

        [HttpPost("forms/modal/{modalType}")]
        [EnableRateLimiting("public-form")]
        [RequestSizeLimit(16_384)]
        public Task<IActionResult> SubmitModalForm(string modalType, [FromBody] FormSubmitDto dto) =>
            _forms.SubmitModalFormAsync(modalType, dto);
    }
}
