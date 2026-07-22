namespace Contracts.Admin;

public sealed record ContainerPresetSlotDefinition(
    string Key,
    string DisplayName,
    bool Required,
    IReadOnlyList<string> AllowedBlockTypes);

public sealed record ContainerPresetDefinition(
    string Key,
    string DisplayName,
    string LayoutMode,
    string Purpose,
    int MaximumChildren,
    IReadOnlyList<string> AllowedBlockTypes,
    IReadOnlyList<ContainerPresetSlotDefinition> Slots,
    bool ChildOrderingAllowed,
    bool EmptySlotsOptional,
    string ResponsiveBehavior,
    string DiagramBehavior,
    int Columns,
    string MobileMode);

public static class ContainerPresetCatalog
{
    public const int CatalogVersion = 1;
    public const string StackKey = "stack-basic";
    public const string RowKey = "row-six";
    public const string GridKey = "grid-six";
    public const string SplitKey = "split-two";
    public const string OrbitKey = "orbit-eight";
    public const string SemicircleKey = "semicircle-six";
    public const string AdvancedFreeformKey = "advanced-freeform-ten";

    private static readonly IReadOnlyList<string> StandardTypes = ContainerCapacityPolicy.ChildBlockTypes;

    private static readonly IReadOnlyDictionary<string, ContainerPresetDefinition> Governed =
        new Dictionary<string, ContainerPresetDefinition>(StringComparer.Ordinal)
        {
            [StackKey] = Preset(StackKey, "Stack", "stack", "collection", 8, "stack", "none", 1,
                ordering: true, optional: true, slotPrefix: "item"),
            [RowKey] = Preset(RowKey, "Row", "row", "collection", 6, "stack", "none", 6,
                ordering: true, optional: true, slotPrefix: "item"),
            [GridKey] = Preset(GridKey, "Grid", "grid", "collection", 6, "stack", "none", 2,
                ordering: true, optional: true, slotPrefix: "cell"),
            [SplitKey] = new ContainerPresetDefinition(
                SplitKey,
                "Split layout",
                "split",
                "composition",
                2,
                StandardTypes,
                [
                    new("left", "Left", true, StandardTypes),
                    new("right", "Right", true, StandardTypes)
                ],
                ChildOrderingAllowed: false,
                EmptySlotsOptional: false,
                ResponsiveBehavior: "stack",
                DiagramBehavior: "none",
                Columns: 2,
                MobileMode: "stack"),
            [OrbitKey] = Preset(OrbitKey, "Orbit", "orbit", "composition", 8, "compact-preserve", "orbit", 1,
                ordering: true, optional: true, slotPrefix: "orbit"),
            [SemicircleKey] = Preset(SemicircleKey, "Semi-circle", "semicircle", "composition", 6, "compact-preserve", "semicircle", 1,
                ordering: true, optional: true, slotPrefix: "arc"),
            [AdvancedFreeformKey] = Preset(AdvancedFreeformKey, "Advanced composition", "freeform", "composition", 10, "custom", "advanced", 1,
                ordering: true, optional: true, slotPrefix: "layer")
        };

    public static IReadOnlyCollection<ContainerPresetDefinition> All => Governed.Values.ToArray();

    public static bool TryGetGoverned(string? key, out ContainerPresetDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(key) && Governed.TryGetValue(key, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static ContainerPresetDefinition ForExisting(string? key) =>
        TryGetGoverned(key, out var definition)
            ? definition
            : throw new ArgumentException("Choose a supported Container preset.", nameof(key));

    public static string EffectiveKey(string? key) => ForExisting(key).Key;

    public static string? ResolveCreationKey(string? requestedKey, string? requestedMode)
    {
        if (!string.IsNullOrWhiteSpace(requestedKey))
            return TryGetGoverned(requestedKey, out var definition) ? definition.Key : null;

        return KeyForMode(requestedMode);
    }

    public static string KeyForMode(string? mode) => ContainerCapacityPolicy.NormalizeMode(mode) switch
    {
        "row" => RowKey,
        "grid" => GridKey,
        "split" => SplitKey,
        "orbit" => OrbitKey,
        "semicircle" => SemicircleKey,
        "freeform" => AdvancedFreeformKey,
        _ => StackKey
    };

    private static ContainerPresetDefinition Preset(
        string key,
        string displayName,
        string mode,
        string purpose,
        int maximumChildren,
        string responsiveBehavior,
        string diagramBehavior,
        int columns,
        bool ordering,
        bool optional,
        string slotPrefix)
    {
        var slots = Enumerable.Range(1, maximumChildren)
            .Select(index => new ContainerPresetSlotDefinition(
                $"{slotPrefix}-{index}",
                $"{displayName} {index}",
                Required: false,
                StandardTypes))
            .ToArray();

        return new ContainerPresetDefinition(
            key,
            displayName,
            mode,
            purpose,
            maximumChildren,
            StandardTypes,
            slots,
            ordering,
            optional,
            responsiveBehavior,
            diagramBehavior,
            columns,
            responsiveBehavior is "compact-preserve" ? "compact-preserve" : "stack");
    }
}
