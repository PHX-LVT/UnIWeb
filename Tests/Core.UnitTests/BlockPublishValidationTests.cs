using Contracts.Admin;
using FullProject.Models;
using FullProject.Services.BlockServices;

namespace Core.UnitTests;

public class BlockPublishValidationTests
{
    [Fact]
    public void SplitContainer_MustFillBothRequiredSlots()
    {
        var container = Container(ContainerPresetCatalog.SplitKey, "split", "composition");
        var child = new TextBlock
        {
            Id = "child-left",
            ParentBlockId = container.Id,
            Content = new() { ["en"] = "Left" },
            Authoring = new() { PresetSlotName = "left" }
        };

        var errors = BlockPublishValidationService.Validate([container, child]);

        Assert.Single(errors);
        Assert.Contains("Right", errors[0]);
    }

    [Fact]
    public void SplitContainer_PublishesWhenBothSlotsAreFilled()
    {
        var container = Container(ContainerPresetCatalog.SplitKey, "split", "composition");
        var left = Child(container.Id, "left", "Left");
        var right = Child(container.Id, "right", "Right");

        Assert.Empty(BlockPublishValidationService.Validate([container, left, right]));
    }

    [Fact]
    public void EmptyCollection_DoesNotFailPublishValidation()
    {
        var container = Container(ContainerPresetCatalog.StackKey, "stack", "collection");

        Assert.Empty(BlockPublishValidationService.Validate([container]));
    }

    [Fact]
    public void VisibleButtonWithLabel_RequiresDestination()
    {
        var button = new ButtonBlock
        {
            Id = "button",
            Label = new() { ["en"] = "Go" },
            Action = "linkToPage",
            Href = null
        };

        Assert.Contains(BlockPublishValidationService.Validate([button]), error =>
            error.Contains("requires a destination", StringComparison.Ordinal));
    }

    [Fact]
    public void DecorativeImage_DoesNotRequireAltText()
    {
        var image = new ImageBlock
        {
            Id = "image",
            Asset = new() { Url = "/assets/background.webp" },
            Appearance = new() { Decorative = true }
        };

        Assert.Empty(BlockPublishValidationService.Validate([image]));
    }

    [Fact]
    public void InformativeImage_RequiresAltText()
    {
        var image = new ImageBlock
        {
            Id = "image",
            Asset = new() { Url = "/assets/team.webp" },
            Appearance = new() { Decorative = false }
        };

        Assert.Contains(BlockPublishValidationService.Validate([image]), error =>
            error.Contains("requires alt text", StringComparison.Ordinal));
    }

    [Fact]
    public void InteractiveIcon_RequiresDestination()
    {
        var icon = new IconBlock
        {
            Id = "icon",
            Icon = "fas fa-arrow-right",
            ActionEnabled = true,
            Action = "linkToPage"
        };

        Assert.Contains(BlockPublishValidationService.Validate([icon]), error =>
            error.Contains("Icon action requires a destination", StringComparison.Ordinal));
    }

    private static ContainerBlock Container(string preset, string mode, string purpose) => new()
    {
        Id = $"container-{preset}",
        PresetKey = preset,
        ContainerLayout = new()
        {
            SchemaVersion = 3,
            Mode = mode,
            Purpose = purpose,
            Columns = mode == "split" ? 2 : 1
        }
    };

    private static TextBlock Child(string parentId, string slot, string content) => new()
    {
        Id = $"child-{slot}",
        ParentBlockId = parentId,
        Content = new() { ["en"] = content },
        Authoring = new() { PresetSlotName = slot }
    };
}
