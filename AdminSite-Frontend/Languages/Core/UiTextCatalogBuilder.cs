using System.Collections.ObjectModel;

namespace AdminSite.Languages.Core
{
    public sealed class UiTextCatalog
    {
        internal UiTextCatalog(string languageCode, Dictionary<string, string> values, IEnumerable<string> duplicateKeys)
        {
            LanguageCode = languageCode;
            Values = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
            DuplicateKeys = duplicateKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string LanguageCode { get; }

        public IReadOnlyDictionary<string, string> Values { get; }

        public IReadOnlyList<string> DuplicateKeys { get; }

        public bool TryGetValue(string key, out string value) => Values.TryGetValue(key, out value!);
    }

    public sealed class UiTextCatalogBuilder
    {
        private readonly string _languageCode;
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _duplicateKeys = new();

        public UiTextCatalogBuilder(string languageCode)
        {
            _languageCode = NormalizeLanguageCode(languageCode);
        }

        public string this[string key]
        {
            set => Add(key, value);
        }

        public UiTextCatalogBuilder Add(string key, string? value)
        {
            key = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A UI text key cannot be empty.", nameof(key));
            }

            if (_values.ContainsKey(key))
            {
                _duplicateKeys.Add(key);
            }

            _values[key] = value ?? string.Empty;
            return this;
        }

        public UiTextCatalog Build() => new(_languageCode, _values, _duplicateKeys);

        private static string NormalizeLanguageCode(string? languageCode)
        {
            languageCode = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
        }
    }
}
