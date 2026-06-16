using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class SettingsService
    {
        private readonly IMongoCollection<SiteSettings> _settings;
        private readonly IMongoCollection<GlossaryTerm> _glossary;

        public SettingsService(IMongoDatabase db)
        {
            _settings = db.GetCollection<SiteSettings>("settings");
            _glossary = db.GetCollection<GlossaryTerm>("glossary");
        }

        public async Task<SiteSettings> GetAsync()
        {
            var settings = await _settings.Find(_ => true).FirstOrDefaultAsync();
            if (settings is null)
            {
                settings = Normalize(new SiteSettings());
                await _settings.InsertOneAsync(settings);
            }
            else
            {
                var previousSignature = LanguageSignature(settings);
                var previousDefaultLanguage = settings.DefaultLanguage;
                var previousVersion = settings.LanguageRegistryVersion;
                var normalized = Normalize(settings);
                if (LanguageSignature(normalized) != previousSignature ||
                    normalized.DefaultLanguage != previousDefaultLanguage ||
                    normalized.LanguageRegistryVersion != previousVersion)
                {
                    await _settings.ReplaceOneAsync(s => s.Id == settings.Id, normalized);
                    settings = normalized;
                }
            }

            return settings;
        }

        public async Task UpdateAsync(SiteSettingsUpdateDto dto)
        {
            var settings = await GetAsync();
            var updates = new List<UpdateDefinition<SiteSettings>>();

            if (dto.Languages != null)
            {
                var fallback = NormalizeCode(dto.DefaultLanguage ?? settings.DefaultLanguage);
                var orderedInput = ApplySystemLanguageOrder(dto.Languages, settings.Languages);
                var langs = NormalizeLanguages(orderedInput, fallback);

                if (!langs.Any(l => string.Equals(l.Slug, fallback, StringComparison.OrdinalIgnoreCase)))
                {
                    fallback = "en";
                    langs = NormalizeLanguages(orderedInput, fallback);
                }

                updates.Add(Builders<SiteSettings>.Update.Set(s => s.Languages, langs));
                updates.Add(Builders<SiteSettings>.Update.Set(s => s.DefaultLanguage, fallback));
                updates.Add(Builders<SiteSettings>.Update.Set(s => s.LanguageRegistryVersion, 1));
            }
            else if (dto.DefaultLanguage != null)
            {
                var fallback = NormalizeCode(dto.DefaultLanguage);
                if (settings.Languages.Any(l => string.Equals(l.Slug, fallback, StringComparison.OrdinalIgnoreCase)))
                    updates.Add(Builders<SiteSettings>.Update.Set(s => s.DefaultLanguage, fallback));
            }

            if (updates.Any())
                await _settings.UpdateOneAsync(s => s.Id == settings.Id,
                    Builders<SiteSettings>.Update.Combine(updates));
        }

        private static SiteSettings Normalize(SiteSettings settings)
        {
            settings.DefaultLanguage = NormalizeCode(settings.DefaultLanguage);
            settings.Languages ??= new SiteSettings().Languages;
            if (settings.LanguageRegistryVersion <= 0)
            {
                foreach (var lang in settings.Languages.Where(l =>
                    l.Slug is "en" or "vi" or "cn"))
                {
                    lang.Active = true;
                    lang.AdminEnabled = true;
                    lang.UserEnabled = true;
                }
            }

            settings.Languages = NormalizeLanguages(
                settings.Languages.Select(l => new LanguageCreateDto
                {
                    Slug = l.Slug,
                    Label = l.Label,
                    NativeName = l.NativeName,
                    Active = l.Active,
                    AdminEnabled = l.AdminEnabled,
                    UserEnabled = l.UserEnabled,
                    Direction = l.Direction,
                    Order = l.Order
                }).ToList(),
                settings.DefaultLanguage);

            if (!settings.Languages.Any(l => l.Slug == settings.DefaultLanguage))
                settings.DefaultLanguage = settings.Languages.First(l => l.Active).Slug;

            settings.AdminAppearancePreset = NormalizeAdminAppearancePreset(settings.AdminAppearancePreset);
            settings.LanguageRegistryVersion = 1;
            return settings;
        }

        public async Task<string> GetAdminAppearancePresetAsync()
        {
            var settings = await GetAsync();
            return NormalizeAdminAppearancePreset(settings.AdminAppearancePreset);
        }

        public async Task<string> UpdateAdminAppearancePresetAsync(string? preset)
        {
            var settings = await GetAsync();
            var normalized = NormalizeAdminAppearancePreset(preset);
            await _settings.UpdateOneAsync(s => s.Id == settings.Id,
                Builders<SiteSettings>.Update.Set(s => s.AdminAppearancePreset, normalized));
            return normalized;
        }

        private static string NormalizeAdminAppearancePreset(string? preset)
        {
            preset = (preset ?? "navy-gold").Trim().ToLowerInvariant();
            return preset switch
            {
                "navy-gold" => "navy-gold",
                "granite" => "granite",
                "cloud-mono" => "cloud-mono",
                "harbor-teal" => "harbor-teal",
                "burgundy-ivory" => "burgundy-ivory",
                _ => "navy-gold"
            };
        }

        private static List<LanguageSetting> NormalizeLanguages(List<LanguageCreateDto> source, string fallback)
        {
            if (source.Count == 0)
                source = new SiteSettings().Languages.Select(l => new LanguageCreateDto
                {
                    Slug = l.Slug,
                    Label = l.Label,
                    NativeName = l.NativeName,
                    Active = l.Active,
                    AdminEnabled = l.AdminEnabled,
                    UserEnabled = l.UserEnabled,
                    Direction = l.Direction,
                    Order = l.Order
                }).ToList();

            var result = new List<LanguageSetting>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in source)
            {
                var code = NormalizeCode(item.Slug);
                if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                    continue;

                var isFallback = string.Equals(code, fallback, StringComparison.OrdinalIgnoreCase);
                result.Add(new LanguageSetting
                {
                    Slug = code,
                    Label = string.IsNullOrWhiteSpace(item.Label) ? code.ToUpperInvariant() : item.Label.Trim(),
                    NativeName = string.IsNullOrWhiteSpace(item.NativeName)
                        ? (string.IsNullOrWhiteSpace(item.Label) ? code.ToUpperInvariant() : item.Label.Trim())
                        : item.NativeName.Trim(),
                    Active = isFallback || item.Active,
                    AdminEnabled = isFallback || item.AdminEnabled,
                    UserEnabled = isFallback || item.UserEnabled,
                    Direction = string.Equals(item.Direction, "rtl", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr",
                    Order = item.Order <= 0 ? result.Count + 1 : item.Order
                });
            }

            if (!result.Any(l => l.Slug == "en"))
            {
                result.Insert(0, new LanguageSetting
                {
                    Slug = "en",
                    Label = "English",
                    NativeName = "English",
                    Active = fallback == "en",
                    AdminEnabled = fallback == "en",
                    UserEnabled = fallback == "en",
                    Direction = "ltr",
                    Order = 1
                });
            }

            if (!result.Any(l => l.Active))
                result.First().Active = result.First().AdminEnabled = result.First().UserEnabled = true;

            return ReassignLanguageOrders(result);
        }

        private static List<LanguageCreateDto> ApplySystemLanguageOrder(
            List<LanguageCreateDto> source,
            List<LanguageSetting> existing)
        {
            var result = new List<LanguageCreateDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var order = 1;

            foreach (var item in source)
            {
                var code = NormalizeCode(item.Slug);
                if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                    continue;

                result.Add(new LanguageCreateDto
                {
                    Slug = item.Slug,
                    Label = item.Label,
                    NativeName = item.NativeName,
                    Active = item.Active,
                    AdminEnabled = item.AdminEnabled,
                    UserEnabled = item.UserEnabled,
                    Direction = item.Direction,
                    Order = order++
                });
            }

            return result;
        }

        private static List<LanguageSetting> ReassignLanguageOrders(List<LanguageSetting> languages)
        {
            var ordered = languages
                .OrderBy(l => l.Active ? 0 : 1)
                .ThenBy(l => l.Order <= 0 ? int.MaxValue : l.Order)
                .ThenBy(l => l.Label)
                .ThenBy(l => l.Slug)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Order = i + 1;

            return ordered;
        }

        private static string NormalizeCode(string? code)
        {
            code = (code ?? "en").Trim().ToLowerInvariant();
            return string.Concat(code.Where(c => char.IsLetterOrDigit(c) || c == '-'));
        }

        private static string LanguageSignature(SiteSettings settings) =>
            string.Join("|", settings.Languages
                .OrderBy(l => l.Order)
                .Select(l => $"{l.Slug}:{l.Label}:{l.NativeName}:{l.Active}:{l.AdminEnabled}:{l.UserEnabled}:{l.Direction}:{l.Order}"));


        // ── Glossary ──────────────────────────────────────────

        public async Task<List<GlossaryTerm>> GetGlossaryAsync() =>
            await _glossary.Find(_ => true).SortBy(t => t.Term).ToListAsync();

        public async Task<GlossaryTerm> CreateTermAsync(GlossaryTermCreateDto dto)
        {
            var term = new GlossaryTerm
            {
                Term = dto.Term,
                Description = dto.Description
            };
            await _glossary.InsertOneAsync(term);
            return term;
        }

        public async Task<bool> UpdateTermAsync(string termId, GlossaryTermUpdateDto dto)
        {
            var updates = new List<UpdateDefinition<GlossaryTerm>>();
            if (dto.Term != null) updates.Add(Builders<GlossaryTerm>.Update.Set(t => t.Term, dto.Term));
            if (dto.Description != null) updates.Add(Builders<GlossaryTerm>.Update.Set(t => t.Description, dto.Description));
            if (!updates.Any()) return true;

            var result = await _glossary.UpdateOneAsync(t => t.Id == termId,
                Builders<GlossaryTerm>.Update.Combine(updates));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteTermAsync(string termId)
        {
            var result = await _glossary.DeleteOneAsync(t => t.Id == termId);
            return result.DeletedCount > 0;
        }
    }
}
