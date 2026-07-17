namespace Contracts.Global;

public sealed record ThemeFontDefinition(
    string Key,
    string DisplayName,
    string CssStack,
    string Category,
    string PreviewText);

public static class ThemeFontCatalog
{
    public const string DefaultFont = "Inter";
    public const string InheritKey = "inherit";

    private const string DefaultPreview = "The quick brown fox";

    public static IReadOnlyList<ThemeFontDefinition> Fonts { get; } =
    [
        new("inter", "Inter", "\"Inter\", sans-serif", "Web", DefaultPreview),
        new("arial", "Arial", "Arial, Helvetica, sans-serif", "System", DefaultPreview),
        new("calibri", "Calibri", "Calibri, Candara, \"Segoe UI\", sans-serif", "System", DefaultPreview),
        new("times-new-roman", "Times New Roman", "\"Times New Roman\", Times, serif", "System", DefaultPreview),
        new("lexend", "Lexend", "\"Lexend\", sans-serif", "Web", DefaultPreview),
        new("roboto", "Roboto", "\"Roboto\", Arial, sans-serif", "Web", DefaultPreview),
        new("open-sans", "Open Sans", "\"Open Sans\", Arial, sans-serif", "Web", DefaultPreview),
        new("lato", "Lato", "\"Lato\", Arial, sans-serif", "Web", DefaultPreview),
        new("montserrat", "Montserrat", "\"Montserrat\", Arial, sans-serif", "Web", DefaultPreview),
        new("poppins", "Poppins", "\"Poppins\", Arial, sans-serif", "Web", DefaultPreview),
        new("noto-sans", "Noto Sans", "\"Noto Sans\", Arial, sans-serif", "Multilingual", DefaultPreview),
        new("noto-serif", "Noto Serif", "\"Noto Serif\", \"Times New Roman\", serif", "Multilingual", DefaultPreview)
    ];

    public static ThemeFontDefinition Default => Fonts[0];

    public static bool IsAllowed(string? value) => TryResolve(value, out _);

    public static string NormalizeNameOrDefault(string? value) =>
        TryResolve(value, out var font) ? font.DisplayName : DefaultFont;

    public static string? NormalizeOptionalName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : TryResolve(value, out var font) ? font.DisplayName : null;

    public static string CssStackOrDefault(string? value) =>
        TryResolve(value, out var font) ? font.CssStack : Default.CssStack;

    public static bool TryResolve(string? value, out ThemeFontDefinition font)
    {
        font = Default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = Normalize(value);
        var match = Fonts.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Normalize(candidate.DisplayName), normalized, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return false;

        font = match;
        return true;
    }

    private static string Normalize(string value) =>
        value.Trim()
            .Replace("_", "-", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();
}
