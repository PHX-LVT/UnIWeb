using Contracts.Admin;

namespace Core.UnitTests;

public class FormationGeometryTests
{
    [Theory]
    [InlineData(ContainerPresetCatalog.CircleFourKey, 4)]
    [InlineData(ContainerPresetCatalog.CircleSixKey, 6)]
    [InlineData(ContainerPresetCatalog.CircleEightKey, 8)]
    [InlineData(ContainerPresetCatalog.SemicircleSixKey, 6)]
    [InlineData(ContainerPresetCatalog.PyramidKey, 6)]
    [InlineData(ContainerPresetCatalog.ProcessPathKey, 6)]
    public void FormationCatalog_ProvidesEveryGovernedSlot(string key, int count)
    {
        var preset = ContainerPresetCatalog.ForFormation(key);

        Assert.True(preset.IsFormation);
        Assert.Equal(count, preset.MaximumChildren);
        Assert.Equal(count, preset.Slots.Count);
        Assert.All(preset.Slots, slot => Assert.True(slot.Required));
    }

    [Fact]
    public void Circle_PreservesSquareFrame()
    {
        var geometry = FormationGeometryResolver.Resolve(
            ContainerPresetCatalog.CircleSixKey,
            "custom",
            740,
            "standard",
            "standard");

        Assert.Equal(740, geometry.WidthPx);
        Assert.Equal(740, geometry.HeightPx);
        Assert.Equal(6, geometry.Slots.Count);
    }

    [Fact]
    public void CustomSize_IsClampedByPreset()
    {
        var preset = ContainerPresetCatalog.ForFormation(ContainerPresetCatalog.TriangleKey);

        var tooSmall = FormationGeometryResolver.Resolve(preset.Key, "custom", 1, "standard", "standard");
        var tooLarge = FormationGeometryResolver.Resolve(preset.Key, "custom", 9999, "standard", "standard");

        Assert.Equal(preset.MinimumWidthPx, tooSmall.WidthPx);
        Assert.Equal(preset.MaximumWidthPx, tooLarge.WidthPx);
    }

    [Fact]
    public void WideSpacing_MovesCircleSlotsAwayFromCentre()
    {
        var compact = FormationGeometryResolver.Resolve(ContainerPresetCatalog.CircleFourKey, "medium", null, "standard", "compact");
        var wide = FormationGeometryResolver.Resolve(ContainerPresetCatalog.CircleFourKey, "medium", null, "standard", "wide");

        Assert.True(Math.Abs(wide.Slots[0].YPercent - 50) > Math.Abs(compact.Slots[0].YPercent - 50));
    }

    [Fact]
    public void ProcessPath_UsesVerticalMobileGeometry()
    {
        var desktop = FormationGeometryResolver.Resolve(
            ContainerPresetCatalog.ProcessPathKey, "medium", null, "standard", "standard");
        var mobile = FormationGeometryResolver.Resolve(
            ContainerPresetCatalog.ProcessPathKey, "medium", null, "standard", "standard", mobile: true);

        Assert.True(desktop.WidthPx > desktop.HeightPx);
        Assert.True(mobile.HeightPx > mobile.WidthPx);
        Assert.Single(mobile.Slots.Select(slot => Math.Round(slot.XPercent)).Distinct());
        Assert.True(mobile.Slots.Last().YPercent > mobile.Slots.First().YPercent);
    }
}
