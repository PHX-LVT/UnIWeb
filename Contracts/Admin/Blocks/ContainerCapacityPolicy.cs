namespace Contracts.Admin;

public static class ContainerCapacityPolicy
{
    public static IReadOnlyList<string> ChildBlockTypes { get; } =
        BlockCapabilityCatalog.All
            .Where(definition => definition.CanBeContainerChild)
            .Select(definition => definition.Type)
            .ToArray();

    public static bool CanOwn(string? blockType) =>
        !string.IsNullOrWhiteSpace(blockType) && ChildBlockTypes.Contains(blockType, StringComparer.Ordinal);

    public static int MaxChildren(string? mode, int? columns = null) => NormalizeMode(mode) switch
    {
        "row" => 6,
        "grid" => Math.Clamp(columns ?? 2, 1, 6) * 3,
        "split" => 2,
        "orbit" => 8,
        "semicircle" => 6,
        "freeform" => 10,
        _ => 8
    };

    public static int MaxChildren(string? presetKey, string? mode, int? columns = null) =>
        ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset)
            ? preset.MaximumChildren
            : MaxChildren(mode, columns);

    public static IReadOnlyList<string> AllowedChildTypes(string? presetKey) =>
        ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset)
            ? preset.AllowedBlockTypes
            : ChildBlockTypes;

    public static ContainerPresetSlotDefinition? NextAvailableSlot(
        string? presetKey,
        IEnumerable<string?> usedSlotKeys,
        string childType)
    {
        if (!ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset) ||
            preset.Slots.Count == 0)
            return null;

        var used = (usedSlotKeys ?? Array.Empty<string?>())
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Select(slot => slot!)
            .ToHashSet(StringComparer.Ordinal);

        return preset.Slots.FirstOrDefault(slot =>
            !used.Contains(slot.Key) &&
            slot.AllowedBlockTypes.Contains(childType, StringComparer.Ordinal));
    }

    public static bool IsRequiredSlot(string? presetKey, string? slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey) ||
            !ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset))
            return false;

        return preset.Slots.Any(slot =>
            string.Equals(slot.Key, slotKey, StringComparison.Ordinal) &&
            slot.Required);
    }

    public static string SlotDisplayName(string? presetKey, string? slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey) ||
            !ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset))
            return slotKey ?? string.Empty;

        return preset.Slots.FirstOrDefault(slot =>
            string.Equals(slot.Key, slotKey, StringComparison.Ordinal))?.DisplayName
            ?? slotKey;
    }

    public static IReadOnlyList<ContainerPresetSlotDefinition> MissingRequiredSlots(
        string? presetKey,
        IEnumerable<string?> occupiedSlotKeys)
    {
        if (!ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset))
            return Array.Empty<ContainerPresetSlotDefinition>();

        var occupied = (occupiedSlotKeys ?? Array.Empty<string?>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);

        return preset.Slots
            .Where(slot => slot.Required && !occupied.Contains(slot.Key))
            .ToArray();
    }

    public static bool CanConvert(
        string? targetPresetKey,
        IEnumerable<string> childTypes,
        out string? error)
    {
        error = null;
        if (!ContainerPresetCatalog.TryGetGoverned(targetPresetKey, out var target))
        {
            error = "Choose a supported Formation preset.";
            return false;
        }

        var children = (childTypes ?? Array.Empty<string>()).ToList();
        if (children.Count > target.MaximumChildren)
        {
            error = $"The {target.DisplayName} preset supports at most {target.MaximumChildren} Blocks.";
            return false;
        }

        var unsupported = children.FirstOrDefault(type =>
            !target.AllowedBlockTypes.Contains(type, StringComparer.Ordinal));
        if (unsupported is not null)
        {
            error = $"The {target.DisplayName} preset does not allow {unsupported} Blocks.";
            return false;
        }

        if (target.Purpose == "collection" && children.Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            error = "A Collection Container can contain only one Block type.";
            return false;
        }

        return true;
    }

    public static string NormalizeMode(string? mode) => mode switch
    {
        "formation" => "formation",
        "row" => "row",
        "grid" => "grid",
        "split" => "split",
        "orbit" => "orbit",
        "semicircle" => "semicircle",
        "freeform" => "freeform",
        _ => "stack"
    };
}
