using Contracts.Admin;

namespace Core.UnitTests;

public class BlockPolicyTests
{
    [Fact]
    public void CapabilityCatalog_CoversAllPersistedBlockTypes()
    {
        Assert.Equal(13, BlockCapabilityCatalog.Types.Count);
        Assert.All(BlockCapabilityCatalog.Types, type => Assert.True(BlockCapabilityCatalog.IsSupported(type)));
        Assert.DoesNotContain("container", ContainerCapacityPolicy.ChildBlockTypes);
    }

    [Fact]
    public void SplitPreset_ReportsBothMissingRequiredSlots()
    {
        var missing = ContainerCapacityPolicy.MissingRequiredSlots(
            ContainerPresetCatalog.SplitKey,
            Array.Empty<string?>());

        Assert.Equal(["left", "right"], missing.Select(slot => slot.Key));
    }

    [Fact]
    public void SplitPreset_ReportsOnlyUnoccupiedRequiredSlot()
    {
        var missing = ContainerCapacityPolicy.MissingRequiredSlots(
            ContainerPresetCatalog.SplitKey,
            ["left"]);

        Assert.Single(missing);
        Assert.Equal("right", missing[0].Key);
    }

    [Fact]
    public void CollectionConversion_RejectsMixedChildTypes()
    {
        var allowed = ContainerCapacityPolicy.CanConvert(
            ContainerPresetCatalog.GridKey,
            ["card", "metric"],
            out var error);

        Assert.False(allowed);
        Assert.Contains("only one Block type", error);
    }

    [Fact]
    public void CompositionConversion_AcceptsMixedChildrenWithinCapacity()
    {
        var allowed = ContainerCapacityPolicy.CanConvert(
            ContainerPresetCatalog.OrbitKey,
            ["card", "metric", "icon"],
            out var error);

        Assert.True(allowed);
        Assert.Null(error);
    }
}
