namespace AdminSite.Languages.Core
{
    public static class UiTextCatalogValidator
    {
        public static UiTextCatalogReport Validate(
            IReadOnlyDictionary<string, UiTextCatalog> catalogs,
            string? fallbackLanguage,
            IEnumerable<UiTextLanguageState> configuredLanguages)
        {
            var fallbackCode = NormalizeLanguageCode(fallbackLanguage);
            var catalogMap = catalogs.ToDictionary(
                pair => NormalizeLanguageCode(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var configured = configuredLanguages
                .Select(language => language with
                {
                    LanguageCode = NormalizeLanguageCode(language.LanguageCode),
                    DisplayName = string.IsNullOrWhiteSpace(language.DisplayName)
                        ? NormalizeLanguageCode(language.LanguageCode).ToUpperInvariant()
                        : language.DisplayName
                })
                .ToList();

            var unionKeys = catalogMap.Values
                .SelectMany(catalog => catalog.Values.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            catalogMap.TryGetValue(fallbackCode, out var fallbackCatalog);
            var fallbackRequiredKeys = fallbackCatalog is null
                ? unionKeys
                : unionKeys
                    .Union(fallbackCatalog.Values.Keys, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            var secondaryRequiredKeys = fallbackCatalog?.Values.Keys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? unionKeys;

            var rowStates = configured
                .Where(language => language.Active && language.AdminEnabled)
                .Concat(configured.Where(language => string.Equals(language.LanguageCode, fallbackCode, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(language => language.LanguageCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(language => language.Order <= 0 ? int.MaxValue : language.Order).First())
                .ToList();

            if (!rowStates.Any(language => string.Equals(language.LanguageCode, fallbackCode, StringComparison.OrdinalIgnoreCase)))
            {
                rowStates.Add(new UiTextLanguageState(fallbackCode, fallbackCode.ToUpperInvariant(), true, true, 0));
            }

            var rows = rowStates
                .OrderBy(language => string.Equals(language.LanguageCode, fallbackCode, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(language => language.Order <= 0 ? int.MaxValue : language.Order)
                .ThenBy(language => language.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(language => BuildLanguageReport(
                    language,
                    catalogMap.TryGetValue(language.LanguageCode, out var catalog) ? catalog : null,
                    fallbackCode,
                    fallbackRequiredKeys,
                    secondaryRequiredKeys))
                .ToArray();

            return new UiTextCatalogReport
            {
                FallbackLanguage = fallbackCode,
                Languages = rows
            };
        }

        private static UiTextLanguageReport BuildLanguageReport(
            UiTextLanguageState language,
            UiTextCatalog? catalog,
            string fallbackCode,
            IReadOnlyCollection<string> fallbackRequiredKeys,
            IReadOnlyCollection<string> secondaryRequiredKeys)
        {
            var isKeyLanguage = string.Equals(language.LanguageCode, fallbackCode, StringComparison.OrdinalIgnoreCase);
            var requiredKeys = isKeyLanguage ? fallbackRequiredKeys : secondaryRequiredKeys;
            var missingKeys = new List<string>();
            var emptyKeys = new List<string>();
            var translatedCount = 0;

            foreach (var key in requiredKeys)
            {
                if (catalog is null || !catalog.Values.TryGetValue(key, out var value))
                {
                    missingKeys.Add(key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    emptyKeys.Add(key);
                    continue;
                }

                translatedCount++;
            }

            var duplicateKeys = catalog?.DuplicateKeys ?? Array.Empty<string>();
            var severity = DetermineSeverity(
                hasCatalog: catalog is not null,
                isKeyLanguage,
                missingKeys.Count,
                emptyKeys.Count,
                duplicateKeys.Count);

            return new UiTextLanguageReport
            {
                LanguageCode = language.LanguageCode,
                DisplayName = language.DisplayName,
                IsKeyLanguage = isKeyLanguage,
                IsAdminEnabled = language.Active && language.AdminEnabled,
                HasCatalog = catalog is not null,
                TotalRequiredKeys = requiredKeys.Count,
                TranslatedCount = translatedCount,
                MissingKeys = missingKeys
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                EmptyKeys = emptyKeys
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                DuplicateKeys = duplicateKeys
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Severity = severity
            };
        }

        private static UiTextCatalogSeverity DetermineSeverity(
            bool hasCatalog,
            bool isKeyLanguage,
            int missingCount,
            int emptyCount,
            int duplicateCount)
        {
            if (!hasCatalog)
            {
                return isKeyLanguage ? UiTextCatalogSeverity.Critical : UiTextCatalogSeverity.Unsupported;
            }

            if (isKeyLanguage && (missingCount > 0 || emptyCount > 0))
            {
                return UiTextCatalogSeverity.Critical;
            }

            if (duplicateCount > 0)
            {
                return UiTextCatalogSeverity.Error;
            }

            if (missingCount > 0 || emptyCount > 0)
            {
                return UiTextCatalogSeverity.Warning;
            }

            return UiTextCatalogSeverity.Complete;
        }

        private static string NormalizeLanguageCode(string? languageCode)
        {
            languageCode = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode;
        }
    }
}
