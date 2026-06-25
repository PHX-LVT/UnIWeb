namespace AdminSite.Helpers
{
    public static class ResourceLibrarySettingsUiHelper
    {
        public static string JoinFormats(IEnumerable<string> formats) =>
            string.Join(", ", formats.Select(f => f.Trim().TrimStart('.')).Where(f => !string.IsNullOrWhiteSpace(f)));

        public static List<string> ParseFormats(string text) =>
            text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(f => f.Trim().TrimStart('.').ToLowerInvariant())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
