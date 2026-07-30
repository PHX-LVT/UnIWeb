using Bunit;
using Contracts.Admin;
using Contracts.Public;
using SharedComponents.Blocks;

namespace AdminSite.ComponentTests;

public sealed class FormationRenderingTests : BunitContext
{
    [Fact]
    public void Formation_RendersEveryGovernedSlotAndResponsiveConnectors()
    {
        var preset = ContainerPresetCatalog.ForFormation(ContainerPresetCatalog.ProcessPathKey);
        var formation = new PublicContainerBlockDto
        {
            Id = "formation-1",
            Type = "container",
            Visible = true,
            PresetKey = preset.Key,
            ContainerLayout = new()
            {
                SchemaVersion = 4,
                Mode = "formation",
                Purpose = "formation",
                MobileMode = "formation",
                SizeMode = "medium",
                ItemSize = "standard",
                FormationSpacing = "standard",
                ConnectorStyle = "solid"
            },
            Children = preset.Slots.Select((slot, index) => (PublicBlockDto)new PublicTextBlockDto
            {
                Id = $"slot-{index}",
                Type = "text",
                Visible = true,
                Order = index,
                ParentBlockId = "formation-1",
                PresetSlotName = slot.Key,
                Content = new() { ["en"] = $"Step {index + 1}" }
            }).ToList()
        };

        var cut = Render<ContainerBlock>(parameters => parameters
            .Add(component => component.Block, formation)
            .Add(component => component.Lang, "en")
            .Add(component => component.ShowInvisible, true));

        Assert.Equal(preset.MaximumChildren, cut.FindAll(".sc-section-blocks--formation > .sc-block-frame").Count);
        Assert.Single(cut.FindAll(".sc-formation-connectors--desktop"));
        Assert.Single(cut.FindAll(".sc-formation-connectors--mobile"));
        Assert.Contains("--sc-formation-mobile-aspect", cut.Find(".sc-section-blocks--formation").GetAttribute("style"));
        Assert.All(cut.FindAll(".sc-section-blocks--formation > .sc-block-frame"), frame =>
        {
            var style = frame.GetAttribute("style") ?? string.Empty;
            Assert.Contains("--sc-formation-x", style);
            Assert.Contains("--sc-formation-mobile-x", style);
        });
    }
}
