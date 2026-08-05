using Contracts.Admin;
using Contracts.Public;

namespace AdminSite.Models;

public static class MediaPlacementModelMapper
{
    public static MediaPlacementModel? Clone(MediaPlacementModel? value) => value is null ? null : new MediaPlacementModel
    {
        Fit = value.Fit,
        FocalPointX = value.FocalPointX,
        FocalPointY = value.FocalPointY,
        Zoom = value.Zoom,
        UseMobileOverride = value.UseMobileOverride,
        MobileFocalPointX = value.MobileFocalPointX,
        MobileFocalPointY = value.MobileFocalPointY,
        MobileZoom = value.MobileZoom
    };

    public static MediaPlacementDto? ToAdmin(MediaPlacementModel? value) => value is null ? null : new MediaPlacementDto
    {
        Fit = value.Fit,
        FocalPointX = value.FocalPointX,
        FocalPointY = value.FocalPointY,
        Zoom = value.Zoom,
        UseMobileOverride = value.UseMobileOverride,
        MobileFocalPointX = value.MobileFocalPointX,
        MobileFocalPointY = value.MobileFocalPointY,
        MobileZoom = value.MobileZoom
    };

    public static PublicMediaPlacementDto? ToPublic(MediaPlacementModel? value) => value is null ? null : new PublicMediaPlacementDto
    {
        Fit = value.Fit,
        FocalPointX = value.FocalPointX,
        FocalPointY = value.FocalPointY,
        Zoom = value.Zoom,
        UseMobileOverride = value.UseMobileOverride,
        MobileFocalPointX = value.MobileFocalPointX,
        MobileFocalPointY = value.MobileFocalPointY,
        MobileZoom = value.MobileZoom
    };
}
