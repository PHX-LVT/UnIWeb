using System.Text.Json;
using Contracts.Admin;

namespace Core.UnitTests;

public class BlockCompatibilityTests
{
    [Fact]
    public void LegacyContainerWithoutPreset_RemainsLegacyFreeform()
    {
        Assert.Equal(
            ContainerPresetCatalog.LegacyFreeformKey,
            ContainerPresetCatalog.EffectiveKey(null));
    }

    [Fact]
    public void LegacyMapPinWithoutVisibility_RemainsVisible()
    {
        var pin = JsonSerializer.Deserialize<MapPinDto>("""{"id":"pin-1","label":"Office","lat":10,"lng":100}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(pin);
        Assert.True(pin.Visible);
        Assert.Equal(0, pin.Order);
    }

    [Fact]
    public void LegacyIconWithoutActionFlag_RemainsNonInteractive()
    {
        var icon = JsonSerializer.Deserialize<IconBlockUpdateDto>(
            """{"type":"icon","icon":"fas fa-star","label":{"en":"Quality"}}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(icon);
        Assert.False(icon.ActionEnabled);
    }
}
