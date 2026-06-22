using System.Net.Http.Json;
using System.Text.Json;
using Contracts.Api;
using Contracts.Public;
using Contracts.Global;


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

        public async Task<List<PublicGlobalButton>> GetGlobalButtonsAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<List<PublicGlobalButton>>>(
                "api/public/global-buttons", _json);
            return r?.Data ?? new();
        }
        public async Task<PublicTheme> GetThemeAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<PublicTheme>>(
                "api/public/theme", _json);
            return r?.Data ?? new PublicTheme();
        }

        public async Task<PublicLanguageSettings> GetLanguageSettingsAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<PublicLanguageSettings>>(
                "api/public/languages", _json);
            return r?.Data ?? new PublicLanguageSettings();
        }

        public async Task<PublicBranding> GetBrandingAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<PublicBranding>>(
                "api/public/branding", _json);
            return r?.Data ?? new PublicBranding();
        }

        public async Task<List<PublicNavItem>> GetNavigationAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<List<PublicNavItem>>>(
                "api/public/navigation", _json);
            return r?.Data ?? new();
        }

        public async Task<PublicFooter> GetFooterAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<PublicFooter>>(
                "api/public/footer", _json);
            return r?.Data ?? new PublicFooter();
        }
       

        public async Task<List<PublicSocialButton>> GetSocialAsync()
        {
            var r = await _http.GetFromJsonAsync<ApiResponse<SocialGroupResponse>>(
                "api/public/social", _json);
            return r?.Data?.Buttons ?? new();
        }

        public async Task<PublicPageDto?> GetPageAsync(string slug)
        {
            try
            {
                var r = await _http.GetFromJsonAsync<ApiResponse<JsonElement>>(
                    $"api/public/pages/{slug}", _json);
                return r is null ? null : MapPage(r.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load public page for slug {Slug}.", slug);
                return null;
            }
        }

        public async Task<PublicPageDto?> GetChildPageAsync(string parentSlug, string childSlug)
        {
            try
            {
                var r = await _http.GetFromJsonAsync<ApiResponse<JsonElement>>(
                    $"api/public/pages/{parentSlug}/{childSlug}", _json);
                return r is null ? null : MapPage(r.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load public child page for slug {ParentSlug}/{ChildSlug}.",
                    parentSlug,
                    childSlug);
                return null;
            }
        }

        public async Task<PublicPageDto?> GetContentPageAsync(string typeKey, string slug)
        {
            try
            {
                var r = await _http.GetFromJsonAsync<ApiResponse<JsonElement>>(
                    $"api/public/content/{typeKey}/{slug}", _json);
                return r is null ? null : MapPage(r.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load public content page for {TypeKey}/{Slug}.",
                    typeKey,
                    slug);
                return null;
            }
        }

        public async Task<bool> SubmitFormAsync(
            string slug, string sectionId, string blockId,
            Dictionary<string, string> data,
            string language = "en")
        {
            try
            {
                var honeypot = data.GetValueOrDefault("__website") ?? string.Empty;
                data.Remove("__website");
                var res = await _http.PostAsJsonAsync(
                    $"api/public/pages/{slug}/sections/{sectionId}/blocks/{blockId}/form/submit",
                    new { Data = data, Language = language, SourcePage = slug, Honeypot = honeypot });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }
        public async Task<bool> SubmitChildFormAsync(
             string parentSlug, string childSlug, string sectionId, string blockId,
            Dictionary<string, string> data,
            string language = "en")
        {
            try
            {
                var honeypot = data.GetValueOrDefault("__website") ?? string.Empty;
                data.Remove("__website");
                var res = await _http.PostAsJsonAsync(
                    $"api/public/pages/{parentSlug}/{childSlug}/sections/{sectionId}/blocks/{blockId}/form/submit",
                    new { Data = data, Language = language, SourcePage = $"{parentSlug}/{childSlug}", Honeypot = honeypot });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
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
                        "gallery" => DeserializeSectionShell<PublicGallerySectionDto>(section),
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
