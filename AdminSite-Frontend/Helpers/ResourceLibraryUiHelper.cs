using AdminSite.Models;

namespace AdminSite.Helpers
{
    public sealed record ResourceGroupFilterOption(string Key, string Scope, string Name, int Count);

    public static class ResourceLibraryUiHelper
    {
        public static string ResourceName(ManagedResourceModel resource, string lang)
        {
            var name = GetValue(resource.Name, lang, fallback: true);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            if (!string.IsNullOrWhiteSpace(resource.FileName)) return resource.FileName;
            return resource.Url.Split('/').LastOrDefault() ?? "resource";
        }

        public static string ResourceDescription(ManagedResourceModel resource, string lang) =>
            GetValue(resource.Description, lang, fallback: true);

        public static string ResourceFileName(ManagedResourceModel resource) =>
            string.IsNullOrWhiteSpace(resource.FileName) ? "resource" : resource.FileName;

        public static string ResourceThumbnailUrl(ManagedResourceModel resource) =>
            !string.IsNullOrWhiteSpace(resource.ThumbnailUrl)
                ? resource.ThumbnailUrl!
                : NormalizeKind(resource.Kind) == "image" ? resource.Url : string.Empty;

        public static string ResourceIcon(ManagedResourceModel resource) => NormalizeKind(resource.Kind) switch
        {
            "image" => "fa-image",
            "video" => "fa-play",
            _ => "fa-file"
        };

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "-";
            string[] units = ["B", "KB", "MB", "GB"];
            var size = (double)bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.#} {units[unit]}";
        }

        public static string NormalizeKind(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image" or "gallery" => "image",
            "video" => "video",
            _ => "file"
        };

        public static string NormalizeLang(string? lang)
        {
            var normalized = (lang ?? "en").Trim().ToLowerInvariant();
            return normalized is "vi" or "cn" ? normalized : "en";
        }

        public static string GetValue(Dictionary<string, string> values, string lang, bool fallback = false)
        {
            if (values.TryGetValue(lang, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
            return fallback && values.TryGetValue("en", out var en) ? en : string.Empty;
        }

        public static bool MatchesGroupFilter(ManagedResourceModel resource, string? groupFilter)
        {
            if (string.IsNullOrWhiteSpace(groupFilter)) return true;
            var parts = groupFilter.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return true;

            var scope = ResourceGroupScope(resource);
            return string.Equals(scope, parts[0], StringComparison.OrdinalIgnoreCase) &&
                   resource.Tags.Any(tag => string.Equals(tag, parts[1], StringComparison.OrdinalIgnoreCase));
        }

        public static List<ResourceGroupFilterOption> BuildGroupFilterOptions(IEnumerable<ManagedResourceModel> resources) =>
            resources
                .SelectMany(resource => resource.Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => new
                    {
                        Scope = ResourceGroupScope(resource),
                        Name = tag.Trim()
                    }))
                .GroupBy(item => $"{item.Scope}|{item.Name}", StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    return new ResourceGroupFilterOption($"{first.Scope}|{first.Name}", first.Scope, first.Name, group.Count());
                })
                .OrderBy(option => option.Scope)
                .ThenBy(option => option.Name)
                .ToList();

        public static string ResourceGroupScope(ManagedResourceModel resource) =>
            NormalizeKind(resource.Kind) == "file" ? "file" : "media";
    }
}
