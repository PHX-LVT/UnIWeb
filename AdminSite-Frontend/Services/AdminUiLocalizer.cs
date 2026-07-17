using AdminSite.Languages;
using AdminSite.Languages.Core;

namespace AdminSite.Services
{
    public static class AdminUiLocalizer
    {
        private static readonly object SyncRoot = new();
        private static string _fallbackLanguage = "en";

        private static readonly IReadOnlyDictionary<string, UiTextCatalog> Catalogs =
            new Dictionary<string, UiTextCatalog>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = EnglishUIText.Catalog,
                ["vi"] = VietnameseUIText.Catalog,
                ["cn"] = ChineseUIText.Catalog
            };

        public static IReadOnlyDictionary<string, UiTextCatalog> CompiledCatalogs => Catalogs;

        public static string FallbackLanguage
        {
            get
            {
                lock (SyncRoot)
                {
                    return _fallbackLanguage;
                }
            }
        }

        public static void SetFallbackLanguage(string? languageCode)
        {
            var normalized = NormalizeLanguageCode(languageCode);

            lock (SyncRoot)
            {
                _fallbackLanguage = normalized;
            }
        }

        public static UiTextCatalogReport Validate(
            string? fallbackLanguage,
            IEnumerable<UiTextLanguageState> configuredLanguages) =>
            UiTextCatalogValidator.Validate(Catalogs, fallbackLanguage, configuredLanguages);

        public static string T(string key, string lang = "en")
        {
            key ??= string.Empty;
            var requestedLanguage = NormalizeLanguageCode(lang);
            if (TryGetNonEmptyValue(requestedLanguage, key, out var requestedValue))
            {
                return requestedValue;
            }

            var fallbackLanguage = FallbackLanguage;
            if (!string.Equals(requestedLanguage, fallbackLanguage, StringComparison.OrdinalIgnoreCase)
                && TryGetNonEmptyValue(fallbackLanguage, key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return key;
        }

        private static bool TryGetNonEmptyValue(string languageCode, string key, out string value)
        {
            value = string.Empty;
            if (!Catalogs.TryGetValue(languageCode, out var catalog)
                || !catalog.TryGetValue(key, out var catalogValue)
                || string.IsNullOrWhiteSpace(catalogValue))
            {
                return false;
            }

            value = catalogValue;
            return true;
        }

        private static string NormalizeLanguageCode(string? languageCode)
        {
            languageCode = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
        }
    }
}
