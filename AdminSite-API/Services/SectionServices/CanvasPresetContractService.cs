using FullProject.Models;

namespace FullProject.Services.SectionServices;

public sealed class CanvasPresetContractService
{
    public const int CurrentSchemaVersion = 3;
    public const int MinimumSupportedSchemaVersion = 1;

    private static readonly HashSet<string> SlotKinds = new(StringComparer.Ordinal)
    {
        "content", "text", "icon", "image", "video", "link", "form"
    };

    public CanvasPresetCompatibility Compatibility(CanvasSectionPreset preset)
    {
        if (preset.SchemaVersion < MinimumSupportedSchemaVersion)
            return new(false, $"Preset schema {preset.SchemaVersion} is too old to migrate safely.");
        if (preset.SchemaVersion > CurrentSchemaVersion)
            return new(false, $"Preset schema {preset.SchemaVersion} requires a newer application version.");
        return new(true, preset.SchemaVersion == CurrentSchemaVersion
            ? null
            : $"Preset schema {preset.SchemaVersion} will be upgraded in memory when applied.");
    }

    public string? PrepareAndValidate(CanvasSectionPreset preset)
    {
        var compatibility = Compatibility(preset);
        if (!compatibility.IsCompatible) return compatibility.Message;
        UpgradeInPlace(preset);
        return Validate(preset);
    }

    public void UpgradeInPlace(CanvasSectionPreset preset)
    {
        preset.Name ??= new();
        preset.Style ??= new SectionStyle();
        preset.Blocks ??= new();
        preset.EditableSlots ??= new();
        preset.LockPolicy ??= new CanvasPresetLockPolicy();
        foreach (var block in preset.Blocks)
        {
            block.Authoring ??= new BlockAuthoringPolicy();
            block.Authoring.SchemaVersion = Math.Max(block.Authoring.SchemaVersion, 1);
            block.Appearance ??= new BlockAppearance();
            block.Responsive ??= new BlockResponsiveSettings();
            block.Animation ??= new BlockAnimationSettings();
        }
        preset.SchemaVersion = CurrentSchemaVersion;
    }

    public string? Validate(CanvasSectionPreset preset)
    {
        if (!preset.Name.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            return "Preset name is required.";
        if (preset.Blocks.Count == 0)
            return "A Canvas preset must contain at least one Block.";
        if (preset.Blocks.Count > 250)
            return "A Canvas preset supports at most 250 Blocks.";

        var ids = preset.Blocks.Select(block => block.Id).ToList();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            return "Preset Block IDs must be present and unique.";
        var stableIds = preset.Blocks.Select(block => block.StableId).ToList();
        if (stableIds.Any(string.IsNullOrWhiteSpace) || stableIds.Distinct(StringComparer.Ordinal).Count() != stableIds.Count)
            return "Preset Block stable identities must be present and unique.";

        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var block in preset.Blocks)
        {
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId) && !idSet.Contains(block.ParentBlockId))
                return "Every preset parent reference must stay inside the preset graph.";
            if (HasParentCycle(block, preset.Blocks))
                return "Preset Block parent relationships contain a cycle.";
        }

        var slotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slottedBlocks = new HashSet<string>(StringComparer.Ordinal);
        var stableIdSet = stableIds.ToHashSet(StringComparer.Ordinal);
        foreach (var slot in preset.EditableSlots)
        {
            slot.Name = NormalizeSlotName(slot.Name);
            slot.Kind = NormalizeSlotKind(slot.Kind);
            if (string.IsNullOrWhiteSpace(slot.Name))
                return "Every editable preset slot requires a name.";
            if (!slotNames.Add(slot.Name))
                return $"Editable preset slot '{slot.Name}' is duplicated.";
            if (!stableIdSet.Contains(slot.BlockStableId))
                return $"Editable preset slot '{slot.Name}' references a missing Block.";
            if (!slottedBlocks.Add(slot.BlockStableId))
                return "A preset Block can belong to only one editable slot.";
            if (!SlotKinds.Contains(slot.Kind))
                return $"Editable preset slot '{slot.Name}' has an unsupported kind.";
        }

        return null;
    }

    public void ApplyPolicy(CanvasSectionPreset preset, IEnumerable<Block> appliedBlocks)
    {
        var sourceById = preset.Blocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
        var slotByStableId = preset.EditableSlots.ToDictionary(slot => slot.BlockStableId, StringComparer.Ordinal);
        foreach (var block in appliedBlocks)
        {
            sourceById.TryGetValue(block.SourceId ?? string.Empty, out var source);
            var slot = source is not null && slotByStableId.TryGetValue(source.StableId, out var value)
                ? value
                : null;
            block.Authoring = new BlockAuthoringPolicy
            {
                SchemaVersion = 1,
                GeometryLocked = preset.LockPolicy.LockGeometryOnApply,
                ContentLocked = preset.LockPolicy.LockContentOutsideSlots && slot is null,
                FullLocked = false,
                PresetSlotName = slot?.Name,
                PresetSourceId = preset.Id
            };
        }
    }

    public static string NormalizeSlotName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        return normalized.Trim('-');
    }

    public static string NormalizeSlotKind(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ? "content" : normalized;
    }

    private static bool HasParentCycle(Block start, IReadOnlyCollection<Block> blocks)
    {
        var byId = blocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = start;
        while (!string.IsNullOrWhiteSpace(current.ParentBlockId) &&
               byId.TryGetValue(current.ParentBlockId, out var parent))
        {
            if (!visited.Add(parent.Id)) return true;
            current = parent;
        }
        return false;
    }
}

public sealed record CanvasPresetCompatibility(bool IsCompatible, string? Message);
