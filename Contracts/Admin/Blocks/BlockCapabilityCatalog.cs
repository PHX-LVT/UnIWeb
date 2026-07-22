namespace Contracts.Admin;

public sealed record BlockCapabilityDefinition(
    string Type,
    bool LocalizedContent,
    bool SupportsActions,
    bool SupportsIcon,
    bool SupportsAsset,
    bool RequiresAccessibleName,
    bool HasItems,
    bool CanBeContainerChild,
    bool SupportsDataBinding = false);

public static class BlockCapabilityCatalog
{
    public static IReadOnlyList<string> Types { get; } =
    [
        "text", "image", "video", "file", "map", "form", "card",
        "button", "metric", "bullet-list", "step", "icon", "container"
    ];

    private static readonly IReadOnlyDictionary<string, BlockCapabilityDefinition> Definitions =
        new Dictionary<string, BlockCapabilityDefinition>(StringComparer.Ordinal)
        {
            ["text"] = D("text", localized: true, actions: true),
            ["image"] = D("image", localized: true, actions: true, asset: true, accessible: true),
            ["video"] = D("video", localized: true, asset: true, accessible: true),
            ["file"] = D("file", localized: true, actions: true, asset: true, accessible: true),
            ["map"] = D("map", localized: true, items: true),
            ["form"] = D("form", accessible: true),
            ["card"] = D("card", localized: true, actions: true, icon: true, asset: true, accessible: true),
            ["button"] = D("button", localized: true, actions: true, icon: true, accessible: true),
            ["metric"] = D("metric", localized: true, icon: true, dataBinding: true),
            ["bullet-list"] = D("bullet-list", localized: true, icon: true, items: true),
            ["step"] = D("step", localized: true, icon: true),
            ["icon"] = D("icon", localized: true, actions: true, icon: true, accessible: true),
            ["container"] = D("container", localized: true, child: false)
        };

    public static IReadOnlyCollection<BlockCapabilityDefinition> All => Definitions.Values.ToArray();

    public static bool TryGet(string? type, out BlockCapabilityDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(type) && Definitions.TryGetValue(type, out var value))
        {
            definition = value;
            return true;
        }

        definition = Definitions["text"];
        return false;
    }

    public static BlockCapabilityDefinition For(string? type) =>
        TryGet(type, out var definition) ? definition : Definitions["text"];

    public static bool IsSupported(string? type) =>
        !string.IsNullOrWhiteSpace(type) && Definitions.ContainsKey(type);

    private static BlockCapabilityDefinition D(
        string type,
        bool localized = false,
        bool actions = false,
        bool icon = false,
        bool asset = false,
        bool accessible = false,
        bool items = false,
        bool child = true,
        bool dataBinding = false) =>
        new(type, localized, actions, icon, asset, accessible, items, child, dataBinding);
}
