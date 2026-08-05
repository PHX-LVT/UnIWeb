using Contracts.Admin;
using Contracts.Public;
using FullProject.Models;
using FullProject.Services.SectionServices;
using SharedComponents.Helpers;
using System.Text.Json;

namespace Core.UnitTests;

public sealed class MediaPlacementTests
{
    [Fact]
    public void Normalize_ClampsDesktopAndMobileValues()
    {
        var placement = MediaPlacementPolicy.Normalize(new MediaPlacementDto
        {
            Fit = "unexpected",
            FocalPointX = -40,
            FocalPointY = 140,
            Zoom = 9,
            UseMobileOverride = true,
            MobileFocalPointX = 125,
            MobileFocalPointY = -5,
            MobileZoom = .25
        });

        Assert.Equal("cover", placement.Fit);
        Assert.Equal(0, placement.FocalPointX);
        Assert.Equal(100, placement.FocalPointY);
        Assert.Equal(9, placement.Zoom);
        Assert.Equal(100, placement.MobileFocalPointX);
        Assert.Equal(0, placement.MobileFocalPointY);
        Assert.Equal(1, placement.MobileZoom);
    }

    [Fact]
    public void Normalize_RemovesInactiveMobileOverrideValues()
    {
        var placement = MediaPlacementPolicy.Normalize(new MediaPlacement
        {
            UseMobileOverride = false,
            MobileFocalPointX = 10,
            MobileFocalPointY = 20,
            MobileZoom = 2
        });

        Assert.Null(placement.MobileFocalPointX);
        Assert.Null(placement.MobileFocalPointY);
        Assert.Null(placement.MobileZoom);
    }

    [Theory]
    [InlineData("center", 50, 50)]
    [InlineData("top", 50, 0)]
    [InlineData("bottom", 50, 100)]
    [InlineData("left", 0, 50)]
    [InlineData("right", 100, 50)]
    [InlineData("invalid", 50, 50)]
    public void LegacyCoordinates_MapsExistingBackgroundPositions(string value, double x, double y)
    {
        Assert.Equal((x, y), MediaPlacementPolicy.LegacyCoordinates(value));
    }

    [Fact]
    public void GetSectionStyle_LeavesImageRenderingToBackgroundMedia_WhenPlacementExists()
    {
        var style = StyleHelper.GetSectionStyle(new PublicSectionStyleDto
        {
            BackgroundType = "image",
            BackgroundImageUrl = "/assets/banner.jpg",
            BackgroundImagePlacement = new PublicMediaPlacementDto()
        });

        Assert.DoesNotContain("background-image", style);
        Assert.Contains("position: relative", style);
    }

    [Fact]
    public void GetMediaPlacementStyle_EmitsResponsiveCssVariables()
    {
        var style = StyleHelper.GetMediaPlacementStyle(new PublicMediaPlacementDto
        {
            Fit = "contain",
            FocalPointX = 32,
            FocalPointY = 68,
            Zoom = 1.4,
            UseMobileOverride = true,
            MobileFocalPointX = 70,
            MobileFocalPointY = 20,
            MobileZoom = 1.8
        });

        Assert.Contains("--sc-media-fit:contain", style);
        Assert.Contains("--sc-media-x:32%", style);
        Assert.Contains("--sc-media-y:68%", style);
        Assert.Contains("--sc-media-zoom:1.4", style);
        Assert.Contains("--sc-media-mobile-x:70%", style);
        Assert.Contains("--sc-media-mobile-y:20%", style);
        Assert.Contains("--sc-media-mobile-zoom:1.8", style);
    }

    [Fact]
    public void HeroUpdateContract_RoundTripsPlacementMetadata()
    {
        SectionUpdateDto source = new HeroSectionUpdateDto
        {
            ImageUrl = "/assets/banner.jpg",
            ImagePlacement = new MediaPlacementDto
            {
                FocalPointX = 22,
                FocalPointY = 71,
                Zoom = 1.35,
                UseMobileOverride = true,
                MobileFocalPointX = 64,
                MobileFocalPointY = 30,
                MobileZoom = 1.1
            }
        };

        var json = JsonSerializer.Serialize(source);
        var restored = Assert.IsType<HeroSectionUpdateDto>(JsonSerializer.Deserialize<SectionUpdateDto>(json));

        Assert.Equal("/assets/banner.jpg", restored.ImageUrl);
        Assert.NotNull(restored.ImagePlacement);
        Assert.Equal(22, restored.ImagePlacement.FocalPointX);
        Assert.Equal(71, restored.ImagePlacement.FocalPointY);
        Assert.Equal(1.35, restored.ImagePlacement.Zoom);
        Assert.True(restored.ImagePlacement.UseMobileOverride);
        Assert.Equal(64, restored.ImagePlacement.MobileFocalPointX);
    }

    [Fact]
    public void SectionItemAndImageBlockContracts_RoundTripPlacementMetadata()
    {
        var listItem = new ListItemDto
        {
            ImageUrl = "/assets/card.jpg",
            ImagePlacement = new MediaPlacementDto { FocalPointX = 18, FocalPointY = 72, Zoom = 1.6 }
        };
        var imageBlock = new ImageBlockUpdateDto
        {
            ImagePlacement = new MediaPlacementDto { Fit = "contain", FocalPointX = 36, Zoom = 2.1 }
        };

        var restoredItem = JsonSerializer.Deserialize<ListItemDto>(JsonSerializer.Serialize(listItem));
        var restoredBlock = JsonSerializer.Deserialize<ImageBlockUpdateDto>(JsonSerializer.Serialize(imageBlock));

        Assert.Equal(18, restoredItem?.ImagePlacement?.FocalPointX);
        Assert.Equal(1.6, restoredItem?.ImagePlacement?.Zoom);
        Assert.Equal("contain", restoredBlock?.ImagePlacement?.Fit);
        Assert.Equal(2.1, restoredBlock?.ImagePlacement?.Zoom);
    }

    [Fact]
    public void ContentMapping_RoundTripsIndependentHeroThumbnailAndGalleryPlacements()
    {
        var source = new ContentItem
        {
            HeroImageUrl = "/assets/hero.jpg",
            HeroImagePlacement = new MediaPlacement { FocalPointX = 22, Zoom = 1.8 },
            ThumbnailUrl = "/assets/thumb.jpg",
            ThumbnailPlacement = new MediaPlacement { FocalPointY = 74, Zoom = 2.2 },
            GalleryItems =
            [
                new ContentGalleryItem
                {
                    Url = "/assets/gallery.jpg",
                    ThumbnailUrl = "/assets/gallery-thumb.jpg",
                    ThumbnailPlacement = new MediaPlacement { FocalPointX = 80, Zoom = 1.4 }
                }
            ]
        };

        var mapped = new FullProject.Services.ContentMappingService().MapItem(source);

        Assert.Equal(22, mapped.HeroImagePlacement?.FocalPointX);
        Assert.Equal(1.8, mapped.HeroImagePlacement?.Zoom);
        Assert.Equal(74, mapped.ThumbnailPlacement?.FocalPointY);
        Assert.Equal(2.2, mapped.ThumbnailPlacement?.Zoom);
        Assert.Equal(80, mapped.GalleryItems.Single().ThumbnailPlacement?.FocalPointX);
    }
}
