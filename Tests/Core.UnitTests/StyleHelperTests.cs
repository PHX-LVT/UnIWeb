using Contracts.Public;
using SharedComponents.Helpers;

namespace Core.UnitTests;

public sealed class StyleHelperTests
{
    [Fact]
    public void GetSectionStyle_EmitsThemeHeightTextAndPosition()
    {
        var style = StyleHelper.GetSectionStyle(new PublicSectionStyleDto
        {
            BackgroundType = "theme",
            Height = "custom",
            CustomMinHeightPx = 9_000,
            TextColor = "light"
        });

        Assert.Contains("var(--theme-color-background)", style);
        Assert.Contains("min-height: 3000px", style);
        Assert.Contains("color: #ffffff", style);
        Assert.Contains("position: relative", style);
    }

    [Fact]
    public void GetSectionStyle_NormalizesImageFitAndPosition()
    {
        var style = StyleHelper.GetSectionStyle(new PublicSectionStyleDto
        {
            BackgroundType = "image",
            BackgroundImageUrl = "/assets/hero.jpg",
            BackgroundImageFit = "invalid",
            BackgroundImagePosition = "right"
        });

        Assert.Contains("background-size: cover", style);
        Assert.Contains("background-position: right center", style);
        Assert.Contains("background-repeat: no-repeat", style);
    }

    [Fact]
    public void GetSectionClass_MapsLayoutOptions()
    {
        var classes = StyleHelper.GetSectionClass(new PublicSectionStyleDto
        {
            Padding = "large",
            ContentWidth = "narrow",
            MobileLayout = "hide"
        });

        Assert.Equal("sc-section sc-pad-lg sc-width-narrow sc-mobile-hide", classes);
    }

    [Theory]
    [InlineData(0, "display: none")]
    [InlineData(-1, "display: none")]
    public void GetOverlayStyle_HidesNonPositiveOpacity(double opacity, string expected)
    {
        var style = StyleHelper.GetOverlayStyle(new PublicSectionStyleDto
        {
            OverlayColor = "#000000",
            OverlayOpacity = opacity
        });

        Assert.Equal(expected, style);
    }

    [Fact]
    public void GetOverlayStyle_MakesVisibleOverlayNonInteractive()
    {
        var style = StyleHelper.GetOverlayStyle(new PublicSectionStyleDto
        {
            OverlayColor = "#000000",
            OverlayOpacity = .4
        });

        Assert.Contains("opacity: 0.4", style);
        Assert.Contains("pointer-events: none", style);
    }

    [Theory]
    [InlineData("modal:contact", true)]
    [InlineData("#quote", true)]
    [InlineData("/contact", false)]
    public void IsModalAction_RecognizesReservedActions(string href, bool expected) =>
        Assert.Equal(expected, StyleHelper.IsModalAction(href));

    [Fact]
    public void Lang_UsesRequestedThenFallbackLanguage()
    {
        StyleHelper.SetFallbackLanguage("en");
        var values = new Dictionary<string, string> { ["en"] = "Hello", ["vi"] = "Xin chao" };

        Assert.Equal("Xin chao", StyleHelper.Lang(values, "vi"));
        Assert.Equal("Hello", StyleHelper.Lang(values, "cn"));
    }
}
