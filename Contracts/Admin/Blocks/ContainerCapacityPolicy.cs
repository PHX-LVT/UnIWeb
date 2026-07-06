namespace Contracts.Admin;

public static class ContainerCapacityPolicy
{
    public static IReadOnlyList<string> ChildBlockTypes { get; } =
    [
        "text", "bullet-list", "image", "video", "file", "card",
        "metric", "step", "icon", "button", "map", "form"
    ];

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

    public static string NormalizeMode(string? mode) => mode switch
    {
        "row" => "row",
        "grid" => "grid",
        "split" => "split",
        "orbit" => "orbit",
        "semicircle" => "semicircle",
        "freeform" => "freeform",
        _ => "stack"
    };
}
