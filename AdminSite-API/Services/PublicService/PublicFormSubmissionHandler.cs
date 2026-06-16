using FullProject.DTOs;
using FullProject.Models;
using FullProject.SectionServices;
using FullProject.Utils;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Services.PublicService
{
    public class PublicFormSubmissionHandler
    {
        private const string SyncHubUsername = "user@yoursite.com";
        private const string SyncHubPassword = "hello123";
        private const string SyncHubRedirectUrl = "/home";

        private readonly PageService _pageService;
        private readonly SectionService _sectionService;
        private readonly BlockService _blockService;
        private readonly FormSubmissionService _submissionService;

        public PublicFormSubmissionHandler(
            PageService pageService,
            SectionService sectionService,
            BlockService blockService,
            FormSubmissionService submissionService)
        {
            _pageService = pageService;
            _sectionService = sectionService;
            _blockService = blockService;
            _submissionService = submissionService;
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

            var validation = ValidateModalSubmission(normalizedType, dto);
            if (validation is not null) return validation;

            if (normalizedType == "sync")
            {
                return await SubmitSyncHubLoginAsync(dto);
            }

            var data = BuildModalSubmissionData(normalizedType, dto.Data);

            await _submissionService.CreateAsync(new FormSubmission
            {
                PageId = $"modal:{normalizedType}",
                SectionId = "hardcoded-modal",
                BlockId = $"{normalizedType}-modal",
                Data = data,
                SubmittedAt = DateTime.UtcNow
            });

            return new OkObjectResult(ApiResult.Ok("Form submitted successfully."));
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

            await _submissionService.CreateAsync(new FormSubmission
            {
                PageId = page.Id,
                SectionId = sectionId,
                BlockId = blockId,
                Data = dto.Data,
                SubmittedAt = DateTime.UtcNow
            });

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
            var success = string.Equals(username, SyncHubUsername, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(password, SyncHubPassword, StringComparison.Ordinal);

            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FormType"] = "sync",
                ["Username"] = username,
                ["LoginResult"] = success ? "Success" : "Denied",
                ["PasswordProvided"] = string.IsNullOrWhiteSpace(password) ? "false" : "true"
            };

            if (dto.Data.TryGetValue("PageUrl", out var pageUrl) && !string.IsNullOrWhiteSpace(pageUrl))
            {
                data["PageUrl"] = pageUrl.Trim();
            }

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

            return new OkObjectResult(ApiResult.Ok(new { RedirectUrl = SyncHubRedirectUrl }, "Login successful."));
        }

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

            if (dto.Data.Count > 50)
                return new BadRequestObjectResult(ApiResult.BadRequest("Too many form fields."));

            var validFields = form.Fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .ToDictionary(f => f.Name, f => f);

            if (dto.Data.Keys.Any(k => string.IsNullOrWhiteSpace(k) ||
                                       k.Length > 100 ||
                                       !validFields.ContainsKey(k)))
            {
                return new BadRequestObjectResult(ApiResult.BadRequest("Invalid form field."));
            }

            if (dto.Data.Values.Any(v => v is not null && v.Length > 2000))
                return new BadRequestObjectResult(ApiResult.BadRequest("Field value too long."));

            var totalLength = dto.Data.Sum(kv => kv.Key.Length + (kv.Value?.Length ?? 0));
            if (totalLength > 20_000)
                return new BadRequestObjectResult(ApiResult.BadRequest("Form payload too large."));

            var missingRequired = form.Fields.Any(f =>
                f.Required &&
                (!dto.Data.TryGetValue(f.Name, out var value) ||
                 string.IsNullOrWhiteSpace(value)));

            if (missingRequired)
                return new BadRequestObjectResult(ApiResult.BadRequest("Required form field missing."));

            return null;
        }

        private static string? NormalizeModalType(string modalType)
        {
            var type = modalType.Trim().ToLowerInvariant();
            return type switch
            {
                "quote" => "quote",
                "expert" => "expert",
                "sync" => "sync",
                "synchub" => "sync",
                "contact" => "expert",
                _ => null
            };
        }

        private static IActionResult? ValidateModalSubmission(string normalizedType, FormSubmitDto dto)
        {
            dto.Data ??= new();

            if (dto.Data.Count > 12)
                return new BadRequestObjectResult(ApiResult.BadRequest("Too many form fields."));

            var required = normalizedType switch
            {
                "quote" => new[] { "ServiceType", "Route", "Email", "Phone" },
                "sync" => new[] { "Username", "Password" },
                _ => new[] { "Name", "Email", "Phone", "Service" }
            };

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

        private static Dictionary<string, string> BuildModalSubmissionData(
            string normalizedType,
            Dictionary<string, string> input)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FormType"] = normalizedType
            };

            foreach (var field in input)
            {
                if (string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Value))
                    continue;

                if (string.Equals(field.Key, "Password", StringComparison.OrdinalIgnoreCase))
                {
                    data["PasswordProvided"] = "true";
                    continue;
                }

                data[field.Key] = field.Value.Trim();
            }

            return data;
        }

        private static bool IsReasonableEmail(string email)
        {
            var trimmed = email.Trim();
            return trimmed.Length <= 254 && trimmed.Contains('@') && trimmed.Contains('.');
        }
    }
}
