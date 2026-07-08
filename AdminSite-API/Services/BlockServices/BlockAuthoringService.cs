using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services.CloneServices;
using FullProject.Services.AssetService;
using MongoDB.Driver;

namespace FullProject.Services.BlockServices;

public sealed class BlockAuthoringService
{
    private readonly MongoDbContext _context;
    private readonly BlockService _blocks;
    private readonly PageGraphCloneService _clones;
    private readonly AssetCleanupService _assetCleanup;

    public BlockAuthoringService(
        MongoDbContext context,
        BlockService blocks,
        PageGraphCloneService clones,
        AssetCleanupService assetCleanup)
    {
        _context = context;
        _blocks = blocks;
        _clones = clones;
        _assetCleanup = assetCleanup;
    }

    public async Task<string?> ValidateContentMutationAsync(
        string pageId,
        string sectionId,
        string blockId)
    {
        var block = await _blocks.GetByIdAsync(pageId, sectionId, blockId);
        if (block is null) return "Block not found.";
        return block.Authoring.FullLocked
            ? "This Block is fully locked."
            : block.Authoring.ContentLocked
                ? "This Block's content is locked by its preset policy."
                : null;
    }

    public async Task<string?> PrepareContentUpdateAsync(
        string pageId,
        string sectionId,
        string blockId,
        BlockUpdateDto dto)
    {
        var block = await _blocks.GetByIdAsync(pageId, sectionId, blockId);
        if (block is null) return "Block not found.";
        if (block.Authoring.FullLocked) return "This Block is fully locked.";
        if (block.Authoring.ContentLocked) return "This Block's content is locked by its preset policy.";
        if (!block.Authoring.GeometryLocked) return null;

        dto.Layout = null;
        dto.Responsive = null;
        dto.BlockZone = block.BlockZone;
        dto.ZoneId = block.BlockZone;
        dto.PositionMode = block.PositionMode;
        dto.ParentBlockId = block.ParentBlockId;
        if (dto is ContainerBlockUpdateDto container)
        {
            container.ContainerLayout = null;
            container.LayoutMode = null;
            container.Columns = null;
            container.Gap = null;
            container.OrbitRadius = null;
            container.OrbitStartAngle = null;
            container.SemicircleRadius = null;
            container.SemicircleStartAngle = null;
            container.SemicircleEndAngle = null;
        }
        return null;
    }

    public async Task<string?> ValidateGeometryMutationAsync(
        string pageId,
        string sectionId,
        IEnumerable<string> blockIds)
    {
        var ids = NormalizeIds(blockIds);
        if (ids.Count == 0) return "Choose at least one Block.";
        var scope = await LoadScopeAsync(pageId, sectionId);
        if (scope is null) return "Page or Section not found.";
        var selected = scope.Value.Blocks.Where(block => ids.Contains(block.Id)).ToList();
        if (selected.Count != ids.Count) return "One or more selected Blocks are outside this Section.";
        return selected.Any(IsGeometryLocked)
            ? "Unlock the selected Block geometry before arranging it."
            : null;
    }

    public async Task<(List<Block>? Blocks, string? Error)> UpdateLayoutsAsync(
        string pageId,
        string sectionId,
        BlockBulkLayoutUpdateDto request)
    {
        var items = request.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.BlockId))
            .GroupBy(item => item.BlockId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Take(100)
            .ToList();
        var error = await ValidateGeometryMutationAsync(pageId, sectionId, items.Select(item => item.BlockId));
        if (error is not null) return (null, error);

        var updated = new List<Block>();
        foreach (var item in items)
        {
            var block = await _blocks.UpdateLayoutAsync(pageId, sectionId, item.BlockId, item.Layout);
            if (block is null) return (null, "A selected Block could not be updated.");
            updated.Add(block);
        }
        return (updated, null);
    }

    public async Task<(List<Block>? Blocks, string? Error)> DuplicateAsync(
        string pageId,
        string sectionId,
        IEnumerable<string> blockIds)
    {
        var ids = NormalizeIds(blockIds);
        var scope = await LoadScopeAsync(pageId, sectionId);
        if (scope is null) return (null, "Page or Section not found.");
        if (ids.Count == 0) return (null, "Choose at least one Block.");

        var roots = scope.Value.Blocks.Where(block => ids.Contains(block.Id)).ToList();
        if (roots.Count != ids.Count) return (null, "One or more selected Blocks are outside this Section.");
        if (roots.Any(block => block.Authoring.FullLocked))
            return (null, "Fully locked Blocks cannot be duplicated.");

        var effectiveRoots = roots.Where(root => !HasSelectedAncestor(root, ids, scope.Value.Blocks)).ToList();
        foreach (var parentGroup in effectiveRoots
                     .Where(root => !string.IsNullOrWhiteSpace(root.ParentBlockId))
                     .GroupBy(root => root.ParentBlockId!, StringComparer.Ordinal))
        {
            var parent = scope.Value.Blocks.OfType<ContainerBlock>()
                .FirstOrDefault(container => container.Id == parentGroup.Key);
            if (parent is null) continue;
            if (parentGroup.Any(root => root is ContainerBlock))
                return (null, "Containers cannot own another Container. Duplicate the Container at the Section level instead.");
            var currentCount = scope.Value.Blocks.Count(block => block.ParentBlockId == parent.Id);
            var capacity = ContainerCapacityPolicy.MaxChildren(
                parent.PresetKey,
                parent.ContainerLayout.Mode,
                parent.ContainerLayout.Columns);
            if (ContainerPresetCatalog.TryGetGoverned(parent.PresetKey, out var preset))
            {
                var disallowed = parentGroup
                    .Select(BlockType)
                    .FirstOrDefault(type => !preset.AllowedBlockTypes.Contains(type, StringComparer.Ordinal));
                if (disallowed is not null)
                    return (null, $"The {preset.DisplayName} Container preset does not allow {disallowed} Blocks.");
            }
            if (currentCount + parentGroup.Count() > capacity)
                return (null,
                    $"This {ContainerCapacityPolicy.NormalizeMode(parent.ContainerLayout.Mode)} Container is full ({capacity} Blocks maximum).");
        }

        var graphIds = new HashSet<string>(ids, StringComparer.Ordinal);
        var added = true;
        while (added)
        {
            added = false;
            foreach (var block in scope.Value.Blocks)
            {
                if (!string.IsNullOrWhiteSpace(block.ParentBlockId) &&
                    graphIds.Contains(block.ParentBlockId) &&
                    graphIds.Add(block.Id))
                    added = true;
            }
        }

        var sourceGraph = scope.Value.Blocks.Where(block => graphIds.Contains(block.Id)).ToList();
        if (sourceGraph.Any(block => block.Authoring.FullLocked))
            return (null, "Fully locked Blocks cannot be duplicated.");
        var clones = _clones.CloneBlocksAsNewContent(
            sourceGraph,
            scope.Value.Page.StableId,
            scope.Value.Section.StableId);

        foreach (var clone in clones.Where(clone =>
                     string.IsNullOrWhiteSpace(clone.ParentBlockId) ||
                     !clones.Any(candidate => candidate.Id == clone.ParentBlockId)))
        {
            clone.Layout.LeftPercent = Math.Clamp((clone.Layout.LeftPercent ?? clone.Layout.X / 12d * 100) + 2, 0, 100);
            clone.Layout.TopPx = Math.Clamp((clone.Layout.TopPx ?? clone.Layout.Y * 48d) + 24, 0, 10000);
            clone.Order += sourceGraph.Count;
        }

        var clonedIds = clones.Select(clone => clone.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var parentGroup in clones
                     .Where(clone => !string.IsNullOrWhiteSpace(clone.ParentBlockId) &&
                                     !clonedIds.Contains(clone.ParentBlockId))
                     .GroupBy(clone => clone.ParentBlockId!, StringComparer.Ordinal))
        {
            var parent = scope.Value.Blocks.OfType<ContainerBlock>()
                .FirstOrDefault(container => container.Id == parentGroup.Key);
            if (parent is null) continue;

            var reserved = scope.Value.Blocks
                .Where(block => block.ParentBlockId == parent.Id)
                .ToList();
            foreach (var clone in parentGroup.OrderBy(block => block.Order))
            {
                AssignNextPresetSlot(parent, clone, reserved);
                reserved.Add(clone);
            }
        }

        if (clones.Count > 0)
            await _context.BlocksDraft.InsertManyAsync(clones);
        return (clones, null);
    }

    private static bool HasSelectedAncestor(Block block, HashSet<string> selectedIds, IReadOnlyCollection<Block> scope)
    {
        var parentId = block.ParentBlockId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(parentId) && visited.Add(parentId))
        {
            if (selectedIds.Contains(parentId)) return true;
            parentId = scope.FirstOrDefault(candidate => candidate.Id == parentId)?.ParentBlockId;
        }
        return false;
    }

    private static string BlockType(Block block) => block switch
    {
        TextBlock => "text", ImageBlock => "image", VideoBlock => "video", FileBlock => "file",
        MapBlock => "map", FormBlock => "form", CardBlock => "card", ButtonBlock => "button",
        MetricBlock => "metric", BulletListBlock => "bullet-list", StepBlock => "step", IconBlock => "icon",
        ContainerBlock => "container", _ => "text"
    };

    private static void AssignNextPresetSlot(
        ContainerBlock parent,
        Block child,
        IReadOnlyCollection<Block> reservedSiblings)
    {
        if (!ContainerPresetCatalog.TryGetGoverned(parent.PresetKey, out var preset) ||
            preset.Slots.Count == 0)
        {
            child.Authoring ??= new BlockAuthoringPolicy();
            child.Authoring.PresetSlotName = null;
            child.Authoring.PresetSourceId = null;
            return;
        }

        var childType = BlockType(child);
        var slot = ContainerCapacityPolicy.NextAvailableSlot(
            parent.PresetKey,
            reservedSiblings.Select(block => block.Authoring?.PresetSlotName),
            childType);
        if (slot is null)
            throw new InvalidOperationException(
                $"The {preset.DisplayName} Container has no available slot for {childType} Blocks.");

        child.Authoring ??= new BlockAuthoringPolicy();
        child.Authoring.SchemaVersion = Math.Max(child.Authoring.SchemaVersion, 1);
        child.Authoring.PresetSlotName = slot.Key;
        child.Authoring.PresetSourceId = parent.PresetKey;
    }

    public async Task<string?> DeleteGraphsAsync(
        string pageId,
        string sectionId,
        IEnumerable<string> blockIds)
    {
        var ids = NormalizeIds(blockIds);
        var scope = await LoadScopeAsync(pageId, sectionId);
        if (scope is null) return "Page or Section not found.";
        if (ids.Count == 0) return "Choose at least one Block.";
        var roots = scope.Value.Blocks.Where(block => ids.Contains(block.Id)).ToList();
        if (roots.Count != ids.Count) return "One or more selected Blocks are outside this Section.";
        var graphIds = new HashSet<string>(ids, StringComparer.Ordinal);
        var added = true;
        while (added)
        {
            added = false;
            foreach (var block in scope.Value.Blocks)
                if (!string.IsNullOrWhiteSpace(block.ParentBlockId) && graphIds.Contains(block.ParentBlockId) && graphIds.Add(block.Id))
                    added = true;
        }
        var selected = scope.Value.Blocks.Where(block => graphIds.Contains(block.Id)).ToList();
        if (selected.Any(block => block.Authoring.FullLocked || block.Authoring.ContentLocked)) return "Content-locked Blocks cannot be deleted.";
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root.ParentBlockId) || graphIds.Contains(root.ParentBlockId))
                continue;
            var parent = scope.Value.Blocks.OfType<ContainerBlock>()
                .FirstOrDefault(container => container.Id == root.ParentBlockId);
            if (parent is not null &&
                ContainerCapacityPolicy.IsRequiredSlot(parent.PresetKey, root.Authoring.PresetSlotName))
            {
                var slotName = ContainerCapacityPolicy.SlotDisplayName(parent.PresetKey, root.Authoring.PresetSlotName);
                return $"This Block fills the required Container slot '{slotName}' and cannot be deleted directly. Delete the Container instead.";
            }
        }
        foreach (var block in selected)
        {
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId) && graphIds.Contains(block.ParentBlockId))
                continue;
            var diagramError = await _blocks.ValidateDiagramDeletionAsync(pageId, sectionId, block.Id);
            if (diagramError is not null) return diagramError;
        }
        var assetUrls = selected.SelectMany(_assetCleanup.BlockAssetUrls).ToList();
        var deleteResult = await _context.BlocksDraft.DeleteManyAsync(block => graphIds.Contains(block.Id));
        if (deleteResult.DeletedCount != selected.Count)
            return "The complete Block graph could not be deleted. No asset cleanup was performed.";
        await _assetCleanup.DeleteUnusedAsync(assetUrls);
        return null;
    }

    public async Task<(Block? Block, string? Error)> UpdateLockAsync(
        string pageId,
        string sectionId,
        string blockId,
        BlockAuthoringLockUpdateDto request,
        bool canUnlock)
    {
        var block = await _blocks.GetByIdAsync(pageId, sectionId, blockId);
        if (block is null) return (null, "Block not found.");
        var current = block.Authoring ?? new BlockAuthoringPolicy();
        var reducesLock =
            current.ContentLocked && !request.ContentLocked ||
            current.GeometryLocked && !request.GeometryLocked ||
            current.FullLocked && !request.FullLocked;
        if (reducesLock && !canUnlock)
            return (null, "Only AdminAdmin can unlock preset-protected Blocks.");

        var policy = new BlockAuthoringPolicy
        {
            SchemaVersion = 1,
            ContentLocked = request.ContentLocked,
            GeometryLocked = request.GeometryLocked || request.FullLocked,
            FullLocked = request.FullLocked,
            PresetSlotName = current.PresetSlotName,
            PresetSourceId = current.PresetSourceId
        };
        await _context.BlocksDraft.UpdateOneAsync(
            item => item.Id == blockId,
            Builders<Block>.Update
                .Set(item => item.Authoring, policy)
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Inc(item => item.Version, 1));
        return (await _blocks.GetByIdAsync(pageId, sectionId, blockId), null);
    }

    private async Task<(Page Page, Section Section, List<Block> Blocks)?> LoadScopeAsync(
        string pageId,
        string sectionId)
    {
        var page = await _context.PagesDraft.Find(item => item.Id == pageId).FirstOrDefaultAsync();
        if (page is null) return null;
        var section = await _context.SectionsDraft
            .Find(item => item.Id == sectionId && item.PageStableId == page.StableId)
            .FirstOrDefaultAsync();
        if (section is null) return null;
        var blocks = await _context.BlocksDraft
            .Find(item => item.PageStableId == page.StableId && item.SectionStableId == section.StableId)
            .SortBy(item => item.Order)
            .ToListAsync();
        return (page, section, blocks);
    }

    private static HashSet<string> NormalizeIds(IEnumerable<string>? ids) =>
        (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsGeometryLocked(Block block) =>
        block.Authoring.FullLocked || block.Authoring.GeometryLocked;

}
