using Contracts.Admin;

namespace FullProject.Services.BlockServices;

public static class BlockStarterPresetCatalog
{
    public const int CatalogVersion = 1;

    private sealed record StarterPreset(
        BlockLayoutDto Layout,
        BlockAppearanceDto Appearance,
        ContainerLayoutSettingsDto? Container = null);

    public static void Apply(BlockCreateDto dto)
    {
        var preset = Get(dto.Type);
        MergeLayout(dto.Layout ??= new BlockLayoutDto(), preset.Layout);
        MergeAppearance(dto.Appearance ??= new BlockAppearanceDto(), preset.Appearance);
        dto.Responsive ??= new BlockResponsiveSettingsDto { SchemaVersion = 1 };
        dto.Responsive.SchemaVersion ??= 1;
        dto.Animation ??= new BlockAnimationSettingsDto
        {
            SchemaVersion = 1,
            Effect = "none",
            Trigger = "enter-viewport",
            DurationMs = 500,
            DelayMs = 0,
            Easing = "ease-out",
            PlayOnce = true,
            StaggerMs = 0,
            ContinuousEffect = "none",
            DisableForReducedMotion = true
        };

        if (dto is ContainerBlockCreateDto container && preset.Container is not null)
        {
            container.ContainerLayout ??= new ContainerLayoutSettingsDto();
            MergeContainer(container.ContainerLayout, preset.Container);
            var resolvedKey = ContainerPresetCatalog.ResolveCreationKey(
                container.PresetKey,
                container.ContainerLayout.Mode);
            if (resolvedKey is not null && ContainerPresetCatalog.TryGetGoverned(resolvedKey, out var governed))
            {
                container.PresetKey = governed.Key;
                ApplyGovernance(container.ContainerLayout, governed);
            }
        }
    }

    private static StarterPreset Get(string type) => type switch
    {
        "text" => Preset(6, 3, 6, padding: "none"),
        "bullet-list" => Preset(5, 4, 5, padding: "medium", radius: "medium"),
        "image" => Preset(5, 4, 5, radius: "medium", aspect: "landscape", mediaFit: "cover"),
        "video" => Preset(6, 4, 6, radius: "medium", aspect: "widescreen", mediaFit: "cover"),
        "file" => Preset(4, 2, 4, padding: "medium", radius: "medium", borderWidth: 1),
        "card" => Preset(4, 6, 4, padding: "medium", radius: "medium", borderWidth: 1, shadow: "small"),
        "metric" => Preset(3, 3, 3, padding: "medium", radius: "medium", textAlign: "center"),
        "step" => Preset(4, 3, 4, padding: "medium", radius: "medium"),
        "icon" => Preset(3, 3, 3, padding: "small", textAlign: "center"),
        "button" => Preset(3, 2, 3, padding: "small", radius: "pill", shape: "pill", textAlign: "center"),
        "map" => Preset(6, 5, 6, radius: "medium", aspect: "landscape"),
        "form" => Preset(6, 6, 6, padding: "medium", radius: "medium", borderWidth: 1),
        "container" => Preset(
            8,
            6,
            8,
            container: new ContainerLayoutSettingsDto
            {
                SchemaVersion = 2,
                Purpose = "collection",
                Mode = "stack",
                Columns = 2,
                Gap = "medium",
                AlignItems = "stretch",
                JustifyContent = "start",
                Wrap = true,
                MobileMode = "stack",
                CompactRadius = 120,
                CompactChildWidth = 120,
                GeometryLocked = false
            }),
        _ => Preset(4, 3, 4, padding: "medium")
    };

    private static StarterPreset Preset(
        int widthUnits,
        int heightUnits,
        int columnSpan,
        string padding = "none",
        string radius = "none",
        int borderWidth = 0,
        string shadow = "none",
        string shape = "rectangle",
        string textAlign = "inherit",
        string aspect = "auto",
        string mediaFit = "cover",
        ContainerLayoutSettingsDto? container = null)
    {
        return new StarterPreset(
            new BlockLayoutDto
            {
                Width = "auto",
                ColumnSpan = columnSpan,
                Align = "stretch",
                Justify = "start",
                Padding = "none",
                Margin = "none",
                BorderRadius = radius,
                W = widthUnits,
                H = heightUnits,
                WidthPercent = widthUnits / 12d * 100d,
                HeightPx = heightUnits * 48d
            },
            new BlockAppearanceDto
            {
                SchemaVersion = 2,
                BackgroundMode = "none",
                TextAlign = textAlign,
                Opacity = 1,
                BorderWidth = borderWidth,
                BorderStyle = "solid",
                BorderRadius = radius,
                Shadow = shadow,
                Shape = shape,
                AspectRatio = aspect,
                MediaFit = mediaFit,
                MediaPosition = "center",
                Padding = padding,
                Margin = "none",
                Decorative = false
            },
            container);
    }

    private static void MergeLayout(BlockLayoutDto target, BlockLayoutDto defaults)
    {
        target.Width ??= defaults.Width;
        target.ColumnSpan ??= defaults.ColumnSpan;
        target.Align ??= defaults.Align;
        target.Justify ??= defaults.Justify;
        target.Padding ??= defaults.Padding;
        target.Margin ??= defaults.Margin;
        target.BorderRadius ??= defaults.BorderRadius;
        target.W ??= defaults.W;
        target.H ??= defaults.H;
        target.WidthPercent ??= defaults.WidthPercent;
        target.HeightPx ??= defaults.HeightPx;
    }

    private static void MergeAppearance(BlockAppearanceDto target, BlockAppearanceDto defaults)
    {
        target.SchemaVersion ??= defaults.SchemaVersion;
        target.BackgroundMode ??= defaults.BackgroundMode;
        target.TextAlign ??= defaults.TextAlign;
        target.FontSizePx ??= defaults.FontSizePx;
        target.FontWeight ??= defaults.FontWeight;
        target.FontFamily ??= defaults.FontFamily;
        target.LineHeight ??= defaults.LineHeight;
        target.LetterSpacingPx ??= defaults.LetterSpacingPx;
        target.Opacity ??= defaults.Opacity;
        target.BorderWidth ??= defaults.BorderWidth;
        target.BorderStyle ??= defaults.BorderStyle;
        target.BorderRadius ??= defaults.BorderRadius;
        target.Shadow ??= defaults.Shadow;
        target.Shape ??= defaults.Shape;
        target.AspectRatio ??= defaults.AspectRatio;
        target.RotationDeg ??= defaults.RotationDeg;
        target.MediaFit ??= defaults.MediaFit;
        target.MediaPosition ??= defaults.MediaPosition;
        target.Padding ??= defaults.Padding;
        target.Margin ??= defaults.Margin;
        target.Decorative ??= defaults.Decorative;
        target.InheritFromContainer ??= defaults.InheritFromContainer;
    }

    private static void MergeContainer(ContainerLayoutSettingsDto target, ContainerLayoutSettingsDto defaults)
    {
        target.SchemaVersion ??= defaults.SchemaVersion;
        target.Purpose ??= defaults.Purpose;
        target.AllowedChildType ??= defaults.AllowedChildType;
        target.Mode ??= defaults.Mode;
        target.Columns ??= defaults.Columns;
        target.Gap ??= defaults.Gap;
        target.AlignItems ??= defaults.AlignItems;
        target.JustifyContent ??= defaults.JustifyContent;
        target.Wrap ??= defaults.Wrap;
        target.MobileMode ??= defaults.MobileMode;
        target.CompactRadius ??= defaults.CompactRadius;
        target.CompactChildWidth ??= defaults.CompactChildWidth;
        target.GeometryLocked ??= defaults.GeometryLocked;
        target.ShareAppearance ??= defaults.ShareAppearance;
        target.SharedAppearance ??= defaults.SharedAppearance;
    }

    private static void ApplyGovernance(
        ContainerLayoutSettingsDto target,
        ContainerPresetDefinition preset)
    {
        target.SchemaVersion = 3;
        target.Purpose = preset.Purpose;
        target.Mode = preset.LayoutMode;
        target.Columns = preset.Columns;
        target.MobileMode = preset.MobileMode;
        if (preset.Purpose != "collection")
            target.AllowedChildType = null;
    }
}
