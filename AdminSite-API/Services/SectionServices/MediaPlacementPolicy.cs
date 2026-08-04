using Contracts.Admin;
using Contracts.Public;
using FullProject.Models;

namespace FullProject.Services.SectionServices;

public static class MediaPlacementPolicy
{
    public const double MinimumZoom = 1;
    public const double MaximumZoom = 20;

    public static MediaPlacement Normalize(MediaPlacementDto value) => Normalize(new MediaPlacement
    {
        Fit = value.Fit,
        FocalPointX = value.FocalPointX,
        FocalPointY = value.FocalPointY,
        Zoom = value.Zoom,
        UseMobileOverride = value.UseMobileOverride,
        MobileFocalPointX = value.MobileFocalPointX,
        MobileFocalPointY = value.MobileFocalPointY,
        MobileZoom = value.MobileZoom
    });

    public static MediaPlacement Normalize(MediaPlacement? value)
    {
        value ??= new MediaPlacement();
        var useMobile = value.UseMobileOverride;
        return new MediaPlacement
        {
            Fit = NormalizeFit(value.Fit),
            FocalPointX = Coordinate(value.FocalPointX, 50),
            FocalPointY = Coordinate(value.FocalPointY, 50),
            Zoom = Zoom(value.Zoom, 1),
            UseMobileOverride = useMobile,
            MobileFocalPointX = useMobile ? Coordinate(value.MobileFocalPointX, value.FocalPointX) : null,
            MobileFocalPointY = useMobile ? Coordinate(value.MobileFocalPointY, value.FocalPointY) : null,
            MobileZoom = useMobile ? Zoom(value.MobileZoom, value.Zoom) : null
        };
    }

    public static MediaPlacementDto? ToAdmin(MediaPlacement? value)
    {
        if (value is null) return null;
        var normalized = Normalize(value);
        return new MediaPlacementDto
        {
            Fit = normalized.Fit,
            FocalPointX = normalized.FocalPointX,
            FocalPointY = normalized.FocalPointY,
            Zoom = normalized.Zoom,
            UseMobileOverride = normalized.UseMobileOverride,
            MobileFocalPointX = normalized.MobileFocalPointX,
            MobileFocalPointY = normalized.MobileFocalPointY,
            MobileZoom = normalized.MobileZoom
        };
    }

    public static PublicMediaPlacementDto? ToPublic(MediaPlacement? value)
    {
        if (value is null) return null;
        var normalized = Normalize(value);
        return new PublicMediaPlacementDto
        {
            Fit = normalized.Fit,
            FocalPointX = normalized.FocalPointX,
            FocalPointY = normalized.FocalPointY,
            Zoom = normalized.Zoom,
            UseMobileOverride = normalized.UseMobileOverride,
            MobileFocalPointX = normalized.MobileFocalPointX,
            MobileFocalPointY = normalized.MobileFocalPointY,
            MobileZoom = normalized.MobileZoom
        };
    }

    public static PublicMediaPlacementDto LegacyPublic(string? fit, string? position)
    {
        var (x, y) = LegacyCoordinates(position);
        return new PublicMediaPlacementDto
        {
            Fit = NormalizeFit(fit),
            FocalPointX = x,
            FocalPointY = y,
            Zoom = 1
        };
    }

    public static (double X, double Y) LegacyCoordinates(string? position) => position?.Trim().ToLowerInvariant() switch
    {
        "top" => (50, 0),
        "bottom" => (50, 100),
        "left" => (0, 50),
        "right" => (100, 50),
        _ => (50, 50)
    };

    public static string NormalizeFit(string? value) =>
        string.Equals(value, "contain", StringComparison.OrdinalIgnoreCase) ? "contain" : "cover";

    private static double Coordinate(double? value, double fallback)
    {
        var candidate = value is { } number && double.IsFinite(number) ? number : fallback;
        if (!double.IsFinite(candidate)) candidate = 50;
        return Math.Clamp(candidate, 0, 100);
    }

    private static double Zoom(double? value, double fallback)
    {
        var candidate = value is { } number && double.IsFinite(number) ? number : fallback;
        if (!double.IsFinite(candidate)) candidate = 1;
        return Math.Clamp(candidate, MinimumZoom, MaximumZoom);
    }
}
