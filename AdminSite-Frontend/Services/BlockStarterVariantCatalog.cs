using Contracts.Admin;
using Contracts.Forms;

namespace AdminSite.Services;

public sealed record BlockStarterVariantDefinition(
    string Key,
    string LabelKey,
    string DescriptionKey,
    string PreviewClass,
    int WidthUnits,
    int HeightUnits,
    int ColumnSpan,
    string AspectRatio = "auto",
    string Shape = "rectangle",
    string BackgroundMode = "none",
    int BorderWidth = 0,
    string Shadow = "none",
    string TextAlign = "inherit",
    string Padding = "medium",
    string? ButtonStyle = null,
    string? ContainerPresetKey = null,
    string? ContainerMode = null,
    int ContainerColumns = 2);

public static class BlockStarterVariantCatalog
{
    public const int CatalogVersion = 1;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<BlockStarterVariantDefinition>> Variants =
        new Dictionary<string, IReadOnlyList<BlockStarterVariantDefinition>>(StringComparer.Ordinal)
        {
            ["text"] =
            [
                V("default", "Text", "BlockDescriptionText", "text-editorial", 6, 3, 6, padding: "none")
            ],
            ["bullet-list"] =
            [
                V("clean", "StarterClean", "StarterCleanHint", "list-clean", 5, 4, 5),
                V("bordered", "StarterBordered", "StarterBorderedHint", "list-bordered", 5, 4, 5, border: 1),
                V("wide", "StarterWide", "StarterWideHint", "list-wide", 8, 4, 8)
            ],
            ["image"] =
            [
                V("square", "StarterSquare", "StarterSquareHint", "image-square", 4, 4, 4, aspect: "square", padding: "none"),
                V("landscape", "StarterLandscape", "StarterLandscapeHint", "image-landscape", 6, 4, 6, aspect: "landscape", padding: "none"),
                V("full-width", "StarterFullWidth", "StarterFullWidthHint", "image-full", 12, 5, 12, aspect: "widescreen", padding: "none")
            ],
            ["video"] =
            [
                V("widescreen", "StarterWidescreen", "StarterWidescreenHint", "video-wide", 8, 5, 8, aspect: "widescreen", padding: "none"),
                V("compact", "StarterCompact", "StarterCompactHint", "video-compact", 5, 3, 5, aspect: "widescreen", padding: "none"),
                V("full-width", "StarterFullWidth", "StarterFullWidthHint", "video-full", 12, 6, 12, aspect: "widescreen", padding: "none")
            ],
            ["file"] =
            [
                V("tile", "StarterTile", "StarterTileHint", "file-tile", 4, 2, 4, border: 1),
                V("compact", "StarterCompact", "StarterCompactHint", "file-compact", 3, 2, 3, padding: "small"),
                V("wide", "StarterWide", "StarterWideHint", "file-wide", 7, 2, 7, border: 1)
            ],
            ["card"] =
            [
                V("minimal", "StarterMinimal", "StarterMinimalHint", "card-minimal", 4, 5, 4),
                V("bordered", "StarterBordered", "StarterBorderedHint", "card-bordered", 4, 6, 4, border: 1),
                V("media", "StarterMedia", "StarterMediaHint", "card-media", 5, 6, 5, aspect: "landscape", border: 1, shadow: "small")
            ],
            ["metric"] =
            [
                V("compact", "StarterCompact", "StarterCompactHint", "metric-compact", 3, 3, 3, align: "center"),
                V("centered", "StarterCentered", "StarterCenteredHint", "metric-centered", 4, 3, 4, align: "center"),
                V("highlight", "StarterHighlight", "StarterHighlightHint", "metric-highlight", 4, 4, 4, background: "theme-background", align: "center", shadow: "small")
            ],
            ["step"] =
            [
                V("compact", "StarterCompact", "StarterCompactHint", "step-compact", 4, 3, 4, padding: "small"),
                V("bordered", "StarterBordered", "StarterBorderedHint", "step-bordered", 4, 4, 4, border: 1),
                V("wide", "StarterWide", "StarterWideHint", "step-wide", 7, 3, 7)
            ],
            ["icon"] =
            [
                V("bare", "StarterBare", "StarterBareHint", "icon-bare", 3, 2, 3, align: "center", padding: "small"),
                V("circular", "StarterCircular", "StarterCircularHint", "icon-circle", 3, 3, 3, shape: "circle", background: "theme-background", align: "center"),
                V("labeled", "StarterLabeled", "StarterLabeledHint", "icon-labeled", 4, 3, 4, align: "center"),
                V("card", "StarterCard", "StarterCardHint", "icon-card", 4, 4, 4, border: 1, shadow: "small", align: "center")
            ],
            ["button"] =
            [
                V("filled", "StarterFilled", "StarterFilledHint", "button-filled", 3, 2, 3, shape: "pill", align: "center", padding: "small", buttonStyle: "filled"),
                V("outline", "StarterOutline", "StarterOutlineHint", "button-outline", 3, 2, 3, shape: "pill", align: "center", padding: "small", buttonStyle: "outline"),
                V("ghost", "StarterGhost", "StarterGhostHint", "button-ghost", 3, 2, 3, align: "center", padding: "small", buttonStyle: "ghost")
            ],
            ["map"] =
            [
                V("compact", "StarterCompact", "StarterCompactHint", "map-compact", 6, 4, 6, aspect: "landscape", padding: "none"),
                V("wide", "StarterWide", "StarterWideHint", "map-wide", 8, 5, 8, aspect: "landscape", padding: "none"),
                V("full-width", "StarterFullWidth", "StarterFullWidthHint", "map-full", 12, 6, 12, aspect: "widescreen", padding: "none")
            ],
            ["container"] =
            [
                V("circle-4", "FormationCircleFour", "FormationCircleFourHint", "formation-circle", 8, 8, 10, containerPresetKey: ContainerPresetCatalog.CircleFourKey, containerMode: "formation"),
                V("circle-6", "FormationCircleSix", "FormationCircleSixHint", "formation-circle", 10, 10, 10, containerPresetKey: ContainerPresetCatalog.CircleSixKey, containerMode: "formation"),
                V("circle-8", "FormationCircleEight", "FormationCircleEightHint", "formation-circle", 10, 10, 10, containerPresetKey: ContainerPresetCatalog.CircleEightKey, containerMode: "formation"),
                V("semicircle-4", "FormationSemicircleFour", "FormationSemicircleFourHint", "formation-semicircle", 10, 6, 10, containerPresetKey: ContainerPresetCatalog.SemicircleFourKey, containerMode: "formation"),
                V("semicircle-6", "FormationSemicircleSix", "FormationSemicircleSixHint", "formation-semicircle", 12, 7, 12, containerPresetKey: ContainerPresetCatalog.SemicircleSixKey, containerMode: "formation"),
                V("triangle", "FormationTriangle", "FormationTriangleHint", "formation-triangle", 9, 8, 9, containerPresetKey: ContainerPresetCatalog.TriangleKey, containerMode: "formation"),
                V("pyramid", "FormationPyramid", "FormationPyramidHint", "formation-pyramid", 10, 8, 10, containerPresetKey: ContainerPresetCatalog.PyramidKey, containerMode: "formation"),
                V("stacked", "FormationStacked", "FormationStackedHint", "formation-stacked", 8, 7, 8, containerPresetKey: ContainerPresetCatalog.StackedCardsKey, containerMode: "formation"),
                V("zigzag", "FormationZigzag", "FormationZigzagHint", "formation-zigzag", 12, 6, 12, containerPresetKey: ContainerPresetCatalog.ZigzagKey, containerMode: "formation"),
                V("process", "FormationProcess", "FormationProcessHint", "formation-process", 12, 5, 12, containerPresetKey: ContainerPresetCatalog.ProcessPathKey, containerMode: "formation")
            ]
        };

    public static IReadOnlyList<BlockStarterVariantDefinition> ForType(string type) =>
        Variants.TryGetValue(type, out var variants) ? variants : Variants["text"];

    public static BlockCreateDto CreateDto(string type, string variantKey, string? formDefinitionId = null)
    {
        if (string.Equals(type, "form", StringComparison.Ordinal))
        {
            return new FormBlockCreateDto
            {
                FormDefinitionId = formDefinitionId,
                FormScale = 1d,
                Layout = new BlockLayoutDto
                {
                    Width = "custom",
                    ColumnSpan = 12,
                    Align = "stretch",
                    Justify = "start",
                    Padding = "none",
                    Margin = "none",
                    BorderRadius = "none",
                    W = 12,
                    H = 1,
                    WidthPercent = 100,
                    HeightPx = FormDesignPolicy.MinimumHeightPx
                },
                Appearance = new BlockAppearanceDto
                {
                    SchemaVersion = 2,
                    BackgroundMode = "none",
                    Opacity = 1,
                    BorderWidth = 0,
                    BorderStyle = "solid",
                    BorderRadius = "none",
                    Shadow = "none",
                    Shape = "rectangle",
                    AspectRatio = "auto",
                    Padding = "none",
                    Margin = "none"
                }
            };
        }
        var variant = ForType(type).FirstOrDefault(item => item.Key == variantKey) ?? ForType(type)[0];
        var dto = CreateTypedDto(type, variant);
        dto.Layout = new BlockLayoutDto
        {
            Width = variant.ColumnSpan == 12 ? "full" : "auto",
            ColumnSpan = variant.ColumnSpan,
            Align = "stretch",
            Justify = "start",
            Padding = "none",
            Margin = "none",
            BorderRadius = variant.Shape == "circle" ? "circle" : variant.Shape == "pill" ? "pill" : "medium",
            W = variant.WidthUnits,
            H = variant.HeightUnits,
            WidthPercent = variant.WidthUnits / 12d * 100d,
            HeightPx = variant.HeightUnits * 48d
        };
        dto.Appearance = new BlockAppearanceDto
        {
            SchemaVersion = 2,
            BackgroundMode = variant.BackgroundMode,
            TextAlign = variant.TextAlign,
            Opacity = 1,
            BorderWidth = variant.BorderWidth,
            BorderStyle = "solid",
            BorderRadius = dto.Layout.BorderRadius,
            Shadow = variant.Shadow,
            Shape = variant.Shape,
            AspectRatio = variant.AspectRatio,
            MediaFit = "cover",
            MediaPosition = "center",
            Padding = variant.Padding,
            Margin = "none",
            Decorative = false
        };
        return dto;
    }

    private static BlockCreateDto CreateTypedDto(string type, BlockStarterVariantDefinition variant) => type switch
    {
        "text" => new TextBlockCreateDto(),
        "bullet-list" => new BulletListBlockCreateDto(),
        "image" => new ImageBlockCreateDto(),
        "video" => new VideoBlockCreateDto(),
        "file" => new FileBlockCreateDto(),
        "card" => new CardBlockCreateDto(),
        "metric" => new MetricBlockCreateDto(),
        "step" => new StepBlockCreateDto(),
        "icon" => new IconBlockCreateDto(),
        "button" => new ButtonBlockCreateDto { Style = variant.ButtonStyle ?? "filled" },
        "map" => new MapBlockCreateDto(),
        "container" => CreateContainer(variant),
        _ => new TextBlockCreateDto()
    };

    private static ContainerBlockCreateDto CreateContainer(BlockStarterVariantDefinition variant)
    {
        var key = variant.ContainerPresetKey ?? ContainerPresetCatalog.KeyForMode(variant.ContainerMode);
        var preset = ContainerPresetCatalog.ForExisting(key);
        return new ContainerBlockCreateDto
        {
            PresetKey = preset.Key,
            ContainerLayout = new ContainerLayoutSettingsDto
            {
                SchemaVersion = 4,
                Purpose = preset.Purpose,
                AllowedChildType = null,
                Mode = preset.LayoutMode,
                Columns = preset.Columns,
                Gap = "medium",
                AlignItems = "stretch",
                JustifyContent = "start",
                Wrap = true,
                MobileMode = preset.MobileMode,
                CompactRadius = 120,
                CompactChildWidth = 120,
                SizeMode = "medium",
                ItemSize = "standard",
                FormationSpacing = "standard",
                ConnectorColorMode = "theme-accent",
                ConnectorStyle = "solid"
            }
        };
    }

    private static BlockStarterVariantDefinition V(
        string key,
        string label,
        string description,
        string preview,
        int width,
        int height,
        int span,
        string aspect = "auto",
        string shape = "rectangle",
        string background = "none",
        int border = 0,
        string shadow = "none",
        string align = "inherit",
        string padding = "medium",
        string? buttonStyle = null,
        string? containerPresetKey = null,
        string? containerMode = null,
        int containerColumns = 2) =>
        new(key, label, description, preview, width, height, span, aspect, shape, background, border, shadow, align, padding, buttonStyle, containerPresetKey, containerMode, containerColumns);
}
