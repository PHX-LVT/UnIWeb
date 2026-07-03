using Contracts.Admin;

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
                V("editorial", "StarterEditorial", "StarterEditorialHint", "text-editorial", 6, 3, 6),
                V("compact", "StarterCompact", "StarterCompactHint", "text-compact", 5, 2, 5, padding: "small"),
                V("feature", "StarterFeature", "StarterFeatureHint", "text-feature", 8, 4, 8, background: "theme-background", align: "center")
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
            ["form"] =
            [
                V("compact", "StarterCompact", "StarterCompactHint", "form-compact", 5, 6, 5, border: 1),
                V("standard", "StarterStandard", "StarterStandardHint", "form-standard", 7, 7, 7, border: 1),
                V("full-width", "StarterFullWidth", "StarterFullWidthHint", "form-full", 12, 8, 12, border: 1)
            ],
            ["container"] =
            [
                V("stack", "StarterStackCollection", "StarterStackCollectionHint", "container-stack", 8, 6, 8, containerMode: "stack"),
                V("grid", "StarterGridCollection", "StarterGridCollectionHint", "container-grid", 10, 7, 10, containerMode: "grid", containerColumns: 2),
                V("advanced", "StarterAdvancedComposition", "StarterAdvancedCompositionHint", "container-advanced", 10, 8, 10, containerMode: "freeform")
            ]
        };

    public static IReadOnlyList<BlockStarterVariantDefinition> ForType(string type) =>
        Variants.TryGetValue(type, out var variants) ? variants : Variants["text"];

    public static BlockCreateDto CreateDto(string type, string variantKey)
    {
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
        "form" => new FormBlockCreateDto(),
        "container" => new ContainerBlockCreateDto
        {
            ContainerLayout = new ContainerLayoutSettingsDto
            {
                SchemaVersion = 2,
                Purpose = variant.Key == "advanced" ? "composition" : "collection",
                AllowedChildType = null,
                Mode = variant.ContainerMode ?? "stack",
                Columns = variant.ContainerColumns,
                Gap = "medium",
                AlignItems = "stretch",
                JustifyContent = "start",
                Wrap = true,
                MobileMode = "stack",
                CompactRadius = 120,
                CompactChildWidth = 120,
                GeometryLocked = false
            }
        },
        _ => new TextBlockCreateDto()
    };

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
        string? containerMode = null,
        int containerColumns = 2) =>
        new(key, label, description, preview, width, height, span, aspect, shape, background, border, shadow, align, padding, buttonStyle, containerMode, containerColumns);
}
