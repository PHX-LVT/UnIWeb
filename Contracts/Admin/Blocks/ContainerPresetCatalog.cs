namespace Contracts.Admin;

public sealed record ContainerPresetSlotDefinition(
    string Key,
    string DisplayName,
    bool Required,
    IReadOnlyList<string> AllowedBlockTypes,
    double XPercent = 50,
    double YPercent = 50,
    double MobileXPercent = 50,
    double MobileYPercent = 50);

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
    string MobileMode,
    bool IsFormation = false,
    string FormationKind = "legacy",
    double AspectRatio = 1,
    int MinimumWidthPx = 360,
    int DefaultWidthPx = 640,
    int MaximumWidthPx = 1200,
    int DefaultItemWidthPx = 160);

/// <summary>
/// The persisted block discriminator remains "container" for compatibility, but
/// new authoring uses only governed Formation presets. Legacy presets stay in the
/// lookup so existing documents can render until their migration is complete.
/// </summary>
public static class ContainerPresetCatalog
{
    public const int CatalogVersion = 2;

    // Formation presets available to new authoring.
    public const string CircleFourKey = "formation-circle-four";
    public const string CircleSixKey = "formation-circle-six";
    public const string CircleEightKey = "formation-circle-eight";
    public const string SemicircleFourKey = "formation-semicircle-four";
    public const string SemicircleSixKey = "formation-semicircle-six";
    public const string TriangleKey = "formation-triangle-three";
    public const string PyramidKey = "formation-pyramid-six";
    public const string StackedCardsKey = "formation-stacked-four";
    public const string ZigzagKey = "formation-zigzag-five";
    public const string ProcessPathKey = "formation-process-six";

    // Legacy keys remain readable during the migration window.
    public const string StackKey = "stack-basic";
    public const string RowKey = "row-six";
    public const string GridKey = "grid-six";
    public const string SplitKey = "split-two";
    public const string OrbitKey = "orbit-eight";
    public const string SemicircleKey = "semicircle-six";
    public const string AdvancedFreeformKey = "advanced-freeform-ten";

    private static readonly IReadOnlyList<string> LegacyTypes = ContainerCapacityPolicy.ChildBlockTypes;
    private static readonly IReadOnlyList<string> FormationTypes =
    [
        "text", "image", "card", "button", "metric", "step", "icon"
    ];

    private static readonly IReadOnlyDictionary<string, ContainerPresetDefinition> Formations =
        new Dictionary<string, ContainerPresetDefinition>(StringComparer.Ordinal)
        {
            [CircleFourKey] = Formation(CircleFourKey, "Circle · 4 Blocks", "circle", CircleSlots(4), 1, 380, 560, 1000, 150),
            [CircleSixKey] = Formation(CircleSixKey, "Circle · 6 Blocks", "circle", CircleSlots(6), 1, 460, 680, 1100, 150),
            [CircleEightKey] = Formation(CircleEightKey, "Circle · 8 Blocks", "circle", CircleSlots(8), 1, 560, 820, 1200, 140),
            [SemicircleFourKey] = Formation(SemicircleFourKey, "Semi-circle · 4 Blocks", "semicircle", SemicircleSlots(4), 1.75, 520, 760, 1200, 160),
            [SemicircleSixKey] = Formation(SemicircleSixKey, "Semi-circle · 6 Blocks", "semicircle", SemicircleSlots(6), 1.8, 620, 900, 1400, 150),
            [TriangleKey] = Formation(TriangleKey, "Triangle · 3 Blocks", "triangle",
            [
                Slot("point-1", "Top", 50, 14),
                Slot("point-2", "Bottom left", 18, 78),
                Slot("point-3", "Bottom right", 82, 78)
            ], 1.2, 420, 620, 1000, 170),
            [PyramidKey] = Formation(PyramidKey, "Pyramid · 6 Blocks", "pyramid",
            [
                Slot("level-1-1", "Level 1", 50, 12),
                Slot("level-2-1", "Level 2 left", 34, 45),
                Slot("level-2-2", "Level 2 right", 66, 45),
                Slot("level-3-1", "Level 3 left", 18, 80),
                Slot("level-3-2", "Level 3 centre", 50, 80),
                Slot("level-3-3", "Level 3 right", 82, 80)
            ], 1.25, 560, 760, 1200, 145),
            [StackedCardsKey] = Formation(StackedCardsKey, "Stacked Cards · 4 Blocks", "stacked",
            [
                Slot("stack-1", "Back card", 38, 34),
                Slot("stack-2", "Card 2", 46, 43),
                Slot("stack-3", "Card 3", 54, 52),
                Slot("stack-4", "Front card", 62, 61)
            ], 1.3, 420, 600, 900, 210),
            [ZigzagKey] = Formation(ZigzagKey, "Zigzag · 5 Blocks", "zigzag",
            [
                Slot("zig-1", "Step 1", 10, 30, 32, 10),
                Slot("zig-2", "Step 2", 30, 70, 68, 30),
                Slot("zig-3", "Step 3", 50, 30, 32, 50),
                Slot("zig-4", "Step 4", 70, 70, 68, 70),
                Slot("zig-5", "Step 5", 90, 30, 32, 90)
            ], 1.8, 620, 920, 1400, 155),
            [ProcessPathKey] = Formation(ProcessPathKey, "Process Path · 6 Blocks", "process",
            [
                Slot("step-1", "Step 1", 8, 50, 50, 8),
                Slot("step-2", "Step 2", 25, 50, 50, 25),
                Slot("step-3", "Step 3", 42, 50, 50, 42),
                Slot("step-4", "Step 4", 58, 50, 50, 58),
                Slot("step-5", "Step 5", 75, 50, 50, 75),
                Slot("step-6", "Step 6", 92, 50, 50, 92)
            ], 2.25, 720, 1080, 1600, 145)
        };

    private static readonly IReadOnlyDictionary<string, ContainerPresetDefinition> Legacy =
        new Dictionary<string, ContainerPresetDefinition>(StringComparer.Ordinal)
        {
            [StackKey] = LegacyPreset(StackKey, "Stack", "stack", "collection", 8, "stack", 1, "item"),
            [RowKey] = LegacyPreset(RowKey, "Row", "row", "collection", 6, "stack", 6, "item"),
            [GridKey] = LegacyPreset(GridKey, "Grid", "grid", "collection", 6, "stack", 2, "cell"),
            [SplitKey] = new ContainerPresetDefinition(
                SplitKey, "Split layout", "split", "composition", 2, LegacyTypes,
                [new("left", "Left", true, LegacyTypes), new("right", "Right", true, LegacyTypes)],
                false, false, "stack", "none", 2, "stack"),
            [OrbitKey] = LegacyPreset(OrbitKey, "Orbit", "orbit", "composition", 8, "compact-preserve", 1, "orbit"),
            [SemicircleKey] = LegacyPreset(SemicircleKey, "Semi-circle", "semicircle", "composition", 6, "compact-preserve", 1, "arc"),
            [AdvancedFreeformKey] = LegacyPreset(AdvancedFreeformKey, "Advanced composition", "freeform", "composition", 10, "custom", 1, "layer")
        };

    public static IReadOnlyCollection<ContainerPresetDefinition> All => Formations.Values.ToArray();
    public static IReadOnlyCollection<ContainerPresetDefinition> LegacyAll => Legacy.Values.ToArray();

    public static bool TryGetFormation(string? key, out ContainerPresetDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(key) && Formations.TryGetValue(key, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static bool TryGetGoverned(string? key, out ContainerPresetDefinition definition)
    {
        if (TryGetFormation(key, out definition)) return true;
        if (!string.IsNullOrWhiteSpace(key) && Legacy.TryGetValue(key, out var legacy))
        {
            definition = legacy;
            return true;
        }

        definition = null!;
        return false;
    }

    public static bool IsFormation(string? key) => TryGetFormation(key, out _);
    public static bool IsLegacy(string? key) => !string.IsNullOrWhiteSpace(key) && Legacy.ContainsKey(key);

    public static ContainerPresetDefinition ForExisting(string? key) =>
        TryGetGoverned(key, out var definition)
            ? definition
            : throw new ArgumentException("Choose a supported Formation preset.", nameof(key));

    public static ContainerPresetDefinition ForFormation(string? key) =>
        TryGetFormation(key, out var definition)
            ? definition
            : throw new ArgumentException("Choose a supported Formation preset.", nameof(key));

    public static string EffectiveKey(string? key) => ForExisting(key).Key;

    public static string? ResolveCreationKey(string? requestedKey, string? requestedMode)
    {
        if (!string.IsNullOrWhiteSpace(requestedKey))
            return TryGetFormation(requestedKey, out var formation) ? formation.Key : null;

        return CircleSixKey;
    }

    public static string KeyForMode(string? mode) => CircleSixKey;

    public static string? FormationMigrationKey(string? legacyKey) => legacyKey switch
    {
        OrbitKey => CircleEightKey,
        SemicircleKey => SemicircleSixKey,
        _ => null
    };

    private static ContainerPresetDefinition Formation(
        string key,
        string displayName,
        string kind,
        IReadOnlyList<ContainerPresetSlotDefinition> slots,
        double aspectRatio,
        int minimumWidth,
        int defaultWidth,
        int maximumWidth,
        int defaultItemWidth) =>
        new(
            key,
            displayName,
            "formation",
            "formation",
            slots.Count,
            FormationTypes,
            slots,
            ChildOrderingAllowed: false,
            EmptySlotsOptional: false,
            ResponsiveBehavior: "formation",
            DiagramBehavior: "preset",
            Columns: 1,
            MobileMode: "formation",
            IsFormation: true,
            FormationKind: kind,
            AspectRatio: aspectRatio,
            MinimumWidthPx: minimumWidth,
            DefaultWidthPx: defaultWidth,
            MaximumWidthPx: maximumWidth,
            DefaultItemWidthPx: defaultItemWidth);

    private static ContainerPresetDefinition LegacyPreset(
        string key,
        string displayName,
        string mode,
        string purpose,
        int maximumChildren,
        string responsiveBehavior,
        int columns,
        string slotPrefix)
    {
        var slots = Enumerable.Range(1, maximumChildren)
            .Select(index => new ContainerPresetSlotDefinition(
                $"{slotPrefix}-{index}", $"{displayName} {index}", false, LegacyTypes))
            .ToArray();

        return new ContainerPresetDefinition(
            key, displayName, mode, purpose, maximumChildren, LegacyTypes, slots,
            true, true, responsiveBehavior, mode, columns,
            responsiveBehavior == "compact-preserve" ? "compact-preserve" : "stack");
    }

    private static ContainerPresetSlotDefinition Slot(
        string key,
        string displayName,
        double x,
        double y,
        double? mobileX = null,
        double? mobileY = null) =>
        new(key, displayName, true, FormationTypes, x, y, mobileX ?? x, mobileY ?? y);

    private static IReadOnlyList<ContainerPresetSlotDefinition> CircleSlots(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var angle = (-90d + 360d * index / count) * Math.PI / 180d;
                var x = 50d + Math.Cos(angle) * 36d;
                var y = 50d + Math.Sin(angle) * 36d;
                var mobileX = 50d + Math.Cos(angle) * 32d;
                var mobileY = 50d + Math.Sin(angle) * 42d;
                return Slot($"circle-{index + 1}", $"Position {index + 1}", x, y, mobileX, mobileY);
            })
            .ToArray();
    }

    private static IReadOnlyList<ContainerPresetSlotDefinition> SemicircleSlots(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var angle = (180d + 180d * index / Math.Max(1, count - 1)) * Math.PI / 180d;
                var x = 50d + Math.Cos(angle) * 40d;
                var y = 72d + Math.Sin(angle) * 52d;
                var progress = index / (double)Math.Max(1, count - 1);
                var mobileX = 34d + Math.Sin(progress * Math.PI) * 34d;
                var mobileY = 10d + progress * 80d;
                return Slot($"arc-{index + 1}", $"Position {index + 1}", x, y, mobileX, mobileY);
            })
            .ToArray();
    }
}
