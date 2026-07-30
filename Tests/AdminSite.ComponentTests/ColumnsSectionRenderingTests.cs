using Bunit;
using Contracts.Public;
using SharedComponents.Sections;

namespace AdminSite.ComponentTests;

public sealed class ColumnsSectionRenderingTests : BunitContext
{
    [Fact]
    public void SplitMode_RendersStructuredLeftContentAndOneRightAuthoringZone()
    {
        var section = new PublicColumnsSectionDto
        {
            Id = "section-1",
            LayoutMode = "split",
            Eyebrow = new() { ["en"] = "Capabilities" },
            Heading = new() { ["en"] = "One section, two roles" },
            Subheading = new() { ["en"] = "Structured content" },
            Content = new() { ["en"] = "First line\nSecond line" },
            ColumnSlots = new()
            {
                new PublicColumnSlotDto { Id = "right-zone", Order = 0 }
            }
        };

        var cut = Render<ColumnsSection>(parameters => parameters
            .Add(component => component.Section, section)
            .Add(component => component.Lang, "en")
            .Add(component => component.ShowInvisible, true));

        Assert.Equal("Capabilities", cut.Find(".sc-split-section__eyebrow").TextContent);
        Assert.Equal("One section, two roles", cut.Find(".sc-split-section__heading").TextContent);
        Assert.Equal("First line\nSecond line", cut.Find(".sc-split-section__body").TextContent);
        Assert.Single(cut.FindAll("[data-authoring-zone='true']"));
        Assert.Equal("right-zone", cut.Find("[data-authoring-zone='true']").GetAttribute("data-block-zone"));
        Assert.Empty(cut.FindAll(".sc-columns"));
    }

    [Fact]
    public void LegacyMode_PreservesEveryExistingColumnSlot()
    {
        var section = new PublicColumnsSectionDto
        {
            Id = "section-legacy",
            LayoutMode = "legacy",
            ColumnRatio = "equal",
            Gap = "small",
            ColumnSlots = new()
            {
                new PublicColumnSlotDto { Id = "left-zone", Order = 0 },
                new PublicColumnSlotDto { Id = "right-zone", Order = 1 }
            }
        };

        var cut = Render<ColumnsSection>(parameters => parameters
            .Add(component => component.Section, section)
            .Add(component => component.ShowInvisible, true));

        Assert.Equal(2, cut.FindAll("[data-authoring-zone='true']").Count);
        Assert.NotEmpty(cut.FindAll(".sc-columns--gap-small"));
        Assert.Empty(cut.FindAll(".sc-split-section"));
    }
}
