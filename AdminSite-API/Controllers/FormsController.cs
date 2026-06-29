using Contracts.Forms;
using FullProject.Models;
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
    [Authorize]
    public class FormsController : ControllerBase
    {
        private readonly FormSubmissionService _submissions;
        private readonly FormSubmissionExportService _submissionExports;
        private readonly FormDefinitionService _definitions;
        private readonly FormInputTypeService _inputTypes;
        private readonly FormValidationService _validation;
        private readonly AuthService _auth;

        public FormsController(
            FormSubmissionService submissions,
            FormSubmissionExportService submissionExports,
            FormDefinitionService definitions,
            FormInputTypeService inputTypes,
            FormValidationService validation,
            AuthService auth)
        {
            _submissions = submissions;
            _submissionExports = submissionExports;
            _definitions = definitions;
            _inputTypes = inputTypes;
            _validation = validation;
            _auth = auth;
        }

        [HttpGet("submissions")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormSubmissions)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? formId,
            [FromQuery] string? formKey,
            [FromQuery] FormSubmissionStatus? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var submissions = await _submissions.GetAllAsync(formKey, status, search, from, to, formId);
            var definitions = await _definitions.GetAllAsync();
            return Ok(ApiResult.Ok(submissions
                .Where(s => !string.Equals(s.PageId, "modal:sync", StringComparison.OrdinalIgnoreCase))
                .Select(submission => MapSubmission(submission, definitions))
                .ToList()));
        }

        [HttpGet("submissions/export")]
        [Authorize(Policy = AdminPermissionKeys.ExportFormSubmissions)]
        public async Task<IActionResult> ExportSubmissions(
            [FromQuery] string? formId,
            [FromQuery] string? formKey,
            [FromQuery] FormSubmissionStatus? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? language)
        {
            var submissions = await _submissions.GetAllAsync(formKey, status, search, from, to, formId);
            var visibleSubmissions = submissions
                .Where(s => !string.Equals(s.PageId, "modal:sync", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var definitions = await _definitions.GetAllAsync();
            var bytes = _submissionExports.BuildXlsx(visibleSubmissions, definitions, language ?? "en");
            var filename = $"form-submissions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        [HttpGet("submissions/assignees")]
        [Authorize(Policy = AdminPermissionKeys.ManageFormSubmissions)]
        public async Task<IActionResult> GetAssignees()
        {
            var users = await _auth.GetUsersAsync();
            var assignees = users
                .Select(user =>
                {
                    AuthService.NormalizeUserDefaults(user);
                    return user;
                })
                .Where(user => user.Status == AdminUserStatus.Active && user.Role != AdminRole.Viewer)
                .OrderBy(user => DisplayName(user))
                .Select(user => new FormSubmissionAssigneeResponse
                {
                    Id = user.Id,
                    DisplayName = DisplayName(user),
                    Email = user.Email
                })
                .ToList();

            return Ok(ApiResult.Ok(assignees));
        }

        [HttpGet("submissions/{submissionId}")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormSubmissions)]
        public async Task<IActionResult> GetSubmission(string submissionId)
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<ManagedFormSubmissionResponse>());

            var submission = await _submissions.GetByIdAsync(submissionId);
            if (submission is null)
                return NotFound(ApiResult.NotFound("Submission not found."));

            await _submissions.MarkViewedAsync(submissionId, actor.Id, DisplayName(actor));
            var updated = await _submissions.GetByIdAsync(submissionId);
            var definitions = await _definitions.GetAllAsync();
            return Ok(ApiResult.Ok(MapSubmission(updated ?? submission, definitions)));
        }

        [HttpPut("submissions/{submissionId}")]
        [Authorize(Policy = AdminPermissionKeys.ManageFormSubmissions)]
        public async Task<IActionResult> UpdateSubmission(
            string submissionId,
            [FromBody] ManagedFormSubmissionUpdateRequest request)
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<ManagedFormSubmissionResponse>());

            var (assignedToAdminId, assignedToAdminName, assignmentError) = await ResolveAssigneeAsync(request.AssignedToAdminId);
            if (assignmentError is not null)
                return BadRequest(ApiResult.BadRequest(assignmentError));

            var updated = await _submissions.UpdateWorkflowAsync(
                submissionId,
                request.Status,
                request.InternalNotes,
                assignedToAdminId,
                assignedToAdminName,
                actor.Id,
                DisplayName(actor));

            if (updated is null)
                return NotFound(ApiResult.NotFound("Submission not found."));

            var definitions = await _definitions.GetAllAsync();
            return Ok(ApiResult.Ok(MapSubmission(updated, definitions), "Submission updated."));
        }

        [HttpPost("submissions/bulk-status")]
        [Authorize(Policy = AdminPermissionKeys.ManageFormSubmissions)]
        public async Task<IActionResult> BulkStatus([FromBody] BulkFormSubmissionStatusRequest request)
        {
            var actor = await CurrentAdminAsync();
            if (actor is null) return Unauthorized(ApiResult.Unauthorized<object>());

            var count = await _submissions.BulkStatusAsync(request.Ids, request.Status, actor.Id, DisplayName(actor));
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} submissions updated."));
        }

        [HttpPost("submissions/bulk-delete")]
        [Authorize(Policy = AdminPermissionKeys.ManageFormSubmissions)]
        public async Task<IActionResult> BulkDelete([FromBody] BulkFormSubmissionDeleteRequest request)
        {
            var count = await _submissions.BulkDeleteAsync(request.Ids);
            return Ok(ApiResult.Ok(new { Count = count }, $"{count} submissions deleted."));
        }

        [HttpDelete("submissions/{submissionId}")]
        [Authorize(Policy = AdminPermissionKeys.ManageFormSubmissions)]
        public async Task<IActionResult> Delete(string submissionId)
        {
            var ok = await _submissions.DeleteAsync(submissionId);
            return ok
                ? Ok(ApiResult.Ok("Submission deleted."))
                : NotFound(ApiResult.NotFound("Submission not found."));
        }

        [HttpGet("definitions")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormDefinitions)]
        public async Task<IActionResult> GetDefinitions()
        {
            var definitions = await _definitions.GetAllAsync();
            return Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definitions)));
        }

        [HttpGet("definitions/{id}")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormDefinitions)]
        public async Task<IActionResult> GetDefinition(string id)
        {
            var definition = await _definitions.GetByIdAsync(id);
            return definition is null
                ? NotFound(ApiResult.NotFound("Form definition not found."))
                : Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definition)));
        }


        [HttpGet("definitions/{id}/usage")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormDefinitions)]
        public async Task<IActionResult> GetDefinitionUsage(string id)
        {
            var definition = await _definitions.GetByIdAsync(id);
            if (definition is null)
                return NotFound(ApiResult.NotFound("Form definition not found."));

            var usage = await _definitions.GetUsageAsync(id);
            return Ok(ApiResult.Ok(usage));
        }
        [HttpPost("definitions")]
        [Authorize(Policy = AdminPermissionKeys.EditFormDefinitions)]
        public async Task<IActionResult> CreateDefinition([FromBody] FormDefinitionUpsertRequest request)
        {
            var errors = await _validation.ValidateDefinitionAsync(request);
            if (errors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", errors)));

            var normalizedRequestKey = FormDefinitionService.NormalizeKey(request.Key);
            if (normalizedRequestKey is not null && await _definitions.GetByKeyAsync(normalizedRequestKey) is not null)
                return BadRequest(ApiResult.BadRequest("Form Key already exists. Use a different Form Key."));

            try
            {
                var definition = await _definitions.UpsertAsync(request);
                return Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definition), "Form definition saved."));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Form Key already exists", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResult.BadRequest("Form Key already exists. Use a different Form Key."));
            }
        }

        [HttpPut("definitions/{id}")]
        [Authorize(Policy = AdminPermissionKeys.EditFormDefinitions)]
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

            var permittedInactiveTypes = existing.Fields
                .Select(field => FormInputTypeCatalog.NormalizeType(field.Type))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var errors = await _validation.ValidateDefinitionAsync(request, permittedInactiveTypes);
            if (errors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", errors)));

            var definition = await _definitions.UpsertAsync(request, id);
            return Ok(ApiResult.Ok(await _definitions.MapPublicAsync(definition), "Form definition saved."));
        }

        [HttpGet("types")]
        [Authorize(Policy = AdminPermissionKeys.ViewFormDefinitions)]
        public async Task<IActionResult> GetInputTypes()
        {
            var types = await _inputTypes.GetAllAsync();
            return Ok(ApiResult.Ok(types));
        }

        [HttpPut("types/{type}")]
        [Authorize(Policy = AdminPermissionKeys.EditFormDefinitions)]
        public async Task<IActionResult> UpdateInputType(string type, [FromBody] FormInputTypeUpdateRequest request)
        {
            var updated = await _inputTypes.UpdateAsync(type, request);
            return updated is null
                ? NotFound(ApiResult.NotFound("Form input type not found."))
                : Ok(ApiResult.Ok(updated, "Form input type saved."));
        }

        [HttpDelete("definitions/{id}")]
        [Authorize(Policy = AdminPermissionKeys.EditFormDefinitions)]
        public async Task<IActionResult> DeleteDefinition(string id)
        {
            if (await _definitions.IsReferencedAsync(id))
                return BadRequest(ApiResult.BadRequest("This form is still used by one or more FormBlocks or buttons. Remove those usages before deleting it."));

            var ok = await _definitions.DeleteAsync(id);
            return ok
                ? Ok(ApiResult.Ok("Form definition deleted."))
                : NotFound(ApiResult.NotFound("Form definition not found."));
        }

        private static ManagedFormSubmissionResponse MapSubmission(
            Models.FormSubmission submission,
            IReadOnlyCollection<FormDefinition>? definitions = null) => new()
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
                    Order = field.Order,
                    IsDeletedField = IsDeletedField(submission, field, definitions)
                })
                .ToList(),
            InternalNotes = submission.InternalNotes,
            AssignedToAdminId = submission.AssignedToAdminId,
            AssignedToAdminName = submission.AssignedToAdminName,
            IsRead = submission.IsRead,
            ViewedAt = submission.ViewedAt,
            ViewedByAdminId = submission.ViewedByAdminId,
            Timeline = submission.Timeline
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new FormSubmissionTimelineEventResponse
                {
                    EventType = item.EventType,
                    Message = item.Message,
                    ActorId = item.ActorId,
                    ActorName = item.ActorName,
                    CreatedAt = item.CreatedAt
                })
                .ToList(),
            SubmittedAt = submission.SubmittedAt,
            UpdatedAt = submission.UpdatedAt
        };

        private static bool IsDeletedField(
            Models.FormSubmission submission,
            FormSubmissionFieldSnapshot field,
            IReadOnlyCollection<FormDefinition>? definitions)
        {
            if (definitions is null || definitions.Count == 0)
                return false;

            var definition = ResolveDefinitionForSubmission(submission, definitions);
            if (definition is null)
                return false;

            return !definition.Fields.Any(activeField =>
                string.Equals(activeField.Key, field.Key, StringComparison.OrdinalIgnoreCase));
        }

        private static FormDefinition? ResolveDefinitionForSubmission(
            Models.FormSubmission submission,
            IReadOnlyCollection<FormDefinition> definitions)
        {
            if (!string.IsNullOrWhiteSpace(submission.FormId))
            {
                var byId = definitions.FirstOrDefault(item =>
                    string.Equals(item.Id, submission.FormId, StringComparison.Ordinal));
                if (byId is not null)
                    return byId;
            }

            return definitions.FirstOrDefault(item =>
                string.Equals(item.Key, submission.FormKey, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<AdminUser?> CurrentAdminAsync()
        {
            var adminId = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminId)) return null;

            var admin = await _auth.GetByIdAsync(adminId);
            if (admin is not null) AuthService.NormalizeUserDefaults(admin);
            return admin;
        }

        private async Task<(string? Id, string? Name, string? Error)> ResolveAssigneeAsync(string? adminId)
        {
            if (string.IsNullOrWhiteSpace(adminId))
                return (null, null, null);

            var admin = await _auth.GetByIdAsync(adminId.Trim());
            if (admin is null)
                return (null, null, "Assigned admin user was not found.");

            AuthService.NormalizeUserDefaults(admin);
            if (admin.Status != AdminUserStatus.Active || admin.Role == AdminRole.Viewer)
                return (null, null, "Assigned admin user is not available for submissions.");

            return (admin.Id, DisplayName(admin), null);
        }

        private static string DisplayName(AdminUser user) =>
            string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
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
