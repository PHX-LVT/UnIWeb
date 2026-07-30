using Contracts.Admin;
using FullProject.Services.BlockServices;

namespace Core.UnitTests;

public class BlockContractTests
{
    [Fact]
    public void DecorativeImage_CanSaveWithoutAltText()
    {
        var dto = new ImageBlockUpdateDto
        {
            Asset = new() { SchemaVersion = 1, Url = "/assets/background.webp" },
            Appearance = new() { Decorative = true },
            AltText = new()
        };

        Assert.DoesNotContain(BlockContractService.Validate(dto), error =>
            error.Contains("alt text", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InformativeImage_RequiresAltText()
    {
        var dto = new ImageBlockUpdateDto
        {
            Asset = new() { SchemaVersion = 1, Url = "/assets/team.webp" },
            Appearance = new() { Decorative = false },
            AltText = new()
        };

        Assert.Contains(BlockContractService.Validate(dto), error =>
            error.Contains("alt text", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MapPins_RequireUniqueIdentities()
    {
        var dto = new MapBlockUpdateDto
        {
            CenterLat = 10,
            CenterLng = 100,
            DefaultZoom = 12,
            Pins =
            [
                new() { Id = "same", Lat = 10, Lng = 100 },
                new() { Id = "same", Lat = 11, Lng = 101 }
            ]
        };

        Assert.Contains(BlockContractService.Validate(dto), error =>
            error.Contains("unique identity", StringComparison.Ordinal));
    }

    [Fact]
    public void ButtonIconPosition_IsRestricted()
    {
        var dto = new ButtonBlockUpdateDto { IconPosition = "above" };

        Assert.Contains(BlockContractService.Validate(dto), error =>
            error.Contains("icon position", StringComparison.Ordinal));
    }

    [Fact]
    public void FormationContract_AcceptsGovernedModesAndVisualControls()
    {
        var dto = new ContainerBlockUpdateDto
        {
            PresetKey = ContainerPresetCatalog.CircleSixKey,
            ContainerLayout = new()
            {
                SchemaVersion = 4,
                Purpose = "formation",
                Mode = "formation",
                MobileMode = "formation",
                SizeMode = "custom",
                CustomWidthPx = 720,
                ItemSize = "standard",
                FormationSpacing = "wide",
                ConnectorColorMode = "color",
                ConnectorColor = "#3156a3",
                ConnectorStyle = "dashed"
            }
        };

        Assert.Empty(BlockContractService.Validate(dto));
    }

    [Fact]
    public void FormationContract_RejectsUnsupportedVisualControls()
    {
        var dto = new ContainerBlockUpdateDto
        {
            ContainerLayout = new()
            {
                Purpose = "formation",
                Mode = "formation",
                MobileMode = "formation",
                SizeMode = "giant",
                ConnectorStyle = "animated"
            }
        };

        var errors = BlockContractService.Validate(dto);

        Assert.Contains(errors, error => error.Contains("size mode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("connector style", StringComparison.OrdinalIgnoreCase));
    }
}
