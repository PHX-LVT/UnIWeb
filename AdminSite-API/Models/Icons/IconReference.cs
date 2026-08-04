using MongoDB.Bson.Serialization.Attributes;

namespace FullProject.Models;

[BsonIgnoreExtraElements]
[BsonNoId]
public sealed class IconReference
{
    public int SchemaVersion { get; set; } = 2;
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
    public Contracts.Icons.IconAppearanceDto? Appearance { get; set; }
}
