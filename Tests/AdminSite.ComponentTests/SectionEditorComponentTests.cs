using AdminSite.Components.Pages.SectionEditors;
using AdminSite.Models;
using Bunit;
using Contracts.Public;
using SharedComponents.Sections;

namespace AdminSite.ComponentTests;

public sealed class SectionEditorComponentTests : BunitContext
{
    [Fact]
    public async Task ContentEditSession_RegistersOnceAndRunsTheUnifiedSaveCommand()
    {
        var changes = 0;
        var saves = 0;
        var session = new SectionContentEditSession();
        session.Changed += () => changes++;
        Task Save() { saves++; session.MarkSaved(); return Task.CompletedTask; }

        session.Register(Save);
        session.Register(Save);
        session.MarkDirty();
        await session.SaveAsync();

        Assert.Equal(1, saves);
        Assert.False(session.IsDirty);
        Assert.False(session.IsSaving);
        Assert.True(session.CanSave);
        Assert.InRange(changes, 4, 5);
    }

    [Fact]
    public void SegmentField_MarksCurrentOptionAndRaisesChangedValue()
    {
        string? selected = null;
        var options = new SectionDesignTab.DesignOption[]
        {
            new("stack", "Stack"),
            new("grid", "Grid", "fa-grid")
        };
        var cut = Render<SegmentField>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Label, "Layout")
            .Add(component => component.Value, "stack")
            .Add(component => component.Options, options)
            .Add(component => component.ValueChanged, value => selected = value));

        var buttons = cut.FindAll("button");
        Assert.Equal(2, buttons.Count);
        Assert.Contains("active", buttons[0].ClassList);
        Assert.DoesNotContain("active", buttons[1].ClassList);

        buttons[1].Click();

        Assert.Equal("grid", selected);
    }

    [Fact]
    public void ArrangementEditor_ShowsColumnsOnlyForGridAndClampsChanges()
    {
        var changes = 0;
        var draft = new SectionStyleModel
        {
            BlockLayoutMode = "grid",
            BlockGridColumns = 4,
            BlockGap = "medium"
        };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft)
            .Add(component => component.Changed, () => changes++));

        var input = cut.Find("input[type=number]");
        Assert.Equal("4", input.GetAttribute("value"));

        input.Change("99");

        Assert.Equal(12, draft.BlockGridColumns);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ArrangementEditor_SegmentUpdatesDraftAndNotifiesParent()
    {
        var changes = 0;
        var draft = new SectionStyleModel
        {
            BlockLayoutMode = "stack",
            BlockGridColumns = 12,
            BlockGap = "medium"
        };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft)
            .Add(component => component.Changed, () => changes++));

        cut.FindAll("button.section-design-segment")
            .Single(button => button.TextContent.Trim() == "Grid")
            .Click();

        Assert.Equal("grid", draft.BlockLayoutMode);
        Assert.Equal(1, changes);
        Assert.NotEmpty(cut.FindAll("input[type=number]"));
    }

    [Fact]
    public void ArrangementEditor_CollapsesWithoutMutatingDraft()
    {
        var draft = new SectionStyleModel { BlockLayoutMode = "stack" };
        var cut = Render<SectionBlockArrangementEditor>(parameters => parameters
            .AddCascadingValue("AdminLanguage", "en")
            .Add(component => component.Draft, draft));

        cut.Find("button.section-design-group__toggle").Click();

        Assert.Empty(cut.FindAll(".section-design-group__body"));
        Assert.Equal("stack", draft.BlockLayoutMode);
    }

    [Fact]
    public void MediaCropEditor_NormalizesPlacementAndClearsDisabledMobileValues()
    {
        var normalized = MediaCropEditor.Normalize(new MediaPlacementModel
        {
            Fit = "invalid",
            FocalPointX = -10,
            FocalPointY = 110,
            Zoom = 5,
            UseMobileOverride = false,
            MobileFocalPointX = 20,
            MobileFocalPointY = 30,
            MobileZoom = 2
        });

        Assert.Equal("cover", normalized.Fit);
        Assert.Equal(0, normalized.FocalPointX);
        Assert.Equal(100, normalized.FocalPointY);
        Assert.Equal(5, normalized.Zoom);
        Assert.Null(normalized.MobileFocalPointX);
        Assert.Null(normalized.MobileFocalPointY);
        Assert.Null(normalized.MobileZoom);
    }

    [Theory]
    [InlineData(3840, 2160, 1.7777777778, 1920, 1080, 2)]
    [InlineData(3840, 2160, 1.6, 640, 400, 5.4)]
    [InlineData(300, 400, 1.7777777778, 800, 450, 1)]
    public void MediaCropEditor_CalculatesZoomFromUsableSourcePixels(
        int sourceWidth,
        int sourceHeight,
        double aspect,
        int targetWidth,
        int targetHeight,
        double expected)
    {
        var maximum = MediaCropEditor.CalculateQualitySafeZoom(
            sourceWidth,
            sourceHeight,
            aspect,
            targetWidth,
            targetHeight);

        Assert.Equal(expected, maximum, 2);
    }

    [Fact]
    public void MediaCropEditor_SeparatesRecommendedAndHardZoomLimits()
    {
        var safe = MediaCropEditor.CalculateResolutionZoomLimit(1920, 1080, 4d / 3d, 960, 720);
        var maximum = MediaCropEditor.CalculateResolutionZoomLimit(1920, 1080, 4d / 3d, 480, 360);

        Assert.Equal(1.5, safe, 2);
        Assert.Equal(3, maximum, 2);
    }

    [Fact]
    public void MediaCropEditor_LowResolutionImageStillGetsAConstrainedZoomRange()
    {
        var maximum = MediaCropEditor.CalculateResolutionZoomLimit(300, 400, 4d / 3d, 240, 180);

        Assert.Equal(1.25, maximum, 2);
    }

    [Fact]
    public void HeroRenderer_EmitsPlacementVariablesOnImage()
    {
        var section = new PublicHeroSectionDto
        {
            Id = "hero-1",
            Type = "hero",
            Visible = true,
            Style = new PublicSectionStyleDto(),
            ImageUrl = "/assets/banner.jpg",
            ImagePlacement = new PublicMediaPlacementDto
            {
                FocalPointX = 25,
                FocalPointY = 75,
                Zoom = 1.5,
                UseMobileOverride = true,
                MobileFocalPointX = 60,
                MobileFocalPointY = 35,
                MobileZoom = 1.2
            }
        };

        var cut = Render<HeroSection>(parameters => parameters
            .Add(component => component.Section, section));

        var style = cut.Find(".sc-hero__image img").GetAttribute("style");
        Assert.Contains("--sc-media-x:25%", style);
        Assert.Contains("--sc-media-y:75%", style);
        Assert.Contains("--sc-media-zoom:1.5", style);
        Assert.Contains("--sc-media-mobile-x:60%", style);
    }

    [Fact]
    public void BackgroundMedia_RendersPlacedImageInsteadOfLegacyCssBackground()
    {
        var style = new PublicSectionStyleDto
        {
            BackgroundType = "image",
            BackgroundImageUrl = "/assets/background.jpg",
            BackgroundImagePlacement = new PublicMediaPlacementDto { FocalPointX = 10, FocalPointY = 90 }
        };

        var cut = Render<SectionBackgroundMedia>(parameters => parameters
            .Add(component => component.Style, style));

        var image = cut.Find("img.sc-section-bg-image");
        Assert.Equal("/assets/background.jpg", image.GetAttribute("src"));
        Assert.Contains("--sc-media-x:10%", image.GetAttribute("style"));
        Assert.Contains("--sc-media-y:90%", image.GetAttribute("style"));
    }

    [Fact]
    public void LibraryRenderer_AppliesContentThumbnailPlacementToCardImage()
    {
        var section = new PublicLibrarySectionDto
        {
            Id = "library-1",
            Type = "library",
            Visible = true,
            Style = new PublicSectionStyleDto(),
            ShowImage = true,
            Items =
            [
                new PublicLibraryItemDto
                {
                    Id = "content-1",
                    Slug = "placed-image",
                    Title = new() { ["en"] = "Placed image" },
                    ThumbnailUrl = "/assets/thumb.jpg",
                    ThumbnailPlacement = new PublicMediaPlacementDto
                    {
                        FocalPointX = 18,
                        FocalPointY = 72,
                        Zoom = 1.6
                    }
                }
            ]
        };

        var cut = Render<LibrarySection>(parameters => parameters
            .Add(component => component.Section, section));

        var style = cut.Find(".sc-library__media").GetAttribute("style");
        Assert.Contains("--sc-media-x:18%", style);
        Assert.Contains("--sc-media-y:72%", style);
        Assert.Contains("--sc-media-zoom:1.6", style);
    }
}
