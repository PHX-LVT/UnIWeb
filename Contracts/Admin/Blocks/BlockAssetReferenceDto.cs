namespace Contracts.Admin;

public class BlockAssetReferenceDto
{
    public int? SchemaVersion { get; set; }
    public string? Url { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceSource { get; set; }
    public string? StorageKey { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
}
