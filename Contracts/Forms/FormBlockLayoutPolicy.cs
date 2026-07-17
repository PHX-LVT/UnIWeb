namespace Contracts.Forms;

public readonly record struct FormBlockDefaultSize(
    int WidthPx,
    int WidthUnits,
    double WidthPercent,
    int HeightPx);

public static class FormBlockLayoutPolicy
{
    public const double MinimumScale = 0.5d;
    public const double MaximumScale = 1d;
    public const int NormalContentWidthPx = 1400;
    public const int NarrowContentWidthPx = 720;
    public const int FullContentWidthPx = 1440;
    public const int MaximumHeightPx = 1800;
    public const int MaximumSectionHeightPx = 3000;
    public const int SectionBottomPaddingPx = 40;

    public static FormBlockDefaultSize CalculateDefaultSize(
        FormDesignSettingsDto design,
        int availableWidthPx = NormalContentWidthPx)
    {
        var safeAvailableWidthPx = Math.Max(1, availableWidthPx);
        var designWidthPx = design.V2 is null
            ? Math.Clamp(
                design.WidthPx,
                FormDesignPolicy.MinimumWidth(design.Shape),
                FormDesignPolicy.MaximumWidth(design.Shape))
            : Math.Clamp(
                design.WidthPx,
                FormDesignV2Policy.MinimumWidth(design.V2.OuterLayout),
                FormDesignV2Policy.MaximumWidth(design.V2.OuterLayout));
        var widthPx = Math.Min(designWidthPx, safeAvailableWidthPx);
        var widthPercent = Math.Clamp(widthPx / (double)safeAvailableWidthPx * 100d, 1d, 100d);
        var widthUnits = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);
        return new FormBlockDefaultSize(
            widthPx,
            widthUnits,
            widthPercent,
            Math.Clamp(design.CalculatedHeightPx, FormDesignPolicy.MinimumHeightPx, MaximumHeightPx));
    }

    public static int AvailableContentWidthPx(string? contentWidth) =>
        contentWidth?.Trim().ToLowerInvariant() switch
        {
            "narrow" => NarrowContentWidthPx,
            "full" => FullContentWidthPx,
            _ => NormalContentWidthPx
        };

}
