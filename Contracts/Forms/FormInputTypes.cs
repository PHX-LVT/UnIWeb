namespace Contracts.Forms;

public sealed record FormInputTypeCapability(
    string Type,
    string LabelKey,
    bool SupportsMaxCharacters,
    bool SupportsOptions,
    bool SupportsInputBoxSize,
    bool UsesMultilineInput,
    int DefaultMaxCharacters,
    int DefaultInputBoxSize);

public static class FormInputTypeCatalog
{
    public const int MinInputBoxSize = 1;
    public const int MaxInputBoxSize = 5;
    public const int MaxCharactersLimit = 2_000;

    private static readonly FormInputTypeCapability ShortText = new(
        "text",
        "FormInputShortText",
        SupportsMaxCharacters: true,
        SupportsOptions: false,
        SupportsInputBoxSize: false,
        UsesMultilineInput: false,
        DefaultMaxCharacters: 500,
        DefaultInputBoxSize: 1);

    private static readonly FormInputTypeCapability[] Types =
    [
        ShortText,
        new("email", "FormInputEmail", true, false, false, false, 254, 1),
        new("tel", "FormInputPhone", true, false, false, false, 40, 1),
        new("textarea", "FormInputLongText", true, false, true, true, 2_000, 4),
        new("select", "FormInputDropdown", false, true, false, false, 0, 1),
        new("checkbox", "FormInputCheckbox", false, false, false, false, 0, 1),
        new("date", "FormInputDate", false, false, false, false, 0, 1),
        new("number", "FormInputNumber", false, false, false, false, 0, 1),
        new("url", "FormInputUrl", true, false, false, false, 500, 1)
    ];

    private static readonly Dictionary<string, FormInputTypeCapability> ByType =
        Types.ToDictionary(item => item.Type, item => item, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<FormInputTypeCapability> EditorTypes { get; } = Types;

    public static string NormalizeType(string? type)
    {
        var normalized = type?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "phone" => "tel",
            "long-text" or "longtext" => "textarea",
            "dropdown" => "select",
            "short-text" or "shorttext" => "text",
            _ when string.IsNullOrWhiteSpace(normalized) => "text",
            _ => normalized!
        };
    }

    public static bool IsSupported(string? type) => ByType.ContainsKey(NormalizeType(type));

    public static FormInputTypeCapability Get(string? type) =>
        ByType.TryGetValue(NormalizeType(type), out var capability)
            ? capability
            : ShortText;

    public static int DefaultMaxCharacters(string? type) => Get(type).DefaultMaxCharacters;

    public static int DefaultInputBoxSize(string? type) => Get(type).DefaultInputBoxSize;

    public static int NormalizeMaxCharacters(string? type, int value)
    {
        var capability = Get(type);
        if (!capability.SupportsMaxCharacters)
            return 0;

        var candidate = value <= 0 ? capability.DefaultMaxCharacters : value;
        return Math.Clamp(candidate, 1, MaxCharactersLimit);
    }

    public static int NormalizeInputBoxSize(string? type, int value)
    {
        var capability = Get(type);
        if (!capability.SupportsInputBoxSize)
            return 1;

        var candidate = value <= 0 ? capability.DefaultInputBoxSize : value;
        return Math.Clamp(candidate, MinInputBoxSize, MaxInputBoxSize);
    }
}
