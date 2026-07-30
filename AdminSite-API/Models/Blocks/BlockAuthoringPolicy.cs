using MongoDB.Bson.Serialization.Attributes;

namespace FullProject.Models;

[BsonIgnoreExtraElements]
[BsonNoId]
public sealed class BlockAuthoringPolicy
{
    public int SchemaVersion { get; set; } = 1;
    public bool ContentLocked { get; set; }
    public bool GeometryLocked { get; set; }
    public bool FullLocked { get; set; }
    public bool IsPlaceholder { get; set; }
    public string? PresetSlotName { get; set; }
    public string? PresetSourceId { get; set; }
}

[BsonIgnoreExtraElements]
[BsonNoId]
public sealed class CanvasPresetEditableSlot
{
    public string Name { get; set; } = string.Empty;
    public string BlockStableId { get; set; } = string.Empty;
    public string Kind { get; set; } = "content";
    public Dictionary<string, string> Label { get; set; } = new();
}

[BsonIgnoreExtraElements]
[BsonNoId]
public sealed class CanvasPresetLockPolicy
{
    public bool LockGeometryOnApply { get; set; }
    public bool LockContentOutsideSlots { get; set; }
}
