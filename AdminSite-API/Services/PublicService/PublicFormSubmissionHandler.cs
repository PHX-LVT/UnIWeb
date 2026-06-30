using FullProject.DTOs;
using FullProject.Models;
using FullProject.Utils;
using FullProject.Services.SectionServices;
using FullProject.Security.Forms;
using Contracts.Forms;
using FullProject.Services.FormServices;
using FullProject.Services.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Services.PublicService
{
    public class PublicFormSubmissionHandler
    {
        private readonly PageService _pageService;
        private readonly SectionService _sectionService;
        private readonly BlockService _blockService;
        private readonly FormSubmissionService _submissionService;
        private readonly FormSubmissionSecurityService _formSecurity;
        private readonly FormDefinitionService _formDefinitionService;
        private readonly FormInputTypeService _formInputTypes;
        private readonly VisitorMetricService _metrics;
        private readonly IConfiguration _configuration;

        public PublicFormSubmissionHandler(
            PageService pageService,
            SectionService sectionService,
            BlockService blockService,
            FormSubmissionService submissionService,
            FormSubmissionSecurityService formSecurity,
            FormDefinitionService formDefinitionService,
            FormInputTypeService formInputTypes,
            VisitorMetricService metrics,
            IConfiguration configuration)
        {
            _pageService = pageService;
            _sectionService = sectionService;
            _blockService = blockService;
            _submissionService = submissionService;
            _formSecurity = formSecurity;
            _formDefinitionService = formDefinitionService;
            _formInputTypes = formInputTypes;
            _metrics = metrics;
            _configuration = configuration;
        }

        public async Task<IActionResult> SubmitPageFormAsync(
            string slug,
            string sectionId,
            string blockId,
            FormSubmitDto dto)
        {
            var page = await _pageService.GetByFullSlugAsync(slug);
            return await SubmitForPageAsync(page, sectionId, blockId, dto);
        }

        public async Task<IActionResult> SubmitChildPageFormAsync(
            string parentSlug,
            string childSlug,
            string sectionId,
            string blockId,
            FormSubmitDto dto)
        {
            var fullSlug = $"{parentSlug}/{childSlug}";
            var page = await _pageService.GetByFullSlugAsync(fullSlug);
            return await SubmitForPageAsync(page, sectionId, blockId, dto);
        }

        public async Task<IActionResult> SubmitModalFormAsync(string modalType, FormSubmitDto dto)
        {
            var normalizedType = NormalizeModalType(modalType);
            if (normalizedType is null)
                return new NotFoundObjectResult(ApiResult.NotFound("Form not found."));

            if (normalizedType != "sync")
                return new NotFoundObjectResult(ApiResult.NotFound("Form not found."));

            if (!SyncHubDemoEnabled())
                return new NotFoundObjectResult(ApiResult.NotFound("Form not found."));

            var validation = ValidateModalSubmission(normalizedType, dto);
            if (validation is not null) return validation;

            return await SubmitSyncHubLoginAsync(dto);
        }

        private async Task<IActionResult> SubmitForPageAsync(
            Page? page,
            string sectionId,
            string blockId,
            FormSubmitDto dto)
        {
            if (page is null || page.Status != PageStatus.Published)
                return new NotFoundObjectResult(ApiResult.NotFound("Page not found."));

            var block = await _blockService.GetPublicByIdAsync(page.StableId, blockId);
            if (block is null || block is not FormBlock form)
                return new NotFoundObjectResult(ApiResult.NotFound("Form not found."));

            var validation = await ValidateFormSubmissionAsync(page, sectionId, form, dto);
            if (validation is not null) return validation;

            var definition = string.IsNullOrWhiteSpace(form.FormDefinitionId)
                ? null
                : await _formDefinitionService.GetActiveByIdAsync(form.FormDefinitionId);
            if (!string.IsNullOrWhiteSpace(form.FormDefinitionId) && definition is null)
                return new NotFoundObjectResult(ApiResult.NotFound("Form definition is unavailable."));

            var inputTypeCapabilities = await _formInputTypes.GetCapabilityLookupAsync();

            var fields = definition is not null
                ? definition.Fields.OrderBy(field => field.Order).Select(field => new SubmissionField(
                    field.Key,
                    field.Type,
                    field.Label,
                    field.Required,
                    SubmissionMaximumLength(field.Type, field.MaxLength, inputTypeCapabilities),
                    field.Options.Select(option => option.Value).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    field.Order)).ToList()
                : form.Fields.OrderBy(field => field.Order).Select(field => new SubmissionField(
                    field.Name,
                    field.Type,
                    field.Label,
                    field.Required,
                    SubmissionMaximumLength(field.Type, 0, inputTypeCapabilities),
                    field.Options?.ToHashSet(StringComparer.OrdinalIgnoreCase),
                    field.Order)).ToList();

            var securityInput = new Dictionary<string, string>(dto.Data, StringComparer.OrdinalIgnoreCase)
            {
                ["__website"] = dto.Honeypot ?? string.Empty
            };
            var rules = fields.Select(field => new FormFieldValidationRule(
                field.Key,
                field.Type,
                field.Required,
                field.MaximumLength,
                field.AllowedValues));
            var security = await _formSecurity.ValidateAsync($"block:{blockId}", securityInput, rules);
            if (!security.Accepted) return SecurityFailure(security);

            var language = NormalizeLanguage(dto.Language);

            await _submissionService.CreateAsync(new FormSubmission
            {
                FormId = definition?.Id ?? string.Empty,
                PageId = page.Id,
                SectionId = sectionId,
                BlockId = blockId,
                FormKey = definition?.Key ?? $"block:{blockId}",
                FormName = definition is null
                    ? "Page Form"
                    : Localized(definition.Name, language, definition.Key),
                Language = language,
                SourcePage = page.FullSlug ?? page.Slug,
                Status = FormSubmissionStatus.New,
                Fields = fields
                    .Where(field => security.Data.ContainsKey(field.Key))
                    .Select(field => new FormSubmissionFieldSnapshot
                    {
                        Key = field.Key,
                        Label = Localized(field.Label, language, field.Key),
                        Type = field.Type,
                        Value = security.Data[field.Key],
                        Order = field.Order
                    })
                    .ToList(),
                Security = ToSecurityModel(security),
                Data = security.Data,
                SubmittedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _metrics.IncrementAsync(VisitorMetricService.FormSubmission, "form", definition?.Key ?? $"block:{blockId}", page.FullSlug ?? page.Slug);

            return new OkObjectResult(ApiResult.Ok("Form submitted successfully."));
        }

        private async Task<IActionResult> SubmitSyncHubLoginAsync(FormSubmitDto dto)
        {
            var username = dto.Data.TryGetValue("Username", out var usernameValue)
                ? usernameValue.Trim()
                : string.Empty;
            var password = dto.Data.TryGetValue("Password", out var passwordValue)
                ? passwordValue
                : string.Empty;
            var expectedUsername = _configuration["PublicDemoForms:SyncHubUsername"]?.Trim();
            var expectedPassword = _configuration["PublicDemoForms:SyncHubPassword"];
            var redirectUrl = _configuration["PublicDemoForms:SyncHubRedirectUrl"]?.Trim();
            if (string.IsNullOrWhiteSpace(redirectUrl))
                redirectUrl = "/home";

            var success = !string.IsNullOrWhiteSpace(expectedUsername) &&
                          !string.IsNullOrEmpty(expectedPassword) &&
                          string.Equals(username, expectedUsername, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(password, expectedPassword, StringComparison.Ordinal);

            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FormType"] = "sync",
                ["Username"] = username,
                ["LoginResult"] = success ? "Success" : "Denied",
                ["PasswordProvided"] = string.IsNullOrWhiteSpace(password) ? "false" : "true"
            };

            await _submissionService.CreateAsync(new FormSubmission
            {
                PageId = "modal:sync",
                SectionId = "hardcoded-modal",
                BlockId = "sync-login",
                Data = data,
                SubmittedAt = DateTime.UtcNow
            });

            if (!success)
            {
                return new UnauthorizedObjectResult(ApiResult.Unauthorized<object>("Invalid username or password."));
            }

            return new OkObjectResult(ApiResult.Ok(new { RedirectUrl = redirectUrl }, "Login successful."));
        }

        private bool SyncHubDemoEnabled() =>
            _configuration.GetValue<bool>("PublicDemoForms:SyncHubEnabled");

        private async Task<IActionResult?> ValidateFormSubmissionAsync(
            Page page,
            string sectionId,
            FormBlock form,
            FormSubmitDto dto)
        {
            dto.Data ??= new();

            var sections = await _sectionService.GetPublicSectionsByPageAsync(page.StableId);
            var section = sections.FirstOrDefault(s => s.Id == sectionId && s.Visible);
            if (section is null || section.StableId != form.SectionStableId)
                return new NotFoundObjectResult(ApiResult.NotFound("Form not found."));

            return null;
        }

        private static string? NormalizeModalType(string modalType)
        {
            var type = modalType.Trim().ToLowerInvariant();
            return type switch
            {
                "sync" => "sync",
                "synchub" => "sync",
                _ => null
            };
        }

        private static IActionResult? ValidateModalSubmission(string normalizedType, FormSubmitDto dto)
        {
            dto.Data ??= new();

            if (dto.Data.Count > 12)
                return new BadRequestObjectResult(ApiResult.BadRequest("Too many form fields."));

            var required = new[] { "Username", "Password" };

            foreach (var field in required)
            {
                if (!dto.Data.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    return new BadRequestObjectResult(ApiResult.BadRequest("Required form field missing."));
                }
            }

            if (dto.Data.Keys.Any(k => string.IsNullOrWhiteSpace(k) || k.Length > 100))
                return new BadRequestObjectResult(ApiResult.BadRequest("Invalid form field."));

            if (dto.Data.Values.Any(v => v is not null && v.Length > 2000))
                return new BadRequestObjectResult(ApiResult.BadRequest("Field value too long."));

            var totalLength = dto.Data.Sum(kv => kv.Key.Length + (kv.Value?.Length ?? 0));
            if (totalLength > 8_000)
                return new BadRequestObjectResult(ApiResult.BadRequest("Form payload too large."));

            if (dto.Data.TryGetValue("Email", out var emailValue) &&
                !string.IsNullOrWhiteSpace(emailValue) &&
                !IsReasonableEmail(emailValue))
            {
                return new BadRequestObjectResult(ApiResult.BadRequest("Invalid email address."));
            }

            if (dto.Data.TryGetValue("Username", out var usernameValue) &&
                usernameValue.Length > 254)
            {
                return new BadRequestObjectResult(ApiResult.BadRequest("Invalid username."));
            }

            return null;
        }

        private static FormSubmissionSecurity ToSecurityModel(FormSecurityResult result) => new()
        {
            IpAddress = result.IpAddress,
            UserAgent = result.UserAgent,
            Fingerprint = result.Fingerprint
        };

        private static IActionResult SecurityFailure(FormSecurityResult result) =>
            result.StatusCode switch
            {
                StatusCodes.Status409Conflict => new ConflictObjectResult(ApiResult.BadRequest(result.Message)),
                StatusCodes.Status429TooManyRequests => new ObjectResult(ApiResult.BadRequest(result.Message)) { StatusCode = StatusCodes.Status429TooManyRequests },
                _ => new BadRequestObjectResult(ApiResult.BadRequest(result.Message))
            };

        private static int SubmissionMaximumLength(
            string? type,
            int maxLength,
            IReadOnlyDictionary<string, FormInputTypeCapability> capabilities)
        {
            var capability = FormInputTypeService.Capability(type, capabilities);
            if (capability.SupportsMaxCharacters)
                return FormInputTypeCatalog.MaximumInputLength(capability);

            return FormInputTypeCatalog.NormalizeType(type) switch
            {
                "checkbox" => 10,
                "date" => 80,
                "number" => 80,
                "select" => 500,
                _ => 500
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            var normalized = language?.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 12 ? "en" : normalized;
        }

        private static string Localized(IReadOnlyDictionary<string, string> values, string language, string fallback) =>
            values.GetValueOrDefault(language) is { Length: > 0 } localized
                ? localized
                : values.GetValueOrDefault("en") is { Length: > 0 } english
                    ? english
                    : fallback;

        private static bool IsReasonableEmail(string email)
        {
            var trimmed = email.Trim();
            return trimmed.Length <= 254 && trimmed.Contains('@') && trimmed.Contains('.');
        }

        private sealed record SubmissionField(
            string Key,
            string Type,
            Dictionary<string, string> Label,
            bool Required,
            int MaximumLength,
            IReadOnlySet<string>? AllowedValues,
            int Order);
    }
}
