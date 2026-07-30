namespace AdminSite.Models;

public sealed class IconReferenceModel
{
    public int SchemaVersion { get; set; } = 1;
    public string Source { get; set; } = "built-in";
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
