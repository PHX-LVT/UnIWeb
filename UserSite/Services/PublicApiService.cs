using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contracts.Api;
using Contracts.Public;
using Contracts.Global;
using Contracts.Forms;


namespace UserSite.Services
{
    public class PublicApiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<PublicApiService> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new System.Text.Json.Serialization.JsonStringEnumConverter()
            }
        };
        public PublicApiService(HttpClient http, ILogger<PublicApiService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public Task<PublicOperationResult<List<PublicGlobalButton>>> GetGlobalButtonsAsync() =>
            GetResourceAsync<List<PublicGlobalButton>>("api/public/global-buttons", "global buttons");

        public Task<PublicOperationResult<PublicTheme>> GetThemeAsync() =>
            GetResourceAsync<PublicTheme>("api/public/theme", "theme");

        public Task<PublicOperationResult<PublicLanguageSettings>> GetLanguageSettingsAsync() =>
            GetResourceAsync<PublicLanguageSettings>("api/public/languages", "language settings");

        public Task<PublicOperationResult<PublicBranding>> GetBrandingAsync() =>
            GetResourceAsync<PublicBranding>("api/public/branding", "branding");

        public Task<PublicOperationResult<List<PublicNavItem>>> GetNavigationAsync() =>
            GetResourceAsync<List<PublicNavItem>>("api/public/navigation", "navigation");

        public Task<PublicOperationResult<PublicFooter>> GetFooterAsync() =>
            GetResourceAsync<PublicFooter>("api/public/footer", "footer");

        public async Task<PublicOperationResult<List<PublicSocialButton>>> GetSocialAsync()
        {
            var result = await GetResourceAsync<SocialGroupResponse>("api/public/social", "social buttons");
            return result.Status switch
            {
                PublicOperationStatus.Success => PublicOperationResult<List<PublicSocialButton>>.Succeeded(result.Data?.Buttons ?? []),
                PublicOperationStatus.NotConfigured => new PublicOperationResult<List<PublicSocialButton>>(PublicOperationStatus.NotConfigured, []),
                _ => PublicOperationResult<List<PublicSocialButton>>.Failed(result.Status, result.Message, result.CorrelationId)
            };
        }

        public Task<PublicOperationResult<PublicPageDto>> GetPageAsync(string slug) =>
            GetPageResourceAsync($"api/public/pages/{Uri.EscapeDataString(slug)}", $"page '{slug}'");

        public Task<PublicOperationResult<PublicPageDto>> GetChildPageAsync(string parentSlug, string childSlug) =>
            GetPageResourceAsync(
                $"api/public/pages/{Uri.EscapeDataString(parentSlug)}/{Uri.EscapeDataString(childSlug)}",
                $"child page '{parentSlug}/{childSlug}'");

        public Task<PublicOperationResult<PublicPageDto>> GetContentPageAsync(string typeKey, string slug) =>
            GetPageResourceAsync(
                $"api/public/content/{Uri.EscapeDataString(typeKey)}/{Uri.EscapeDataString(slug)}",
                $"content page '{typeKey}/{slug}'");

        public Task<PublicFormSubmitResult> SubmitFormAsync(
            string slug, string sectionId, string blockId,
            Dictionary<string, string> data,
            string language = "en") =>
            SubmitFormCoreAsync(
                $"api/public/pages/{Uri.EscapeDataString(slug)}/sections/{Uri.EscapeDataString(sectionId)}/blocks/{Uri.EscapeDataString(blockId)}/form/submit",
                data,
                language,
                slug,
                $"embedded form on '{slug}'");

        public Task<PublicFormSubmitResult> SubmitChildFormAsync(
             string parentSlug, string childSlug, string sectionId, string blockId,
            Dictionary<string, string> data,
            string language = "en") =>
            SubmitFormCoreAsync(
                $"api/public/pages/{Uri.EscapeDataString(parentSlug)}/{Uri.EscapeDataString(childSlug)}/sections/{Uri.EscapeDataString(sectionId)}/blocks/{Uri.EscapeDataString(blockId)}/form/submit",
                data,
                language,
                $"{parentSlug}/{childSlug}",
                $"embedded form on '{parentSlug}/{childSlug}'");

        public Task<PublicOperationResult<FormDefinitionResponse>> GetFormDefinitionByIdAsync(string id) =>
            GetResourceAsync<FormDefinitionResponse>(
                $"api/public/forms/by-id/{Uri.EscapeDataString(id)}",
                $"form definition '{id}'");

        public Task<PublicOperationResult<FormDefinitionResponse>> GetFormDefinitionByKeyAsync(string key) =>
            GetResourceAsync<FormDefinitionResponse>(
                $"api/public/forms/{Uri.EscapeDataString(key)}",
                $"form definition '{key}'");

        public Task<PublicFormSubmitResult> SubmitDefinitionFormAsync(
            string formKey,
            Dictionary<string, string> data,
            string language,
            string sourcePage) =>
            SubmitDefinitionFormCoreAsync(formKey, data, language, sourcePage);

        private async Task<PublicFormSubmitResult> SubmitDefinitionFormCoreAsync(
            string formKey,
            Dictionary<string, string> data,
            string language,
            string sourcePage)
        {
            var payload = CopySubmissionData(data, out var honeypot);
            return await SendSubmissionAsync(
                $"api/public/forms/{Uri.EscapeDataString(formKey)}/submit",
                new PublicFormSubmitRequest
                {
                    Data = payload,
                    Language = language,
                    SourcePage = sourcePage,
                    Honeypot = honeypot
                },
                $"form definition '{formKey}'");
        }

        private Task<PublicFormSubmitResult> SubmitFormCoreAsync(
            string path,
            Dictionary<string, string> data,
            string language,
            string sourcePage,
            string operation)
        {
            var payload = CopySubmissionData(data, out var honeypot);
            return SendSubmissionAsync(
                path,
                new { Data = payload, Language = language, SourcePage = sourcePage, Honeypot = honeypot },
                operation);
        }

        private async Task<PublicOperationResult<PublicPageDto>> GetPageResourceAsync(
            string path,
            string operation)
        {
            var response = await GetResourceAsync<JsonElement>(path, operation);
            if (!response.IsSuccess)
            {
                return PublicOperationResult<PublicPageDto>.Failed(
                    response.Status,
                    response.Message,
                    response.CorrelationId);
            }

            try
            {
                var page = MapPage(response.Data);
                if (page is not null)
                    return PublicOperationResult<PublicPageDto>.Succeeded(page);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "The public API returned an invalid payload for {Operation}.", operation);
            }

            return PublicOperationResult<PublicPageDto>.Failed(
                PublicOperationStatus.InvalidResponse,
                "The page data is temporarily unavailable.",
                response.CorrelationId);
        }

        private async Task<PublicOperationResult<T>> GetResourceAsync<T>(
            string path,
            string operation)
        {
            try
            {
                using var response = await _http.GetAsync(path);
                var correlationId = ReadCorrelationId(response);
                var payload = await ReadEnvelopeAsync<T>(response);

                if (!response.IsSuccessStatusCode)
                {
                    var status = MapStatus(response.StatusCode);
                    var message = SafeMessage(payload?.Message, status, operation);
                    _logger.LogWarning(
                        "Public API request for {Operation} failed with {StatusCode}. CorrelationId: {CorrelationId}",
                        operation,
                        (int)response.StatusCode,
                        correlationId);
                    return PublicOperationResult<T>.Failed(status, message, correlationId);
                }

                if (payload?.Success != true)
                {
                    _logger.LogError(
                        "Public API request for {Operation} returned an unsuccessful or invalid success envelope. CorrelationId: {CorrelationId}",
                        operation,
                        correlationId);
                    return PublicOperationResult<T>.Failed(
                        PublicOperationStatus.InvalidResponse,
                        $"The {operation} response is invalid.",
                        correlationId);
                }

                if (payload.Data is null)
                {
                    return new PublicOperationResult<T>(
                        PublicOperationStatus.NotConfigured,
                        default,
                        $"The {operation} has not been configured.",
                        correlationId);
                }

                return new PublicOperationResult<T>(
                    PublicOperationStatus.Success,
                    payload.Data,
                    payload.Message ?? string.Empty,
                    correlationId);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Public API request for {Operation} timed out.", operation);
                return PublicOperationResult<T>.Failed(
                    PublicOperationStatus.Timeout,
                    $"The {operation} request timed out. Please try again.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Public API request for {Operation} is unavailable.", operation);
                return PublicOperationResult<T>.Failed(
                    PublicOperationStatus.Unavailable,
                    $"The {operation} service is temporarily unavailable.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Public API request for {Operation} returned invalid JSON.", operation);
                return PublicOperationResult<T>.Failed(
                    PublicOperationStatus.InvalidResponse,
                    $"The {operation} response is invalid.");
            }
        }

        private async Task<PublicFormSubmitResult> SendSubmissionAsync(
            string path,
            object request,
            string operation)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(path, request, _json);
                var correlationId = ReadCorrelationId(response);
                var payload = await ReadEnvelopeAsync<JsonElement>(response);
                var status = MapStatus(response.StatusCode);

                if (response.IsSuccessStatusCode && payload?.Success != false)
                    return new PublicFormSubmitResult(PublicOperationStatus.Success, payload?.Message ?? string.Empty, correlationId);

                var message = SafeMessage(payload?.Message, status, operation);
                _logger.LogWarning(
                    "Public submission for {Operation} failed with {StatusCode}. CorrelationId: {CorrelationId}",
                    operation,
                    (int)response.StatusCode,
                    correlationId);
                return PublicFormSubmitResult.Rejected(status, message, correlationId);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Public submission for {Operation} timed out.", operation);
                return PublicFormSubmitResult.Rejected(
                    PublicOperationStatus.Timeout,
                    "The submission timed out. Your entries are still available; please try again.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Public submission service for {Operation} is unavailable.", operation);
                return PublicFormSubmitResult.Rejected(
                    PublicOperationStatus.Unavailable,
                    "The form service is temporarily unavailable. Your entries are still available; please try again.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Public submission for {Operation} returned invalid JSON.", operation);
                return PublicFormSubmitResult.Rejected(
                    PublicOperationStatus.InvalidResponse,
                    "The form service returned an invalid response. Your entries were not cleared.");
            }
        }

        private static Dictionary<string, string> CopySubmissionData(
            Dictionary<string, string> source,
            out string honeypot)
        {
            var payload = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
            honeypot = payload.GetValueOrDefault("__website") ?? string.Empty;
            payload.Remove("__website");
            return payload;
        }

        private static async Task<ApiResponse<T>?> ReadEnvelopeAsync<T>(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentLength == 0)
                return null;

            return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_json);
        }

        private static PublicOperationStatus MapStatus(HttpStatusCode statusCode) => statusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => PublicOperationStatus.ValidationFailed,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PublicOperationStatus.Unauthorized,
            HttpStatusCode.NotFound => PublicOperationStatus.NotFound,
            HttpStatusCode.TooManyRequests => PublicOperationStatus.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => PublicOperationStatus.Timeout,
            _ when (int)statusCode >= 500 => PublicOperationStatus.Unavailable,
            _ => PublicOperationStatus.InvalidResponse
        };

        private static string SafeMessage(
            string? apiMessage,
            PublicOperationStatus status,
            string operation)
        {
            if (!string.IsNullOrWhiteSpace(apiMessage) &&
                status is PublicOperationStatus.ValidationFailed or PublicOperationStatus.NotFound or PublicOperationStatus.RateLimited)
            {
                return apiMessage;
            }

            return status switch
            {
                PublicOperationStatus.NotFound => $"The {operation} was not found.",
                PublicOperationStatus.ValidationFailed => "Please review the submitted values and try again.",
                PublicOperationStatus.Unauthorized => "This request is not permitted.",
                PublicOperationStatus.RateLimited => "Too many requests were received. Please wait and try again.",
                PublicOperationStatus.Timeout => "The request timed out. Please try again.",
                PublicOperationStatus.Unavailable => "The service is temporarily unavailable. Please try again.",
                _ => "The service returned an invalid response."
            };
        }

        private static string? ReadCorrelationId(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-Correlation-ID", out var values))
                return values.FirstOrDefault();
            if (response.Headers.TryGetValues("traceparent", out values))
                return values.FirstOrDefault();
            return null;
        }

        // -- Private response wrappers -------------------------

     
        private class SocialGroupResponse
        {
            public bool GroupVisible { get; set; }
            public List<PublicSocialButton> Buttons { get; set; } = new();
        }

        public class PublicLanguageSettings
        {
            public List<PublicLanguageOption> Languages { get; set; } = new();
            public string DefaultLanguage { get; set; } = "en";
        }

        public class PublicLanguageOption
        {
            public string Slug { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string NativeName { get; set; } = string.Empty;
            public bool Active { get; set; }
            public bool UserEnabled { get; set; }
            public bool IsFallback { get; set; }
            public int Order { get; set; }
        }

        private static PublicPageDto? MapPage(JsonElement page)
        {
            if (page.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

            var dto = new PublicPageDto
            {
                Id = ReadString(page, "id"),
                Slug = ReadString(page, "slug"),
                FullSlug = ReadString(page, "fullSlug"),
                Name = ReadDictionary(page, "name")
            };

            if (page.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sections.EnumerateArray())
                {
                    var type = ReadString(section, "type");
                    PublicSectionDto? mapped = type switch
                    {
                        "hero" => DeserializeSectionShell<PublicHeroSectionDto>(section),
                        "cta" => DeserializeSectionShell<PublicCtaSectionDto>(section),
                        "list" => DeserializeSectionShell<PublicListSectionDto>(section),
                        "html" => DeserializeSectionShell<PublicHtmlSectionDto>(section),
                        "columns" => DeserializeSectionShell<PublicColumnsSectionDto>(section),
                        "showcase" => DeserializeSectionShell<PublicShowcaseSectionDto>(section),
                        "library" => DeserializeSectionShell<PublicLibrarySectionDto>(section),
                        "stats" => DeserializeSectionShell<PublicStatsSectionDto>(section),
                        "carousel" => DeserializeSectionShell<PublicCarouselSectionDto>(section),
                        "network-map" => DeserializeSectionShell<PublicNetworkMapSectionDto>(section),
                        "testimonial" => DeserializeSectionShell<PublicTestimonialSectionDto>(section),
                        "canvas" => DeserializeSectionShell<PublicCanvasSectionDto>(section),
                        _ => null
                    };

                    if (mapped is not null)
                    {
                        mapped.Blocks = MapBlocks(section, "blocks");

                        if (mapped is PublicColumnsSectionDto columns)
                            MapColumnSlotBlocks(section, columns);
                    }

                    if (mapped is not null && mapped.Visible)
                        dto.Sections.Add(mapped);
                }
            }

            return dto;
        }

        private static string ReadString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static Dictionary<string, string> ReadDictionary(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
                ? value.Deserialize<Dictionary<string, string>>(_json) ?? new()
                : new();

        private static T? DeserializeSectionShell<T>(JsonElement section)
            where T : PublicSectionDto
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteSectionShell(writer, section);
            }

            return JsonSerializer.Deserialize<T>(stream.ToArray(), _json);
        }

        private static void WriteSectionShell(Utf8JsonWriter writer, JsonElement section)
        {
            writer.WriteStartObject();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in section.EnumerateObject())
            {
                if (string.Equals(property.Name, "blocks", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!seen.Add(property.Name))
                    continue;

                if (string.Equals(property.Name, "columnSlots", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    writer.WritePropertyName(property.Name);
                    WriteColumnSlotsShell(writer, property.Value);
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        private static void WriteColumnSlotsShell(Utf8JsonWriter writer, JsonElement slots)
        {
            writer.WriteStartArray();

            foreach (var slot in slots.EnumerateArray())
            {
                if (slot.ValueKind != JsonValueKind.Object)
                {
                    slot.WriteTo(writer);
                    continue;
                }

                writer.WriteStartObject();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var property in slot.EnumerateObject())
                {
                    if (string.Equals(property.Name, "blocks", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (seen.Add(property.Name))
                        property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static List<PublicBlockDto> MapBlocks(JsonElement parent, string propertyName)
        {
            var blocks = new List<PublicBlockDto>();
            if (!parent.TryGetProperty(propertyName, out var blockArray) ||
                blockArray.ValueKind != JsonValueKind.Array)
            {
                return blocks;
            }

            var allBlocks = new List<PublicBlockDto>();
            foreach (var block in blockArray.EnumerateArray())
            {
                var mapped = MapBlock(block);
                if (mapped is not null && mapped.Visible)
                    allBlocks.Add(mapped);
            }

            return BuildBlockTree(allBlocks);
        }

        private static List<PublicBlockDto> BuildBlockTree(IEnumerable<PublicBlockDto> blocks, string? parentBlockId = null)
        {
            var blockList = blocks.ToList();
            var roots = blockList
                .Where(b => string.IsNullOrWhiteSpace(parentBlockId)
                    ? string.IsNullOrWhiteSpace(b.ParentBlockId)
                    : string.Equals(b.ParentBlockId, parentBlockId, StringComparison.Ordinal))
                .OrderBy(b => b.Order)
                .ToList();

            foreach (var container in roots.OfType<PublicContainerBlockDto>())
            {
                container.Children = BuildBlockTree(blockList, container.Id);
            }

            return roots;
        }

        private static void MapColumnSlotBlocks(JsonElement section, PublicColumnsSectionDto columns)
        {
            if (columns.ColumnSlots is null ||
                !section.TryGetProperty("columnSlots", out var slots) ||
                slots.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var slotsById = columns.ColumnSlots.ToDictionary(s => s.Id);
            foreach (var slotElement in slots.EnumerateArray())
            {
                var slotId = ReadString(slotElement, "id");
                if (slotId.Length == 0 || !slotsById.TryGetValue(slotId, out var slot))
                    continue;

                slot.Blocks = MapBlocks(slotElement, "blocks");
            }
        }

        private static PublicBlockDto? MapBlock(JsonElement block)
        {
            var type = ReadString(block, "type");
            return type switch
            {
                "text" => block.Deserialize<PublicTextBlockDto>(_json),
                "image" => block.Deserialize<PublicImageBlockDto>(_json),
                "video" => block.Deserialize<PublicVideoBlockDto>(_json),
                "file" => block.Deserialize<PublicFileBlockDto>(_json),
                "map" => block.Deserialize<PublicMapBlockDto>(_json),
                "form" => block.Deserialize<PublicFormBlockDto>(_json),
                "card" => block.Deserialize<PublicCardBlockDto>(_json),
                "button" => block.Deserialize<PublicButtonBlockDto>(_json),
                "metric" => block.Deserialize<PublicMetricBlockDto>(_json),
                "bullet-list" => block.Deserialize<PublicBulletListBlockDto>(_json),
                "step" => block.Deserialize<PublicStepBlockDto>(_json),
                "icon" => block.Deserialize<PublicIconBlockDto>(_json),
                "container" => block.Deserialize<PublicContainerBlockDto>(_json),
                _ => null
            };
        }
    }
}
