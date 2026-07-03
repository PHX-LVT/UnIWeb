using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services.CloneServices;
using FullProject.Services.AssetService;
using MongoDB.Bson;
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

        if (clones.Count > 0)
            await _context.BlocksDraft.InsertManyAsync(clones);
        return (clones, null);
    }

    public async Task<(ContainerBlock? Container, string? Error)> GroupAsync(
        string pageId,
        string sectionId,
        BlockGroupRequestDto request)
    {
        var ids = NormalizeIds(request.BlockIds);
        if (ids.Count < 2) return (null, "Choose at least two Blocks to group.");
        var scope = await LoadScopeAsync(pageId, sectionId);
        if (scope is null) return (null, "Page or Section not found.");
        var selected = scope.Value.Blocks.Where(block => ids.Contains(block.Id)).ToList();
        if (selected.Count != ids.Count) return (null, "One or more selected Blocks are outside this Section.");
        if (selected.Any(IsGeometryLocked)) return (null, "Unlock Block geometry before grouping.");

        var parentIds = selected.Select(block => block.ParentBlockId ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        var zones = selected.Select(block => block.BlockZone).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (parentIds.Count != 1 || zones.Count != 1)
            return (null, "Grouped Blocks must share the same parent and zone.");
        if (!string.IsNullOrWhiteSpace(parentIds[0]))
        {
            var parentContainer = scope.Value.Blocks.OfType<ContainerBlock>()
                .FirstOrDefault(block => block.Id == parentIds[0]);
            if (parentContainer?.ContainerLayout.Purpose == "collection" &&
                parentContainer.ContainerLayout.AllowedChildType != "container")
                return (null, "Grouping would place a Composition inside a Collection locked to another Block type.");
        }

        foreach (var block in selected)
        {
            var diagramError = await _blocks.ValidateDiagramDeletionAsync(pageId, sectionId, block.Id);
            if (diagramError is not null)
                return (null, "Remove parent diagram connectors before grouping the referenced Blocks.");
        }

        var left = selected.Min(block => Left(block.Layout));
        var top = selected.Min(block => Top(block.Layout));
        var right = selected.Max(block => Left(block.Layout) + Width(block.Layout));
        var bottom = selected.Max(block => Top(block.Layout) + Height(block.Layout));
        var groupWidth = Math.Clamp(right - left, 1, 100);
        var groupHeight = Math.Clamp(bottom - top, 24, 10000);
        var now = DateTime.UtcNow;
        var container = new ContainerBlock
        {
            Id = ObjectId.GenerateNewId().ToString(),
            StableId = Guid.NewGuid().ToString(),
            PageStableId = scope.Value.Page.StableId,
            SectionStableId = scope.Value.Section.StableId,
            ParentBlockId = string.IsNullOrWhiteSpace(parentIds[0]) ? null : parentIds[0],
            BlockZone = zones[0],
            PositionMode = "freeform",
            Title = request.Title,
            Order = selected.Min(block => block.Order),
            Layout = new BlockLayout
            {
                LeftPercent = left,
                TopPx = top,
                WidthPercent = groupWidth,
                HeightPx = groupHeight,
                X = Math.Clamp((int)Math.Round(left / 100d * 12), 0, 11),
                Y = Math.Clamp((int)Math.Round(top / 48d), 0, 60),
                W = Math.Clamp((int)Math.Round(groupWidth / 100d * 12), 1, 12),
                H = Math.Clamp((int)Math.Round(groupHeight / 48d), 1, 40)
            },
            ContainerLayout = new ContainerLayoutSettings
            {
                SchemaVersion = 2,
                Purpose = "composition",
                AllowedChildType = null,
                Mode = "freeform"
            },
            CreatedAt = now,
            UpdatedAt = now
        };

        await _context.BlocksDraft.InsertOneAsync(container);
        try
        {
            var writes = selected.Select(block =>
            {
                var childLeft = Math.Clamp((Left(block.Layout) - left) / groupWidth * 100, 0, 100);
                var childWidth = Math.Clamp(Width(block.Layout) / groupWidth * 100, 1, 100);
                var childTop = Math.Clamp(Top(block.Layout) - top, 0, groupHeight);
                var childLayout = CopyLayout(block.Layout);
                childLayout.LeftPercent = childLeft;
                childLayout.TopPx = childTop;
                childLayout.WidthPercent = childWidth;
                return new UpdateOneModel<Block>(
                    Builders<Block>.Filter.Eq(item => item.Id, block.Id),
                    Builders<Block>.Update
                        .Set(item => item.ParentBlockId, container.Id)
                        .Set(item => item.BlockZone, "default")
                        .Set(item => item.Layout, childLayout)
                        .Set(item => item.UpdatedAt, now)
                        .Inc(item => item.Version, 1));
            }).Cast<WriteModel<Block>>().ToList();
            await _context.BlocksDraft.BulkWriteAsync(writes);
        }
        catch
        {
            await _context.BlocksDraft.DeleteOneAsync(block => block.Id == container.Id);
            throw;
        }

        return (container, null);
    }

    public async Task<(List<string>? BlockIds, string? Error)> UngroupAsync(
        string pageId,
        string sectionId,
        string containerId)
    {
        var scope = await LoadScopeAsync(pageId, sectionId);
        if (scope is null) return (null, "Page or Section not found.");
        var container = scope.Value.Blocks.OfType<ContainerBlock>().FirstOrDefault(block => block.Id == containerId);
        if (container is null) return (null, "Container not found.");
        if (IsGeometryLocked(container)) return (null, "Unlock the Container before ungrouping.");
        var children = scope.Value.Blocks.Where(block => block.ParentBlockId == containerId).ToList();
        if (children.Any(IsGeometryLocked))
            return (null, "Unlock child Block geometry before ungrouping this Container.");
        if (!string.IsNullOrWhiteSpace(container.ParentBlockId))
        {
            var parentContainer = scope.Value.Blocks.OfType<ContainerBlock>()
                .FirstOrDefault(block => block.Id == container.ParentBlockId);
            if (parentContainer?.ContainerLayout.Purpose == "collection")
            {
                var childTypes = children.Select(BlockType).Distinct(StringComparer.Ordinal).ToList();
                if (childTypes.Count != 1 || childTypes[0] != parentContainer.ContainerLayout.AllowedChildType)
                    return (null, "Ungrouping would violate the parent Collection's locked child type.");
            }
        }
        var diagramError = await _blocks.ValidateDiagramDeletionAsync(pageId, sectionId, container.Id);
        if (diagramError is not null) return (null, diagramError);
        var now = DateTime.UtcNow;
        var writes = children.Select(child =>
        {
            var layout = CopyLayout(child.Layout);
            layout.LeftPercent = Math.Clamp(Left(container.Layout) + Left(child.Layout) / 100d * Width(container.Layout), 0, 100);
            layout.TopPx = Math.Clamp(Top(container.Layout) + Top(child.Layout), 0, 10000);
            layout.WidthPercent = Math.Clamp(Width(child.Layout) / 100d * Width(container.Layout), 1, 100);
            return new UpdateOneModel<Block>(
                Builders<Block>.Filter.Eq(block => block.Id, child.Id),
                Builders<Block>.Update
                    .Set(block => block.ParentBlockId, container.ParentBlockId)
                    .Set(block => block.BlockZone, container.BlockZone)
                    .Set(block => block.Layout, layout)
                    .Set(block => block.UpdatedAt, now)
                    .Inc(block => block.Version, 1));
        }).Cast<WriteModel<Block>>().ToList();
        if (writes.Count > 0) await _context.BlocksDraft.BulkWriteAsync(writes);
        await _context.BlocksDraft.DeleteOneAsync(block => block.Id == container.Id);
        return (children.Select(child => child.Id).ToList(), null);
    }

    private static string BlockType(Block block) => block switch
    {
        TextBlock => "text", ImageBlock => "image", VideoBlock => "video", FileBlock => "file",
        MapBlock => "map", FormBlock => "form", CardBlock => "card", ButtonBlock => "button",
        MetricBlock => "metric", BulletListBlock => "bullet-list", StepBlock => "step", IconBlock => "icon",
        ContainerBlock => "container", _ => "text"
    };

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
        foreach (var block in selected)
        {
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId) && graphIds.Contains(block.ParentBlockId))
                continue;
            var diagramError = await _blocks.ValidateDiagramDeletionAsync(pageId, sectionId, block.Id);
            if (diagramError is not null) return diagramError;
        }
        var assetUrls = selected.SelectMany(_assetCleanup.BlockAssetUrls).ToList();
        await _context.BlocksDraft.DeleteManyAsync(block => graphIds.Contains(block.Id));
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

    private static double Left(BlockLayout layout) =>
        Math.Clamp(layout.LeftPercent ?? layout.X / 12d * 100, 0, 100);
    private static double Top(BlockLayout layout) =>
        Math.Clamp(layout.TopPx ?? layout.Y * 48d, 0, 10000);
    private static double Width(BlockLayout layout) =>
        Math.Clamp(layout.WidthPercent ?? layout.W / 12d * 100, 1, 100);
    private static double Height(BlockLayout layout) =>
        Math.Clamp(layout.HeightPx ?? layout.H * 48d, 24, 10000);

    private static BlockLayout CopyLayout(BlockLayout value) => new()
    {
        Width = value.Width,
        ColumnSpan = value.ColumnSpan,
        Align = value.Align,
        Justify = value.Justify,
        Padding = value.Padding,
        Margin = value.Margin,
        BackgroundColor = value.BackgroundColor,
        BorderRadius = value.BorderRadius,
        ZIndex = value.ZIndex,
        X = value.X,
        Y = value.Y,
        W = value.W,
        H = value.H,
        LeftPercent = value.LeftPercent,
        TopPx = value.TopPx,
        WidthPercent = value.WidthPercent,
        HeightPx = value.HeightPx
    };
}
