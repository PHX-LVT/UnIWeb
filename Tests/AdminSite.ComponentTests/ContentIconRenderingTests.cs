using Bunit;
using Contracts.Icons;
using SharedComponents;

namespace AdminSite.ComponentTests;

public sealed class ContentIconRenderingTests : BunitContext
{
    [Fact]
    public void BuiltInIcon_RendersCanonicalFontAwesomeClass()
    {
        var cut = Render<ContentIcon>(parameters => parameters
            .Add(component => component.Icon, new PublicIconReferenceDto
            {
                Source = IconSources.BuiltIn,
                ClassName = "fa-solid fa-truck"
            }));

        Assert.Contains("fa-solid", cut.Find("i").ClassList);
        Assert.Contains("fa-truck", cut.Find("i").ClassList);
        Assert.Empty(cut.FindAll("img"));
    }

    [Fact]
    public void CustomIcon_RendersImageWithLocalizedAltText()
    {
        var cut = Render<ContentIcon>(parameters => parameters
            .Add(component => component.Icon, new PublicIconReferenceDto
            {
                Source = IconSources.Custom,
                Url = "https://cdn.example.test/custom.svg",
                AltText = new() { ["en"] = "Custom logistics mark" }
            })
            .Add(component => component.Lang, "en"));

        var image = cut.Find("img");
        Assert.Equal("https://cdn.example.test/custom.svg", image.GetAttribute("src"));
        Assert.Equal("Custom logistics mark", image.GetAttribute("alt"));
        Assert.Empty(cut.FindAll("i"));
    }
}
