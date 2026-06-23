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
                ImageUrl = block.ImageUrl,
                AltText = block.AltText ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "video" => new VideoBlockUpdateDto
            {
                EmbedUrl = block.EmbedUrl ?? string.Empty,
                Title = block.Title ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "file" => new FileBlockUpdateDto
            {
                FileUrl = block.FileUrl,
                Filename = block.FileName ?? string.Empty,
                FileType = block.FileType ?? string.Empty,
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
                    Href = p.Href
                }).ToList() ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "form" => new FormBlockUpdateDto
            {
                FormDefinitionId = block.FormDefinitionId,
                SubmitButtonLabel = block.SubmitButtonLabel ?? new(),
                Fields = block.Fields?.Select(f => new FormFieldDto
                {
                    Name = f.Name ?? f.Id,
                    Type = f.FieldType ?? "text",
                    Label = f.Label ?? new(),
                    Required = f.Required,
                    Options = f.Options,
                    Order = f.Order
                }).ToList() ?? new(),
                Visible = block.Visible,
                Layout = layout
            },
            "card" => new CardBlockUpdateDto
            {
                Icon = block.Icon ?? string.Empty,
                Title = block.Title ?? new(),
                Description = block.Description ?? new(),
                ImageUrl = block.ImageUrl,
                ButtonLabel = block.ButtonLabel ?? new(),
                Href = block.Href,
                Visible = block.Visible,
                Layout = layout
            },
            "button" => new ButtonBlockUpdateDto
            {
                Label = block.Label ?? new(),
                Href = block.Href,
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
                Visible = block.Visible,
                Layout = layout
            },
            "container" => new ContainerBlockUpdateDto
            {
                Title = block.Title ?? new(),
                LayoutMode = block.LayoutMode ?? "stack",
                Columns = Math.Clamp(block.Columns ?? 2, 1, 6),
                Gap = block.Gap ?? "medium",
                OrbitRadius = Math.Clamp(block.OrbitRadius ?? 180, 80, 480),
                OrbitStartAngle = Math.Clamp(block.OrbitStartAngle ?? -90, -360, 360),
                SemicircleRadius = Math.Clamp(block.SemicircleRadius ?? 180, 80, 480),
                SemicircleStartAngle = Math.Clamp(block.SemicircleStartAngle ?? 180, -360, 360),
                SemicircleEndAngle = Math.Clamp(block.SemicircleEndAngle ?? 360, -360, 360),
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
        dto.Layout = layout;
        dto.BlockZone = NormalizeBlockZone(block.BlockZone);
        dto.PositionMode = NormalizePositionMode(block.PositionMode);
        dto.ParentBlockId = block.ParentBlockId;
        return dto;
    }

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
