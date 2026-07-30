using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services;
using FullProject.Services.AssetService;
using MongoDB.Driver;

namespace FullProject.Services.BlockServices;

public sealed class FormationAuthoringService
{
    private readonly MongoDbContext _context;
    private readonly BlockService _blocks;
    private readonly AssetCleanupService _assetCleanup;
    private static readonly Ganss.Xss.HtmlSanitizer Sanitizer = new();

    public FormationAuthoringService(
        MongoDbContext context,
        BlockService blocks,
        AssetCleanupService assetCleanup)
    {
        _context = context;
        _blocks = blocks;
        _assetCleanup = assetCleanup;
    }

    public async Task<(Block? Block, string? Error)> ReplaceSlotAsync(
        string pageId,
        string sectionId,
        string formationId,
        string slotName,
        BlockCreateDto request)
    {
        var formation = await _blocks.GetByIdAsync(pageId, sectionId, formationId) as ContainerBlock;
        if (formation is null || !ContainerPresetCatalog.TryGetFormation(formation.PresetKey, out var preset))
            return (null, "Formation not found.");

        var slot = preset.Slots.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, slotName, StringComparison.Ordinal));
        if (slot is null) return (null, "Choose a valid Formation slot.");
        if (!slot.AllowedBlockTypes.Contains(request.Type, StringComparer.Ordinal))
            return (null, $"The {slot.DisplayName} slot does not allow {request.Type} Blocks.");

        var existing = (await _blocks.GetBySectionAsync(pageId, sectionId)).FirstOrDefault(block =>
            block.ParentBlockId == formation.Id &&
            string.Equals(block.Authoring?.PresetSlotName, slot.Key, StringComparison.Ordinal));
        if (existing is null) return (null, "The governed Formation slot is missing.");
        if (existing.Authoring.FullLocked || existing.Authoring.ContentLocked)
            return (null, "Unlock this Formation slot before replacing it.");

        Block replacement;
        try
        {
            replacement = BuildReplacement(request, existing, formation, preset, slot);
        }
        catch (ArgumentException exception)
        {
            return (null, exception.Message);
        }

        var obsoleteAssets = _assetCleanup.BlockAssetUrls(existing).ToList();
        await _context.BlocksDraft.ReplaceOneAsync(block => block.Id == existing.Id, replacement);
        await _assetCleanup.DeleteUnusedAsync(obsoleteAssets);
        return (await _blocks.GetByIdAsync(pageId, sectionId, existing.Id), null);
    }

    public async Task<string?> SwapSlotsAsync(
        string pageId,
        string sectionId,
        string formationId,
        FormationSlotSwapRequestDto request)
    {
        var formation = await _blocks.GetByIdAsync(pageId, sectionId, formationId) as ContainerBlock;
        if (formation is null || !ContainerPresetCatalog.TryGetFormation(formation.PresetKey, out var preset))
            return "Formation not found.";
        if (string.Equals(request.FirstSlotName, request.SecondSlotName, StringComparison.Ordinal))
            return null;

        var firstIndex = preset.Slots.ToList().FindIndex(slot => slot.Key == request.FirstSlotName);
        var secondIndex = preset.Slots.ToList().FindIndex(slot => slot.Key == request.SecondSlotName);
        if (firstIndex < 0 || secondIndex < 0) return "Choose two valid Formation slots.";

        var children = (await _blocks.GetBySectionAsync(pageId, sectionId))
            .Where(block => block.ParentBlockId == formation.Id)
            .ToList();
        var first = children.FirstOrDefault(block => block.Authoring?.PresetSlotName == request.FirstSlotName);
        var second = children.FirstOrDefault(block => block.Authoring?.PresetSlotName == request.SecondSlotName);
        if (first is null || second is null) return "One or more governed Formation slots are missing.";
        if (first.Authoring.FullLocked || second.Authoring.FullLocked)
            return "Fully locked Formation slots cannot be swapped.";

        using var session = await _context.Client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            await UpdateSlotAsync(session, first, request.SecondSlotName, preset.Key, secondIndex);
            await UpdateSlotAsync(session, second, request.FirstSlotName, preset.Key, firstIndex);
            await session.CommitTransactionAsync();
            return null;
        }
        catch (Exception exception)
        {
            await session.AbortTransactionAsync();
            return $"Formation slot swap failed: {exception.Message}";
        }
    }

    public async Task<(ContainerBlock? Formation, string? Error)> ConvertAsync(
        string pageId,
        string sectionId,
        string formationId,
        FormationConvertRequestDto request)
    {
        var formation = await _blocks.GetByIdAsync(pageId, sectionId, formationId) as ContainerBlock;
        if (formation is null)
            return (null, "Formation not found.");
        if (!ContainerPresetCatalog.TryGetFormation(request.TargetPresetKey, out var target))
            return (null, "Choose a supported Formation preset.");
        var isCurrentFormation = ContainerPresetCatalog.IsFormation(formation.PresetKey);
        var legacyMigrationTarget = ContainerPresetCatalog.FormationMigrationKey(formation.PresetKey);
        if (!isCurrentFormation &&
            !string.Equals(legacyMigrationTarget, target.Key, StringComparison.Ordinal))
        {
            return (null, "This legacy Container requires its dedicated migration path.");
        }
        if (formation.Authoring.FullLocked || formation.Authoring.GeometryLocked)
            return (null, "Unlock the Formation geometry before changing its Formation.");
        if (isCurrentFormation && string.Equals(formation.PresetKey, target.Key, StringComparison.Ordinal))
            return (formation, null);

        var children = (await _blocks.GetBySectionAsync(pageId, sectionId))
            .Where(block => block.ParentBlockId == formation.Id)
            .OrderBy(block => block.Order)
            .ToList();
        if (children.Any(child => !target.AllowedBlockTypes.Contains(BlockType(child), StringComparer.Ordinal)))
            return (null, $"The {target.DisplayName} Formation does not support one or more current Block types.");

        var retained = ResolveRetainedChildren(children, target.MaximumChildren, request.RetainedBlockIds, out var retainError);
        if (retainError is not null) return (null, retainError);
        var removed = children.Where(child => retained.All(keep => keep.Id != child.Id)).ToList();
        var removedIds = removed.Select(item => item.Id).ToList();
        var obsoleteAssets = removed.SelectMany(_assetCleanup.BlockAssetUrls).ToList();
        var now = DateTime.UtcNow;
        var section = await _context.SectionsDraft
            .Find(candidate => candidate.Id == sectionId)
            .FirstOrDefaultAsync();

        using var session = await _context.Client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            for (var index = 0; index < retained.Count; index++)
                await UpdateSlotAsync(session, retained[index], target.Slots[index].Key, target.Key, index);

            if (removed.Count > 0)
                await _context.BlocksDraft.DeleteManyAsync(session, block => removedIds.Contains(block.Id));

            var placeholders = new List<Block>();
            for (var index = retained.Count; index < target.MaximumChildren; index++)
                placeholders.Add(BuildPlaceholder(formation, target, target.Slots[index], index, now));
            if (placeholders.Count > 0)
                await _context.BlocksDraft.InsertManyAsync(session, placeholders);

            formation.PresetKey = target.Key;
            formation.ContainerLayout.SchemaVersion = 4;
            formation.ContainerLayout.Purpose = "formation";
            formation.ContainerLayout.Mode = "formation";
            formation.ContainerLayout.Columns = 1;
            formation.ContainerLayout.MobileMode = "formation";
            formation.ContainerLayout.AllowedChildType = null;
            formation.ContainerLayout.Diagram = new ContainerDiagramSettings { SchemaVersion = 1, Enabled = false };
            formation.ContainerLayout.CustomWidthPx = formation.ContainerLayout.SizeMode == "custom"
                ? Math.Clamp(formation.ContainerLayout.CustomWidthPx ?? target.DefaultWidthPx, target.MinimumWidthPx, target.MaximumWidthPx)
                : null;
            formation.ContainerLayout.SizeMode = NormalizeChoice(formation.ContainerLayout.SizeMode, ["small", "medium", "large", "custom"], "medium");
            formation.ContainerLayout.ItemSize = NormalizeChoice(formation.ContainerLayout.ItemSize, ["compact", "standard", "large"], "standard");
            formation.ContainerLayout.FormationSpacing = NormalizeChoice(formation.ContainerLayout.FormationSpacing, ["compact", "standard", "wide"], "standard");
            formation.ContainerLayout.ConnectorColorMode = NormalizeChoice(formation.ContainerLayout.ConnectorColorMode, ["theme-primary", "theme-accent", "color"], "theme-accent");
            formation.ContainerLayout.ConnectorStyle = NormalizeChoice(formation.ContainerLayout.ConnectorStyle, ["solid", "dashed", "dotted", "none"], "solid");
            formation.Layout = ResizeFormationLayout(
                formation.Layout,
                target,
                formation.ContainerLayout,
                Contracts.Forms.FormBlockLayoutPolicy.AvailableContentWidthPx(section?.Style?.ContentWidth));
            formation.UpdatedAt = now;
            formation.Version++;
            await _context.BlocksDraft.ReplaceOneAsync(session, block => block.Id == formation.Id, formation);

            await session.CommitTransactionAsync();
        }
        catch (Exception exception)
        {
            await session.AbortTransactionAsync();
            return (null, $"Formation conversion failed: {exception.Message}");
        }

        await _assetCleanup.DeleteUnusedAsync(obsoleteAssets);
        return (await _blocks.GetByIdAsync(pageId, sectionId, formationId) as ContainerBlock, null);
    }

    public async Task<(string? ActiveBlockId, string? Error)> MigrateLegacyAsync(
        string pageId,
        string sectionId,
        string containerId)
    {
        var container = await _blocks.GetByIdAsync(pageId, sectionId, containerId) as ContainerBlock;
        if (container is null || !ContainerPresetCatalog.IsLegacy(container.PresetKey))
            return (null, "Legacy Container not found.");
        if (container.Authoring.FullLocked || container.Authoring.GeometryLocked)
            return (null, "Unlock the legacy Container geometry before migrating it.");

        var automaticTarget = ContainerPresetCatalog.FormationMigrationKey(container.PresetKey);
        if (automaticTarget is not null)
        {
            var (formation, error) = await ConvertAsync(pageId, sectionId, containerId, new FormationConvertRequestDto
            {
                TargetPresetKey = automaticTarget
            });
            return (formation?.Id, error);
        }

        if (container.PresetKey is not (ContainerPresetCatalog.StackKey or
            ContainerPresetCatalog.RowKey or
            ContainerPresetCatalog.GridKey))
        {
            return (null, "Split and advanced freeform Containers require manual reconstruction before the legacy Container can be removed.");
        }
        if (!string.Equals(container.PositionMode, "flow", StringComparison.Ordinal))
            return (null, "A positioned legacy Container must be reconstructed manually so its child coordinates are not lost.");

        var scope = await _blocks.GetBySectionAsync(pageId, sectionId);
        var children = scope
            .Where(block => string.Equals(block.ParentBlockId, container.Id, StringComparison.Ordinal))
            .OrderBy(block => block.Order)
            .ToList();
        if (children.Any(child => child.Authoring.FullLocked || child.Authoring.GeometryLocked))
            return (null, "Unlock all legacy Container children before flattening it.");

        var parentId = string.IsNullOrWhiteSpace(container.ParentBlockId) ? null : container.ParentBlockId;
        var siblingsAfter = scope
            .Where(block => block.Id != container.Id)
            .Where(block => string.Equals(Normalize(block.ParentBlockId), Normalize(parentId), StringComparison.Ordinal))
            .Where(block => string.Equals(Normalize(block.BlockZone), Normalize(container.BlockZone), StringComparison.Ordinal))
            .Where(block => string.Equals(Normalize(block.ColumnSlotId), Normalize(container.ColumnSlotId), StringComparison.Ordinal))
            .Where(block => block.Order > container.Order)
            .ToList();
        var orderDelta = Math.Max(0, children.Count - 1);
        var now = DateTime.UtcNow;

        using var session = await _context.Client.StartSessionAsync();
        session.StartTransaction();
        try
        {
            if (orderDelta > 0)
            {
                foreach (var sibling in siblingsAfter)
                {
                    await _context.BlocksDraft.UpdateOneAsync(
                        session,
                        block => block.Id == sibling.Id,
                        Builders<Block>.Update
                            .Set(block => block.Order, sibling.Order + orderDelta)
                            .Set(block => block.UpdatedAt, now)
                            .Inc(block => block.Version, 1));
                }
            }

            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                var authoring = child.Authoring ?? new BlockAuthoringPolicy();
                authoring.PresetSourceId = null;
                authoring.PresetSlotName = null;
                authoring.IsPlaceholder = false;
                await _context.BlocksDraft.UpdateOneAsync(
                    session,
                    block => block.Id == child.Id,
                    Builders<Block>.Update
                        .Set(block => block.ParentBlockId, parentId)
                        .Set(block => block.BlockZone, container.BlockZone)
                        .Set(block => block.ColumnSlotId, container.ColumnSlotId)
                        .Set(block => block.PositionMode, "flow")
                        .Set(block => block.Order, container.Order + index)
                        .Set(block => block.Authoring, authoring)
                        .Set(block => block.UpdatedAt, now)
                        .Inc(block => block.Version, 1));
            }

            await _context.BlocksDraft.DeleteOneAsync(session, block => block.Id == container.Id);
            await session.CommitTransactionAsync();
        }
        catch (Exception exception)
        {
            await session.AbortTransactionAsync();
            return (null, $"Legacy Container migration failed: {exception.Message}");
        }

        return (children.FirstOrDefault()?.Id, null);
    }

    private static List<Block> ResolveRetainedChildren(
        IReadOnlyList<Block> children,
        int targetCount,
        IReadOnlyCollection<string> retainedIds,
        out string? error)
    {
        error = null;
        if (children.Count <= targetCount)
            return children.ToList();

        var requested = (retainedIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requested.Count != targetCount)
        {
            error = $"Choose exactly {targetCount} Blocks to retain in the smaller Formation.";
            return new();
        }

        var retained = requested
            .Select(id => children.FirstOrDefault(child => child.Id == id))
            .ToList();
        if (retained.Any(child => child is null))
        {
            error = "One or more retained Blocks do not belong to this Formation.";
            return new();
        }

        return retained.Select(child => child!).ToList();
    }

    private async Task UpdateSlotAsync(
        IClientSessionHandle session,
        Block child,
        string slotName,
        string presetKey,
        int order)
    {
        var policy = child.Authoring ?? new BlockAuthoringPolicy();
        policy.SchemaVersion = Math.Max(policy.SchemaVersion, 2);
        policy.GeometryLocked = true;
        policy.PresetSlotName = slotName;
        policy.PresetSourceId = presetKey;
        await _context.BlocksDraft.UpdateOneAsync(
            session,
            block => block.Id == child.Id,
            Builders<Block>.Update
                .Set(block => block.Authoring, policy)
                .Set(block => block.PositionMode, "formation")
                .Set(block => block.Order, order)
                .Set(block => block.UpdatedAt, DateTime.UtcNow)
                .Inc(block => block.Version, 1));
    }

    private static Block BuildReplacement(
        BlockCreateDto request,
        Block existing,
        ContainerBlock formation,
        ContainerPresetDefinition preset,
        ContainerPresetSlotDefinition slot)
    {
        Block replacement = request switch
        {
            TextBlockCreateDto text => new TextBlock
            {
                Title = Clean(text.Title),
                Content = Clean(text.Content)
            },
            ImageBlockCreateDto image => new ImageBlock
            {
                Asset = BlockAssetMetadataService.ToModel(image.Asset),
                AltText = Clean(image.AltText),
                Caption = Clean(image.Caption),
                OpenInLightbox = image.OpenInLightbox,
                FocalPointX = Math.Clamp(image.FocalPointX, 0, 100),
                FocalPointY = Math.Clamp(image.FocalPointY, 0, 100)
            },
            CardBlockCreateDto card => new CardBlock
            {
                Icon = card.Icon,
                Title = Clean(card.Title),
                Description = Clean(card.Description),
                Asset = BlockAssetMetadataService.ToModel(card.Asset),
                ImageAltText = Clean(card.ImageAltText),
                ButtonLabel = Clean(card.ButtonLabel),
                Href = CleanUrl(card.Href),
                Action = NormalizeAction(card.Action),
                FormDefinitionId = NormalizeAction(card.Action) == "openForm" ? card.FormDefinitionId : null,
                ButtonStyle = NormalizeStyle(card.ButtonStyle)
            },
            ButtonBlockCreateDto button => new ButtonBlock
            {
                Icon = button.Icon,
                IconPosition = button.IconPosition == "right" ? "right" : "left",
                Label = Clean(button.Label),
                Href = CleanUrl(button.Href),
                Action = NormalizeAction(button.Action),
                FormDefinitionId = NormalizeAction(button.Action) == "openForm" ? button.FormDefinitionId : null,
                Style = NormalizeStyle(button.Style)
            },
            MetricBlockCreateDto metric => new MetricBlock
            {
                Icon = metric.Icon,
                Label = Clean(metric.Label),
                Value = metric.Value,
                Prefix = metric.Prefix,
                Suffix = metric.Suffix,
                Description = Clean(metric.Description)
            },
            StepBlockCreateDto step => new StepBlock
            {
                Icon = step.Icon,
                AutoNumber = step.AutoNumber,
                StepLabel = Clean(step.StepLabel),
                Title = Clean(step.Title),
                Description = Clean(step.Description)
            },
            IconBlockCreateDto icon => new IconBlock
            {
                Icon = icon.Icon,
                Label = Clean(icon.Label),
                Description = Clean(icon.Description),
                ActionEnabled = icon.ActionEnabled,
                Href = icon.ActionEnabled ? CleanUrl(icon.Href) : null,
                Action = NormalizeAction(icon.Action),
                FormDefinitionId = icon.ActionEnabled && NormalizeAction(icon.Action) == "openForm"
                    ? icon.FormDefinitionId
                    : null
            },
            _ => throw new ArgumentException("Choose a Block type supported by Formations.")
        };

        replacement.Id = existing.Id;
        replacement.StableId = string.IsNullOrWhiteSpace(existing.StableId)
            ? Guid.NewGuid().ToString()
            : existing.StableId;
        replacement.PageStableId = formation.PageStableId;
        replacement.SectionStableId = formation.SectionStableId;
        replacement.ParentBlockId = formation.Id;
        replacement.BlockZone = "default";
        replacement.PositionMode = "formation";
        replacement.Visible = true;
        replacement.Order = existing.Order;
        replacement.EditorLabel = request.EditorLabel.Count > 0 ? Clean(request.EditorLabel) : new()
        {
            ["en"] = slot.DisplayName,
            ["vi"] = slot.DisplayName,
            ["cn"] = slot.DisplayName
        };
        replacement.Layout = new BlockLayout { Width = "custom", ColumnSpan = 1, ZIndex = existing.Order + 1 };
        replacement.Appearance = BlockContractService.MergeAppearance(null, request.Appearance);
        replacement.Appearance.InheritFromContainer = request.Appearance?.InheritFromContainer ?? true;
        replacement.Responsive = new BlockResponsiveSettings { SchemaVersion = 1 };
        replacement.Animation = BlockContractService.MergeAnimation(null, request.Animation);
        replacement.Authoring = new BlockAuthoringPolicy
        {
            SchemaVersion = 2,
            GeometryLocked = true,
            IsPlaceholder = true,
            PresetSlotName = slot.Key,
            PresetSourceId = preset.Key
        };
        replacement.CreatedAt = existing.CreatedAt == default ? DateTime.UtcNow : existing.CreatedAt;
        replacement.UpdatedAt = DateTime.UtcNow;
        replacement.Version = Math.Max(1, existing.Version + 1);
        return replacement;
    }

    private static Block BuildPlaceholder(
        ContainerBlock formation,
        ContainerPresetDefinition preset,
        ContainerPresetSlotDefinition slot,
        int order,
        DateTime now) => new TextBlock
    {
        StableId = Guid.NewGuid().ToString(),
        PageStableId = formation.PageStableId,
        SectionStableId = formation.SectionStableId,
        ParentBlockId = formation.Id,
        BlockZone = "default",
        PositionMode = "formation",
        Visible = true,
        Order = order,
        EditorLabel = new() { ["en"] = slot.DisplayName, ["vi"] = slot.DisplayName, ["cn"] = slot.DisplayName },
        Layout = new BlockLayout { Width = "custom", ColumnSpan = 1, ZIndex = order + 1 },
        Appearance = new BlockAppearance { SchemaVersion = 2, BackgroundMode = "none", InheritFromContainer = true },
        Responsive = new BlockResponsiveSettings { SchemaVersion = 1 },
        Animation = new BlockAnimationSettings { SchemaVersion = 1 },
        Authoring = new BlockAuthoringPolicy
        {
            SchemaVersion = 2,
            GeometryLocked = true,
            IsPlaceholder = true,
            PresetSlotName = slot.Key,
            PresetSourceId = preset.Key
        },
        CreatedAt = now,
        UpdatedAt = now
    };

    private static Dictionary<string, string> Clean(Dictionary<string, string>? source) =>
        source?.ToDictionary(pair => pair.Key, pair => Sanitizer.Sanitize(pair.Value ?? string.Empty)) ?? new();

    private static string? CleanUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    private static string NormalizeAction(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "openform" or "openmodal" => "openForm",
        "externalurl" => "externalUrl",
        "downloadfile" => "downloadFile",
        _ => "linkToPage"
    };

    private static string NormalizeStyle(string? value) => value is "outline" or "ghost" ? value : "filled";
    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeChoice(string? value, IReadOnlyCollection<string> allowed, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && allowed.Contains(value, StringComparer.Ordinal) ? value : fallback;

    private static BlockLayout ResizeFormationLayout(
        BlockLayout? current,
        ContainerPresetDefinition preset,
        ContainerLayoutSettings settings,
        int availableWidthPx)
    {
        current ??= new BlockLayout();
        var geometry = FormationGeometryResolver.Resolve(
            preset.Key,
            settings.SizeMode,
            settings.CustomWidthPx,
            settings.ItemSize,
            settings.FormationSpacing);
        var availableWidth = Math.Max(320, availableWidthPx);
        var widthPercent = Math.Clamp(geometry.WidthPx / (double)availableWidth * 100d, 1d, 100d);
        var widthUnits = Math.Clamp((int)Math.Ceiling(widthPercent / 100d * 12d), 1, 12);
        var leftPercent = Math.Clamp(
            current.LeftPercent ?? Math.Clamp(current.X, 0, 11) / 12d * 100d,
            0d,
            Math.Max(0d, 100d - widthPercent));
        var topPx = Math.Clamp(
            current.TopPx ?? Math.Clamp(current.Y, 0, 60) * 48d,
            0d,
            Math.Max(0d, 10000d - geometry.HeightPx));

        return new BlockLayout
        {
            Width = "custom",
            ColumnSpan = widthUnits,
            Align = current.Align,
            Justify = current.Justify,
            Padding = current.Padding,
            Margin = current.Margin,
            BackgroundColor = current.BackgroundColor,
            BorderRadius = current.BorderRadius,
            ZIndex = current.ZIndex,
            X = Math.Clamp((int)Math.Round(leftPercent / 100d * 12d), 0, Math.Max(0, 12 - widthUnits)),
            Y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60),
            W = widthUnits,
            H = Math.Clamp((int)Math.Ceiling(geometry.HeightPx / 48d), 1, 40),
            LeftPercent = leftPercent,
            TopPx = topPx,
            WidthPercent = widthPercent,
            HeightPx = geometry.HeightPx
        };
    }

    private static string BlockType(Block block) => block switch
    {
        TextBlock => "text", ImageBlock => "image", CardBlock => "card", ButtonBlock => "button",
        MetricBlock => "metric", StepBlock => "step", IconBlock => "icon", _ => "unsupported"
    };
}
