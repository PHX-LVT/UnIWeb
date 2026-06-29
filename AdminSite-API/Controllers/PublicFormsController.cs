using Contracts.Forms;
using FullProject.Services.FormServices;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FullProject.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/forms")]
public sealed class PublicFormsController : ControllerBase
{
    private readonly FormDefinitionService _definitions;
    private readonly PublicFormSubmissionService _submissions;

    public PublicFormsController(
        FormDefinitionService definitions,
        PublicFormSubmissionService submissions)
    {
        _definitions = definitions;
        _submissions = submissions;
    }

    [HttpGet("{formKey}")]
    public async Task<IActionResult> GetDefinition(string formKey)
    {
        var definition = await _definitions.GetActiveByKeyAsync(formKey);
        return definition is null
            ? NotFound(ApiResult.NotFound("Form not found."))
            : Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definition)));
    }

    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetDefinitionById(string id)
    {
        var definition = await _definitions.GetActiveByIdAsync(id);
        return definition is null
            ? NotFound(ApiResult.NotFound("Form not found."))
            : Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definition)));
    }

    [HttpPost("{formKey}/submit")]
    [EnableRateLimiting("public-form")]
    [RequestSizeLimit(32_768)]
    public async Task<IActionResult> Submit(string formKey, [FromBody] PublicFormSubmitRequest request)
    {
        var result = await _submissions.SubmitAsync(formKey, request);
        return StatusCode(result.StatusCode, result.Response);
    }
}
