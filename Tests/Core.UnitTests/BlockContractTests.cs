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
}
