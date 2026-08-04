using Contracts.Icons;

namespace AdminSite.Models;

public static class IconReferenceModelMapper
{
    public static IconAppearanceModel? CloneAppearance(IconAppearanceModel? value) => value is null ? null : new()
    {
        ColorMode = value.ColorMode,
        ThemeRole = value.ThemeRole,
        Color = value.Color,
        Size = value.Size,
        BackgroundMode = value.BackgroundMode,
        BackgroundThemeRole = value.BackgroundThemeRole,
        BackgroundColor = value.BackgroundColor,
        Shape = value.Shape
    };

    public static IconAppearanceDto? ToDto(IconAppearanceModel? value) => value is null ? null : new()
    {
        ColorMode = value.ColorMode,
        ThemeRole = value.ThemeRole,
        Color = value.Color,
        Size = value.Size,
        BackgroundMode = value.BackgroundMode,
        BackgroundThemeRole = value.BackgroundThemeRole,
        BackgroundColor = value.BackgroundColor,
        Shape = value.Shape
    };
}
