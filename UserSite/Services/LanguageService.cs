using Blazored.LocalStorage;

namespace UserSite.Services
{
    public class LanguageService
    {
        private readonly ILocalStorageService _storage;
        private readonly PublicApiService _api;
        private string? _lang;
        private string _fallbackLanguage = "en";
        private List<LanguageOption> _languages = new()
        {
            new("en", "English", "English"),
            new("vi", "Vietnamese", "Tiếng Việt"),
            new("cn", "Chinese", "中文")
        };

        public LanguageService(ILocalStorageService storage, PublicApiService api)
        {
            _storage = storage;
            _api = api;
        }

        public event Action<string>? Changed;
        public IReadOnlyList<LanguageOption> Languages => _languages;
        public string FallbackLanguage => _fallbackLanguage;

        public async Task<string> GetAsync()
        {
            await LoadSettingsAsync();
            _lang ??= Normalize(await _storage.GetItemAsync<string>("lang"));
            return _lang;
        }

        public async Task SetAsync(string lang)
        {
            await LoadSettingsAsync();
            var normalized = Normalize(lang);
            if (_lang == normalized) return;

            _lang = normalized;
            await _storage.SetItemAsync("lang", _lang);
            Changed?.Invoke(_lang);
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _api.GetLanguageSettingsAsync();
                _fallbackLanguage = string.IsNullOrWhiteSpace(settings.DefaultLanguage)
                    ? "en"
                    : settings.DefaultLanguage;

                var languages = settings.Languages
                    .Where(l => l.Active && l.UserEnabled)
                    .OrderBy(l => l.Order)
                    .Select(l => new LanguageOption(
                        l.Slug,
                        string.IsNullOrWhiteSpace(l.Label) ? l.Slug.ToUpperInvariant() : l.Label,
                        string.IsNullOrWhiteSpace(l.NativeName) ? l.Slug.ToUpperInvariant() : l.NativeName))
                    .ToList();

                if (languages.Count > 0)
                    _languages = languages;
            }
            catch
            {
                // Keep built-in defaults if the API is temporarily unavailable.
            }

            _lang = Normalize(_lang);
        }

        private string Normalize(string? lang)
        {
            var match = _languages.FirstOrDefault(l =>
                string.Equals(l.Code, lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Code;

            var fallback = _languages.FirstOrDefault(l =>
                string.Equals(l.Code, _fallbackLanguage, StringComparison.OrdinalIgnoreCase));
            return fallback?.Code ?? _languages.First().Code;
        }

        public sealed record LanguageOption(string Code, string Name, string NativeName);
    }
}
