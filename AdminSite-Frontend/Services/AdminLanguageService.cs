using Blazored.LocalStorage;

namespace AdminSite.Services
{
    public class AdminLanguageService
    {
        private const string StorageKey = "admin-lang";
        private readonly ILocalStorageService _storage;
        private readonly AdminSettingsService _settings;
        private string? _lang;
        private string _fallbackLanguage = "en";
        private List<LanguageOption> _supportedLanguages = new()
        {
            new("en", "English", "English"),
            new("vi", "Vietnamese", "Tiếng Việt"),
            new("cn", "Chinese", "中文")
        };

        public AdminLanguageService(ILocalStorageService storage, AdminSettingsService settings)
        {
            _storage = storage;
            _settings = settings;
        }

        public event Action? Changed;

        public IReadOnlyList<string> SupportedLanguages => _supportedLanguages.Select(l => l.Code).ToList();
        public IReadOnlyList<LanguageOption> SupportedLanguageOptions => _supportedLanguages;
        public string FallbackLanguage => _fallbackLanguage;

        public async Task<string> GetAsync()
        {
            await LoadSettingsAsync();
            _lang ??= Normalize(await _storage.GetItemAsync<string>(StorageKey));
            return _lang;
        }

        public async Task SetAsync(string lang)
        {
            await LoadSettingsAsync();
            var normalized = Normalize(lang);
            if (_lang == normalized) return;

            _lang = normalized;
            await _storage.SetItemAsync(StorageKey, normalized);
            Changed?.Invoke();
        }

        public async Task LoadSettingsAsync()
        {
            var res = await _settings.GetLanguagesAsync();
            var settings = res.Data;
            if (settings is null) return;

            _fallbackLanguage = string.IsNullOrWhiteSpace(settings.DefaultLanguage)
                ? "en"
                : settings.DefaultLanguage;

            var options = settings.Languages
                .Where(l => l.Active && l.AdminEnabled)
                .OrderBy(l => l.Order)
                .Select(l => new LanguageOption(
                    l.Slug,
                    string.IsNullOrWhiteSpace(l.Name) ? l.Slug.ToUpperInvariant() : l.Name!,
                    string.IsNullOrWhiteSpace(l.NativeName) ? (l.Name ?? l.Slug.ToUpperInvariant()) : l.NativeName!))
                .ToList();

            if (options.Count > 0)
                _supportedLanguages = options;

            _lang = Normalize(_lang);
        }

        private string Normalize(string? lang)
        {
            var match = _supportedLanguages.FirstOrDefault(l =>
                string.Equals(l.Code, lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Code;

            var fallback = _supportedLanguages.FirstOrDefault(l =>
                string.Equals(l.Code, _fallbackLanguage, StringComparison.OrdinalIgnoreCase));
            return fallback?.Code ?? _supportedLanguages.First().Code;
        }

        public sealed record LanguageOption(string Code, string Name, string NativeName);
    }
}
