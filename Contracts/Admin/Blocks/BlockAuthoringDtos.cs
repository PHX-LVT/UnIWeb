namespace Contracts.Admin;

public sealed class BlockAuthoringPolicyDto
{
    public int SchemaVersion { get; set; } = 1;
    public bool ContentLocked { get; set; }
    public bool GeometryLocked { get; set; }
    public bool FullLocked { get; set; }
    public string? PresetSlotName { get; set; }
    public string? PresetSourceId { get; set; }
}

public sealed class BlockLayoutMutationDto
{
    public string BlockId { get; set; } = string.Empty;
    public BlockLayoutDto Layout { get; set; } = new();
}

public sealed class BlockBulkLayoutUpdateDto
{
    public List<BlockLayoutMutationDto> Items { get; set; } = new();
}

public sealed class BlockDuplicateRequestDto
{
    public List<string> BlockIds { get; set; } = new();
}

public sealed class BlockAuthoringLockUpdateDto
{
    public bool ContentLocked { get; set; }
    public bool GeometryLocked { get; set; }
    public bool FullLocked { get; set; }
}

public sealed class BlockAuthoringOperationResponseDto
{
    public List<string> BlockIds { get; set; } = new();
}

public sealed class BlockMoveRequestDto
{
    public string BlockId { get; set; } = string.Empty;
    public string? TargetParentBlockId { get; set; }
    public string? TargetSlotName { get; set; }
    public int? TargetIndex { get; set; }
}

public sealed class CanvasPresetEditableSlotDto
{
    public string Name { get; set; } = string.Empty;
    public string BlockStableId { get; set; } = string.Empty;
    public string Kind { get; set; } = "content";
    public Dictionary<string, string> Label { get; set; } = new();
}

public sealed class CanvasPresetLockPolicyDto
{
    public bool LockGeometryOnApply { get; set; }
    public bool LockContentOutsideSlots { get; set; }
}
