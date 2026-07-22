using AdminSite.Models;
using Contracts.Admin;

namespace AdminSite.Services.Mapper;

public static class BlockUpdateDtoMapper
{
    public static BlockUpdateDto Build(BlockModel block)
    {
        var layout = ToLayoutDto(block.Layout);

        BlockUpdateDto dto = block.Type switch
        {
            "text" => new TextBlockUpdateDto
            {
                Title = block.Title ?? new(),
                Content = block.Content ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "image" => new ImageBlockUpdateDto
            {
                Asset = ToAssetDto(block.Asset),
                AltText = block.AltText ?? new(),
                Caption = block.Caption ?? new(),
                OpenInLightbox = block.OpenInLightbox,
                FocalPointX = block.FocalPointX,
                FocalPointY = block.FocalPointY,
                Visible = block.Visible,
                Layout = layout
            },
            "video" => new VideoBlockUpdateDto
            {
                Asset = ToAssetDto(block.Asset),
                SourceType = block.SourceType,
                Title = block.Title ?? new(),
                ShowControls = block.ShowControls,
                Autoplay = block.Autoplay,
                Muted = block.Muted,
                Loop = block.Loop,
                Visible = block.Visible,
                Layout = layout
            },
            "file" => new FileBlockUpdateDto
            {
                Asset = ToAssetDto(block.Asset),
                Filename = block.FileName ?? string.Empty,
                DisplayName = block.DisplayName ?? new(),
                OpenBehavior = block.OpenBehavior,
                Visible = block.Visible,
                Layout = layout
            },
            "map" => new MapBlockUpdateDto
            {
                CenterLat = block.CenterLat ?? 0,
                CenterLng = block.CenterLng ?? 0,
                DefaultZoom = block.DefaultZoom ?? 12,
                Pins = block.Pins?.Select(p => new MapPinDto
                {
                    Id = p.Id,
                    Label = p.Label ?? string.Empty,
                    Lat = p.Lat,
                    Lng = p.Lng,
                    Href = p.Href,
                    Visible = p.Visible,
                    Order = p.Order
                }).ToList() ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "form" => new FormBlockUpdateDto
            {
                FormDefinitionId = block.FormDefinitionId,
                FormScale = block.FormScale,
                Visible = block.Visible,
                Layout = layout
            },
            "card" => new CardBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                Title = block.Title ?? new(),
                Description = block.Description ?? new(),
                Asset = ToAssetDto(block.Asset),
                ImageAltText = block.AltText ?? new(),
                ButtonLabel = block.ButtonLabel ?? new(),
                Href = block.Href,
                Action = block.Action ?? "linkToPage",
                FormDefinitionId = block.FormDefinitionId,
                ButtonStyle = block.Style ?? "outline",
                Visible = block.Visible,
                Layout = layout
            },
            "button" => new ButtonBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                IconPosition = block.IconPosition,
                Label = block.Label ?? new(),
                Href = block.Href,
                Action = block.Action ?? "linkToPage",
                FormDefinitionId = block.FormDefinitionId,
                Style = block.Style ?? "filled",
                Visible = block.Visible,
                Layout = layout
            },
            "metric" => new MetricBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                Label = block.Label ?? new(),
                Value = block.Value ?? string.Empty,
                Prefix = block.Prefix,
                Suffix = block.Suffix,
                Description = block.Description ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "bullet-list" => new BulletListBlockUpdateDto
            {
                Title = block.Title ?? new(),
                Items = block.BulletItems?.Select((item, i) => new BulletListItemDto
                {
                    Id = item.Id,
                    Icon = item.Icon,
                    Text = item.Text ?? new(),
                    Visible = item.Visible,
                    Order = i
                }).ToList() ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "step" => new StepBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                AutoNumber = block.AutoNumber,
                StepLabel = block.StepLabel ?? new(),
                Title = block.Title ?? new(),
                Description = block.Description ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "icon" => new IconBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                Label = block.Label ?? new(),
                Description = block.Description ?? new(),
                ActionEnabled = block.ActionEnabled,
                Href = block.Href,
                Action = block.Action ?? "linkToPage",
                FormDefinitionId = block.FormDefinitionId,
                Visible = block.Visible,
                Layout = layout
            },
            "container" => new ContainerBlockUpdateDto
            {
                PresetKey = block.PresetKey,
                Title = block.Title ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            _ => new TextBlockUpdateDto
            {
                Title = block.Title ?? new(),
                Content = block.Content ?? new(),
                Visible = block.Visible,
                Layout = layout
            }
        };

        dto.Visible = block.Visible;
        dto.EditorLabel = block.EditorLabel is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(block.EditorLabel);
        dto.Layout = layout;
        dto.BlockZone = NormalizeBlockZone(block.BlockZone);
        dto.PositionMode = NormalizePositionMode(block.PositionMode);
        dto.ParentBlockId = block.ParentBlockId;
        dto.Appearance = ToAppearanceDto(block.Appearance);
        dto.Responsive = ToResponsiveDto(block.Responsive);
        dto.Animation = ToAnimationDto(block.Animation);
        dto.Authoring = ToAuthoringDto(block.Authoring);
        if (dto is ContainerBlockUpdateDto containerDto)
            containerDto.ContainerLayout = ToContainerLayoutDto(block.ContainerLayout);
        return dto;
    }

    public static BlockAuthoringPolicyDto ToAuthoringDto(BlockAuthoringPolicyModel? value) => new()
    {
        SchemaVersion = Math.Max(value?.SchemaVersion ?? 0, 1),
        ContentLocked = value?.ContentLocked ?? false,
        GeometryLocked = value?.GeometryLocked ?? false,
        FullLocked = false,
        PresetSlotName = value?.PresetSlotName,
        PresetSourceId = value?.PresetSourceId
    };

    public static BlockAppearanceDto ToAppearanceDto(BlockAppearanceModel? appearance) => new()
    {
        SchemaVersion = Math.Max(appearance?.SchemaVersion ?? 0, 2),
        BackgroundMode = appearance?.BackgroundMode ?? "none",
        BackgroundColor = appearance?.BackgroundColor,
        TextColor = appearance?.TextColor,
        TextAlign = appearance?.TextAlign ?? "inherit",
        FontSizePx = appearance?.FontSizePx,
        FontWeight = appearance?.FontWeight,
        FontFamily = appearance?.FontFamily,
        LineHeight = appearance?.LineHeight,
        LetterSpacingPx = appearance?.LetterSpacingPx,
        Opacity = appearance?.Opacity ?? 1,
        BorderColor = appearance?.BorderColor,
        BorderWidth = appearance?.BorderWidth ?? 0,
        BorderStyle = appearance?.BorderStyle ?? "solid",
        BorderRadius = appearance?.BorderRadius ?? "none",
        Shadow = appearance?.Shadow ?? "none",
        Shape = appearance?.Shape ?? "rectangle",
        AspectRatio = appearance?.AspectRatio ?? "auto",
        RotationDeg = appearance?.RotationDeg ?? 0,
        MediaFit = appearance?.MediaFit ?? "cover",
        MediaPosition = appearance?.MediaPosition ?? "center",
        Padding = appearance?.Padding ?? "none",
        Margin = appearance?.Margin ?? "none",
        Decorative = appearance?.Decorative ?? false,
        InheritFromContainer = appearance?.InheritFromContainer ?? false
    };

    private static BlockAssetReferenceDto ToAssetDto(BlockAssetReferenceModel? value) => new()
    {
        SchemaVersion = 1,
        Url = value?.Url,
        ResourceId = value?.ResourceId,
        ResourceSource = value?.ResourceSource ?? "DirectUpload",
        StorageKey = value?.StorageKey,
        FileName = value?.FileName,
        ContentType = value?.ContentType,
        SizeBytes = value?.SizeBytes ?? 0
    };

    private static BlockResponsiveSettingsDto ToResponsiveDto(BlockResponsiveSettingsModel? responsive) => new()
    {
        SchemaVersion = 1,
        Tablet = ToResponsiveOverrideDto(responsive?.Tablet),
        Mobile = ToResponsiveOverrideDto(responsive?.Mobile)
    };

    private static BlockResponsiveOverrideDto? ToResponsiveOverrideDto(BlockResponsiveOverrideModel? value) =>
        value is null ? null : new BlockResponsiveOverrideDto
        {
            Mode = value.Mode,
            Width = value.Width,
            ColumnSpan = value.ColumnSpan,
            LeftPercent = value.LeftPercent,
            TopPx = value.TopPx,
            WidthPercent = value.WidthPercent,
            HeightPx = value.HeightPx
        };

    private static BlockAnimationSettingsDto ToAnimationDto(BlockAnimationSettingsModel? animation) => new()
    {
        SchemaVersion = Math.Max(animation?.SchemaVersion ?? 0, 1),
        Effect = animation?.Effect ?? "none",
        Trigger = animation?.Trigger ?? "enter-viewport",
        DurationMs = animation?.DurationMs ?? 500,
        DelayMs = animation?.DelayMs ?? 0,
        Easing = animation?.Easing ?? "ease-out",
        PlayOnce = animation?.PlayOnce ?? true,
        StaggerMs = animation?.StaggerMs ?? 0,
        ContinuousEffect = animation?.ContinuousEffect ?? "none",
        DisableForReducedMotion = true
    };

    private static ContainerLayoutSettingsDto ToContainerLayoutDto(ContainerLayoutSettingsModel? value) => new()
    {
        SchemaVersion = 2,
        Purpose = value?.Purpose ?? "composition",
        AllowedChildType = value?.Purpose == "collection" ? value.AllowedChildType : null,
        Mode = value?.Mode ?? "stack",
        Columns = Math.Clamp(value?.Columns ?? 2, 1, 6),
        Gap = value?.Gap ?? "medium",
        AlignItems = value?.AlignItems ?? "stretch",
        JustifyContent = value?.JustifyContent ?? "start",
        Wrap = value?.Wrap ?? true,
        OrbitRadius = Math.Clamp(value?.OrbitRadius ?? 180, 80, 480),
        OrbitStartAngle = Math.Clamp(value?.OrbitStartAngle ?? -90, -360, 360),
        OrbitEndAngle = Math.Clamp(value?.OrbitEndAngle ?? 270, -360, 360),
        OrbitDirection = value?.OrbitDirection ?? "clockwise",
        SemicircleRadius = Math.Clamp(value?.SemicircleRadius ?? 180, 80, 480),
        SemicircleStartAngle = Math.Clamp(value?.SemicircleStartAngle ?? 180, -360, 360),
        SemicircleEndAngle = Math.Clamp(value?.SemicircleEndAngle ?? 360, -360, 360),
        MobileMode = value?.MobileMode ?? "stack",
        CompactRadius = Math.Clamp(value?.CompactRadius ?? 120, 60, 220),
        CompactChildWidth = Math.Clamp(value?.CompactChildWidth ?? 120, 72, 180),
        GeometryLocked = value?.GeometryLocked ?? false,
        ShareAppearance = value?.ShareAppearance ?? false,
        SharedAppearance = value?.SharedAppearance is null ? null : ToAppearanceDto(value.SharedAppearance),
        Diagram = ToDiagramDto(value?.Diagram)
    };

    private static ContainerDiagramSettingsDto ToDiagramDto(ContainerDiagramSettingsModel? value) => new()
    {
        SchemaVersion = 1,
        Enabled = value?.Enabled ?? false,
        Decorations = (value?.Decorations ?? new()).Select(item => new ContainerDecorationSettingsDto
        {
            Id = item.Id,
            Kind = item.Kind,
            FromAnchor = item.FromAnchor,
            ToAnchor = item.ToAnchor,
            RadiusPercent = item.RadiusPercent,
            StartAngle = item.StartAngle,
            EndAngle = item.EndAngle,
            ColorMode = item.ColorMode,
            Color = item.Color,
            Width = item.Width,
            Style = item.Style,
            Opacity = item.Opacity
        }).ToList(),
        Connectors = (value?.Connectors ?? new()).Select(item => new ContainerConnectorSettingsDto
        {
            Id = item.Id,
            FromAnchor = item.FromAnchor,
            ToAnchor = item.ToAnchor,
            Routing = item.Routing,
            ColorMode = item.ColorMode,
            Color = item.Color,
            Width = item.Width,
            Style = item.Style,
            Opacity = item.Opacity,
            ArrowEnd = item.ArrowEnd
        }).ToList()
    };

    private static string NormalizeBlockZone(string? zone) =>
        string.IsNullOrWhiteSpace(zone) ? "default" : zone.Trim().ToLowerInvariant();

    private static string NormalizePositionMode(string? mode) =>
        string.Equals(mode, "freeform", StringComparison.OrdinalIgnoreCase) ? "freeform" : "flow";

    private static BlockLayoutDto ToLayoutDto(BlockLayoutModel? layout) => new()
    {
        Width = layout?.Width ?? "auto",
        ColumnSpan = Math.Clamp(layout?.ColumnSpan ?? 12, 1, 12),
        Align = layout?.Align ?? "stretch",
        Justify = layout?.Justify ?? "start",
        Padding = layout?.Padding ?? "none",
        Margin = layout?.Margin ?? "none",
        BackgroundColor = layout?.BackgroundColor,
        BorderRadius = layout?.BorderRadius ?? "none",
        ZIndex = Math.Clamp(layout?.ZIndex ?? 1, 0, 1000),
        X = Math.Clamp(layout?.X ?? 0, 0, 11),
        Y = Math.Clamp(layout?.Y ?? 0, 0, 60),
        W = Math.Clamp(layout?.W ?? 4, 1, 12),
        H = Math.Clamp(layout?.H ?? 2, 1, 40),
        LeftPercent = ClampDouble(layout?.LeftPercent, 0, 100),
        TopPx = ClampDouble(layout?.TopPx, 0, 10000),
        WidthPercent = ClampDouble(layout?.WidthPercent, 1, 100),
        HeightPx = ClampDouble(layout?.HeightPx, 24, 10000)
    };

    private static double? ClampDouble(double? value, double min, double max)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;

        return Math.Clamp(value.Value, min, max);
    }
}
