namespace Contracts.Admin;

public sealed record FormationSlotGeometry(
    string SlotKey,
    double XPercent,
    double YPercent,
    int ItemWidthPx,
    int ItemHeightPx);

public sealed record FormationGeometry(
    int WidthPx,
    int HeightPx,
    int ItemWidthPx,
    int ItemHeightPx,
    IReadOnlyList<FormationSlotGeometry> Slots);

public static class FormationGeometryResolver
{
    public static FormationGeometry Resolve(
        string presetKey,
        string? sizeMode,
        int? customWidthPx,
        string? itemSize,
        string? spacing,
        bool mobile = false)
    {
        var preset = ContainerPresetCatalog.ForFormation(presetKey);
        var width = ResolveWidth(preset, sizeMode, customWidthPx);
        var height = mobile
            ? ResolveMobileHeight(preset, width)
            : Math.Max(240, (int)Math.Round(width / Math.Max(0.5, preset.AspectRatio)));
        var itemWidth = ResolveItemWidth(preset, itemSize, width, mobile);
        var itemHeight = Math.Max(72, (int)Math.Round(itemWidth * 0.72));
        var spacingFactor = spacing switch
        {
            "compact" => 0.82,
            "wide" => 1.12,
            _ => 1d
        };

        var slots = preset.Slots.Select(slot =>
        {
            var sourceX = mobile ? slot.MobileXPercent : slot.XPercent;
            var sourceY = mobile ? slot.MobileYPercent : slot.YPercent;
            var x = Math.Clamp(50d + (sourceX - 50d) * spacingFactor, 5d, 95d);
            var y = Math.Clamp(50d + (sourceY - 50d) * spacingFactor, 5d, 95d);
            return new FormationSlotGeometry(slot.Key, x, y, itemWidth, itemHeight);
        }).ToArray();

        return new FormationGeometry(width, height, itemWidth, itemHeight, slots);
    }

    public static int ResolveWidth(
        ContainerPresetDefinition preset,
        string? sizeMode,
        int? customWidthPx) => sizeMode switch
    {
        "small" => preset.MinimumWidthPx,
        "large" => Math.Clamp((int)Math.Round(preset.DefaultWidthPx * 1.25), preset.MinimumWidthPx, preset.MaximumWidthPx),
        "custom" => Math.Clamp(customWidthPx ?? preset.DefaultWidthPx, preset.MinimumWidthPx, preset.MaximumWidthPx),
        _ => preset.DefaultWidthPx
    };

    private static int ResolveItemWidth(
        ContainerPresetDefinition preset,
        string? itemSize,
        int formationWidth,
        bool mobile)
    {
        var factor = itemSize switch
        {
            "compact" => 0.8,
            "large" => 1.2,
            _ => 1d
        };
        if (mobile) factor *= 0.82;
        var requested = (int)Math.Round(preset.DefaultItemWidthPx * factor);
        return Math.Clamp(requested, 84, Math.Max(84, formationWidth / 3));
    }

    private static int ResolveMobileHeight(ContainerPresetDefinition preset, int width) =>
        preset.FormationKind switch
        {
            "process" => Math.Max(560, (int)Math.Round(width * 1.7)),
            "zigzag" => Math.Max(520, (int)Math.Round(width * 1.5)),
            "semicircle" => Math.Max(520, (int)Math.Round(width * 1.45)),
            _ => Math.Max(360, width)
        };
}
