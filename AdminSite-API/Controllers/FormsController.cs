using Contracts.Forms;
using FullProject.Services;
using FullProject.Services.FormServices;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Auth;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/forms")]
    [Authorize(Policy = AdminPermissionKeys.ManageContent)]
    public class FormsController : ControllerBase
    {
        private readonly FormSubmissionService _submissions;
        private readonly FormDefinitionService _definitions;
        private readonly FormValidationService _validation;

        public FormsController(
            FormSubmissionService submissions,
            FormDefinitionService definitions,
            FormValidationService validation)
        {
            _submissions = submissions;
            _definitions = definitions;
            _validation = validation;
        }

        [HttpGet("submissions")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? formKey,
            [FromQuery] FormSubmissionStatus? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var submissions = await _submissions.GetAllAsync(formKey, status, search, from, to);
            return Ok(ApiResult.Ok(submissions
                .Where(s => !string.Equals(s.PageId, "modal:sync", StringComparison.OrdinalIgnoreCase))
                .Select(MapSubmission)
                .ToList()));
        }

        [HttpGet("submissions/{submissionId}")]
        public async Task<IActionResult> GetSubmission(string submissionId)
        {
            var submission = await _submissions.GetByIdAsync(submissionId);
            return submission is null
                ? NotFound(ApiResult.NotFound("Submission not found."))
                : Ok(ApiResult.Ok(MapSubmission(submission)));
        }

        [HttpPut("submissions/{submissionId}")]
        public async Task<IActionResult> UpdateSubmission(
            string submissionId,
            [FromBody] ManagedFormSubmissionUpdateRequest request)
        {
            var ok = await _submissions.UpdateAsync(submissionId, request.Status, request.InternalNotes);
            return ok
                ? Ok(ApiResult.Ok("Submission updated."))
                : NotFound(ApiResult.NotFound("Submission not found."));
        }

        [HttpPost("submissions/bulk-status")]
        public async Task<IActionResult> BulkStatus([FromBody] BulkFormSubmissionStatusRequest request)
        {
            var count = await _submissions.BulkStatusAsync(request.Ids, request.Status);
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} submissions updated."));
        }

        [HttpPost("submissions/bulk-delete")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkFormSubmissionDeleteRequest request)
        {
            var count = await _submissions.BulkDeleteAsync(request.Ids);
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} submissions deleted."));
        }

        [HttpDelete("submissions/{submissionId}")]
        public async Task<IActionResult> Delete(string submissionId)
        {
            var ok = await _submissions.DeleteAsync(submissionId);
            return ok
                ? Ok(ApiResult.Ok("Submission deleted."))
                : NotFound(ApiResult.NotFound("Submission not found."));
        }

        [HttpGet("definitions")]
        public async Task<IActionResult> GetDefinitions()
        {
            var definitions = await _definitions.GetAllAsync();
            return Ok(ApiResult.Ok(definitions.Select(FormDefinitionService.MapPublic).ToList()));
        }

        [HttpGet("definitions/{id}")]
        public async Task<IActionResult> GetDefinition(string id)
        {
            var definition = await _definitions.GetByIdAsync(id);
            return definition is null
                ? NotFound(ApiResult.NotFound("Form definition not found."))
                : Ok(ApiResult.Ok(FormDefinitionService.MapPublic(definition)));
        }


        [HttpGet("definitions/{id}/usage")]
        public async Task<IActionResult> GetDefinitionUsage(string id)
        {
            var definition = await _definitions.GetByIdAsync(id);
            if (definition is null)
                return NotFound(ApiResult.NotFound("Form definition not found."));

            var usage = await _definitions.GetUsageAsync(id);
            return Ok(ApiResult.Ok(usage));
        }
        [HttpPost("definitions")]
        public async Task<IActionResult> CreateDefinition([FromBody] FormDefinitionUpsertRequest request)
        {
            var errors = _validation.ValidateDefinition(request);
            if (errors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", errors)));

            var definition = await _definitions.UpsertAsync(request);
            return Ok(ApiResult.Ok(FormDefinitionService.MapPublic(definition), "Form definition saved."));
        }

        [HttpPut("definitions/{id}")]
        public async Task<IActionResult> UpdateDefinition(string id, [FromBody] FormDefinitionUpsertRequest request)
        {
            var existing = await _definitions.GetByIdAsync(id);
            if (existing is null)
                return NotFound(ApiResult.NotFound("Form definition not found."));

            var normalizedRequestKey = FormDefinitionService.NormalizeKey(request.Key);
            if (normalizedRequestKey is null || !string.Equals(normalizedRequestKey, existing.Key, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResult.BadRequest("Form key cannot be changed after creation."));

            if (existing.Active && !request.Active && await _definitions.IsReferencedAsync(id))
                return BadRequest(ApiResult.BadRequest("This form is used by one or more FormBlocks or buttons. Remove those usages before disabling it."));

            var errors = _validation.ValidateDefinition(request);
            if (errors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", errors)));

            var definition = await _definitions.UpsertAsync(request, id);
            return Ok(ApiResult.Ok(FormDefinitionService.MapPublic(definition), "Form definition saved."));
        }

        [HttpDelete("definitions/{id}")]
        public async Task<IActionResult> DeleteDefinition(string id)
        {
            if (await _definitions.IsReferencedAsync(id))
                return BadRequest(ApiResult.BadRequest("This form is still used by one or more FormBlocks or buttons. Remove those usages before deleting it."));

            if (await _definitions.HasSubmissionsAsync(id))
                return BadRequest(ApiResult.BadRequest("This form already has submissions. Disable it instead, or permanently delete its submissions first."));

            var ok = await _definitions.DeleteAsync(id);
            return ok
                ? Ok(ApiResult.Ok("Form definition deleted."))
                : NotFound(ApiResult.NotFound("Form definition not found."));
        }

        private static ManagedFormSubmissionResponse MapSubmission(Models.FormSubmission submission) => new()
        {
            Id = submission.Id,
            FormId = submission.FormId,
            FormKey = submission.FormKey,
            FormName = submission.FormName,
            Language = submission.Language,
            SourcePage = submission.SourcePage,
            Status = submission.Status,
            Fields = submission.Fields
                .OrderBy(field => field.Order)
                .Select(field => new FormSubmissionFieldResponse
                {
                    Key = field.Key,
                    Label = field.Label,
                    Type = field.Type,
                    Value = field.Value,
                    Order = field.Order
                })
                .ToList(),
            InternalNotes = submission.InternalNotes,
            SubmittedAt = submission.SubmittedAt,
            UpdatedAt = submission.UpdatedAt
        };
    }

    public sealed class BulkFormSubmissionStatusRequest
    {
        public List<string> Ids { get; set; } = new();
        public FormSubmissionStatus Status { get; set; }
    }

    public sealed class BulkFormSubmissionDeleteRequest
    {
        public List<string> Ids { get; set; } = new();
    }
}
