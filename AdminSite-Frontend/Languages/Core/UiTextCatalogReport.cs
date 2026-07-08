namespace AdminSite.Languages.Core
{
    public enum UiTextCatalogSeverity
    {
        Complete,
        Warning,
        Error,
        Critical,
        Unsupported
    }

    public sealed record UiTextLanguageState(
        string LanguageCode,
        string DisplayName,
        bool Active,
        bool AdminEnabled,
        int Order = 0);

    public sealed class UiTextCatalogReport
    {
        public string FallbackLanguage { get; init; } = "en";

        public IReadOnlyList<UiTextLanguageReport> Languages { get; init; } = Array.Empty<UiTextLanguageReport>();

        public UiTextLanguageReport? KeyLanguage =>
            Languages.FirstOrDefault(language => language.IsKeyLanguage);

        public UiTextCatalogSeverity OverallSeverity
        {
            get
            {
                if (Languages.Any(language => language.Severity == UiTextCatalogSeverity.Critical))
                {
                    return UiTextCatalogSeverity.Critical;
                }

                if (Languages.Any(language => language.Severity == UiTextCatalogSeverity.Error))
                {
                    return UiTextCatalogSeverity.Error;
                }

                if (Languages.Any(language => language.Severity == UiTextCatalogSeverity.Warning))
                {
                    return UiTextCatalogSeverity.Warning;
                }

                if (Languages.Any(language => language.Severity == UiTextCatalogSeverity.Unsupported))
                {
                    return UiTextCatalogSeverity.Unsupported;
                }

                return UiTextCatalogSeverity.Complete;
            }
        }
    }

    public sealed class UiTextLanguageReport
    {
        public string LanguageCode { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public bool IsKeyLanguage { get; init; }

        public bool IsAdminEnabled { get; init; }

        public bool HasCatalog { get; init; }

        public int TotalRequiredKeys { get; init; }

        public int TranslatedCount { get; init; }

        public IReadOnlyList<string> MissingKeys { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> EmptyKeys { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> DuplicateKeys { get; init; } = Array.Empty<string>();

        public double CoveragePercentage =>
            TotalRequiredKeys <= 0 ? 100 : Math.Round(TranslatedCount * 100d / TotalRequiredKeys, 1);

        public UiTextCatalogSeverity Severity { get; init; } = UiTextCatalogSeverity.Complete;

        public bool HasIssues =>
            MissingKeys.Count > 0 || EmptyKeys.Count > 0 || DuplicateKeys.Count > 0 || !HasCatalog;
    }
}
