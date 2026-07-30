namespace Contracts.Icons;

public static class IconSources
{
    public const string BuiltIn = "built-in";
    public const string Custom = "custom";
}

public sealed class IconReferenceDto
{
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = IconSources.BuiltIn;
    public string? ClassName { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceSource { get; set; }
    public string? Url { get; set; }
    public string? StorageKey { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public Dictionary<string, string> AltText { get; set; } = new();
}

public sealed class PublicIconReferenceDto
{
    public string Source { get; set; } = IconSources.BuiltIn;
    public string? ClassName { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string> AltText { get; set; } = new();
}
