using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Contracts.Admin;
using Contracts.Forms;
using FullProject.Services.AssetService;
using FullProject.Services.BlockServices;
using FullProject.Services.FormServices;
using SharedComponents.Helpers;

namespace FullProject.Services
{
    public class BlockService
    {
        private readonly MongoDbContext _context;
        private readonly AssetCleanupService _assetCleanup;
        private static readonly Ganss.Xss.HtmlSanitizer _sanitizer = new();

        public BlockService(MongoDbContext context, AssetCleanupService assetCleanup)
        {
            _context = context;
            _assetCleanup = assetCleanup;
        }

        private static Dictionary<string, string> SanitizeDictionary(Dictionary<string, string>? input)
        {
            if (input == null) return new();
            return input.ToDictionary(kv => kv.Key,
                kv => _sanitizer.Sanitize(kv.Value ?? string.Empty));
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ADMIN WORKSPACE BACKEND METHODS (DRAFT EXCLUSIVE)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public async Task<List<Block>> GetBySectionAsync(string pageId, string sectionId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return new();
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return new();
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId && b.SectionStableId == section.StableId)
                .SortBy(b => b.Order)
                .ToListAsync();
        }

        public async Task<List<Block>> GetByPageAsync(string pageId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return new();
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId)
                .SortBy(b => b.Order)
                .ToListAsync();
        }

        public async Task<Block?> GetByIdAsync(string pageId, string sectionId, string blockId)
        {
            if (!ObjectId.TryParse(pageId, out _) ||
                !ObjectId.TryParse(sectionId, out _) ||
                !ObjectId.TryParse(blockId, out _))
            {
                return null;
            }

            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return null;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return null;
            return await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId &&
                           b.SectionStableId == section.StableId &&
                           b.Id == blockId)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> ValidateDiagramAnchorsAsync(
            string pageId,
            string sectionId,
            string containerId,
            ContainerDiagramSettingsDto? diagram)
        {
            if (diagram?.Enabled != true) return null;
            var blocks = await GetBySectionAsync(pageId, sectionId);
            var validAnchors = blocks
                .Where(block => string.Equals(block.ParentBlockId, containerId, StringComparison.Ordinal))
                .Select(block => block.StableId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            var requestedAnchors = (diagram.Decorations ?? new())
                .SelectMany(item => new[] { item.FromAnchor, item.ToAnchor })
                .Concat((diagram.Connectors ?? new()).SelectMany(item => new[] { item.FromAnchor, item.ToAnchor }))
                .Where(value => !string.IsNullOrWhiteSpace(value) && value != "center")
                .Cast<string>()
                .Distinct(StringComparer.Ordinal);
            var missing = requestedAnchors.Where(anchor => !validAnchors.Contains(anchor)).ToList();
            return missing.Count == 0
                ? null
                : "Diagram connectors must reference children of the current Container.";
        }

        public async Task<string?> ValidateDiagramDeletionAsync(string pageId, string sectionId, string blockId)
        {
            var block = await GetByIdAsync(pageId, sectionId, blockId);
            if (block is null || string.IsNullOrWhiteSpace(block.ParentBlockId)) return null;
            var parent = await GetByIdAsync(pageId, sectionId, block.ParentBlockId);
            if (parent is not ContainerBlock container || container.ContainerLayout.Diagram?.Enabled != true)
                return null;
            var anchor = block.StableId;
            var isReferenced = container.ContainerLayout.Diagram.Decorations.Any(item =>
                    item.FromAnchor == anchor || item.ToAnchor == anchor) ||
                container.ContainerLayout.Diagram.Connectors.Any(item =>
                    item.FromAnchor == anchor || item.ToAnchor == anchor);
            return isReferenced
                ? "Remove diagram connectors that reference this Block before deleting it."
                : null;
        }

        public async Task<string?> ValidateFunctionalReferencesAsync(object dto, bool requireComplete)
        {
            var formIds = new HashSet<string>(StringComparer.Ordinal);
            string? actionError = null;

            switch (dto)
            {
                case FormBlockCreateDto form when !string.IsNullOrWhiteSpace(form.FormDefinitionId):
                    formIds.Add(form.FormDefinitionId);
                    break;
                case FormBlockUpdateDto form:
                    if (string.IsNullOrWhiteSpace(form.FormDefinitionId))
                    {
                        if (requireComplete) return "Choose an active Form Definition for the Form Block.";
                    }
                    else formIds.Add(form.FormDefinitionId);
                    break;
                case ButtonBlockCreateDto button:
                    actionError = ValidateAction(button.Action, button.Href, button.FormDefinitionId, requireComplete);
                    AddFormReference(button.Action, button.FormDefinitionId, formIds);
                    break;
                case ButtonBlockUpdateDto button:
                    actionError = ValidateAction(button.Action, button.Href, button.FormDefinitionId, requireComplete);
                    AddFormReference(button.Action, button.FormDefinitionId, formIds);
                    break;
                case CardBlockCreateDto card:
                    var createCardHasAction = card.ButtonLabel.Values.Any(value => !string.IsNullOrWhiteSpace(value));
                    actionError = ValidateAction(card.Action, card.Href, card.FormDefinitionId, requireComplete && createCardHasAction);
                    AddFormReference(card.Action, card.FormDefinitionId, formIds);
                    break;
                case CardBlockUpdateDto card:
                    var updateCardHasAction = card.ButtonLabel.Values.Any(value => !string.IsNullOrWhiteSpace(value));
                    actionError = ValidateAction(card.Action, card.Href, card.FormDefinitionId, requireComplete && updateCardHasAction);
                    AddFormReference(card.Action, card.FormDefinitionId, formIds);
                    break;
                case IconBlockCreateDto icon when icon.ActionEnabled:
                    actionError = ValidateAction(icon.Action, icon.Href, icon.FormDefinitionId, requireComplete);
                    AddFormReference(icon.Action, icon.FormDefinitionId, formIds);
                    break;
                case IconBlockUpdateDto icon when icon.ActionEnabled:
                    actionError = ValidateAction(icon.Action, icon.Href, icon.FormDefinitionId, requireComplete);
                    AddFormReference(icon.Action, icon.FormDefinitionId, formIds);
                    break;
            }

            if (actionError is not null) return actionError;

            var buttons = dto switch
            {
                BlockCreateDto create => create.Buttons,
                BlockUpdateDto update => update.Buttons ?? new(),
                _ => new List<BlockButtonDto>()
            };
            foreach (var button in buttons.Where(button => button.Visible))
            {
                var error = ValidateAction(button.Action, button.Href, button.FormDefinitionId, requireComplete);
                if (error is not null) return error;
                if (button.Action == BlockButtonAction.OpenForm && !string.IsNullOrWhiteSpace(button.FormDefinitionId))
                    formIds.Add(button.FormDefinitionId);
            }

            if (formIds.Count == 0) return null;
            var activeCount = await _context.FormDefinitions
                .CountDocumentsAsync(form => formIds.Contains(form.Id) && form.Active);
            return activeCount == formIds.Count
                ? null
                : "Every Form action must reference an active Form Definition.";
        }
        public async Task<Block> CreateAsync(string pageId, string sectionId, BlockCreateDto dto)
        {
            // Fetch page and section to get their StableIds
            var page = await _context.PagesDraft
                .Find(p => p.Id == pageId).FirstOrDefaultAsync()
                ?? throw new ArgumentException("Page not found");
            var section = await _context.SectionsDraft
                .Find(s => s.Id == sectionId).FirstOrDefaultAsync()
                ?? throw new ArgumentException("Section not found");

            ContainerPresetDefinition? containerPreset = null;
            if (dto is ContainerBlockCreateDto containerCreate)
                containerPreset = PrepareContainerPresetForCreation(containerCreate);

            Block block = dto switch
            {
                TextBlockCreateDto t => new TextBlock
                {
                    Title = SanitizeDictionary(t.Title),
                    Content = SanitizeDictionary(t.Content)
                },
                ImageBlockCreateDto img => new ImageBlock
                {
                    Asset = BlockAssetMetadataService.ToModel(img.Asset),
                    AltText = SanitizeDictionary(img.AltText),
                    Caption = SanitizeDictionary(img.Caption),
                    OpenInLightbox = img.OpenInLightbox,
                    FocalPointX = Math.Clamp(img.FocalPointX, 0, 100),
                    FocalPointY = Math.Clamp(img.FocalPointY, 0, 100)
                },
                VideoBlockCreateDto v => new VideoBlock
                {
                    Asset = BlockAssetMetadataService.ToModel(v.Asset),
                    SourceType = NormalizeVideoSourceType(v.SourceType),
                    Title = SanitizeDictionary(v.Title),
                    ShowControls = v.ShowControls,
                    Autoplay = v.Autoplay,
                    Muted = v.Autoplay || v.Muted,
                    Loop = v.Loop
                },
                FileBlockCreateDto f => new FileBlock
                {
                    Asset = BlockAssetMetadataService.ToModel(f.Asset),
                    Filename = f.Filename,
                    DisplayName = SanitizeDictionary(f.DisplayName),
                    OpenBehavior = NormalizeFileOpenBehavior(f.OpenBehavior)
                },
                MapBlockCreateDto m => new MapBlock
                {
                    CenterLat = m.CenterLat,
                    CenterLng = m.CenterLng,
                    DefaultZoom = m.DefaultZoom,
                    Pins = m.Pins.Select(p => new MapPin
                    {
                        Id = string.IsNullOrEmpty(p.Id)
                            ? ObjectId.GenerateNewId().ToString() : p.Id,
                        Label = p.Label,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Href = CleanUrl(p.Href),
                        Visible = p.Visible,
                        Order = p.Order
                    }).ToList()
                },
                FormBlockCreateDto form => new FormBlock
                {
                    FormDefinitionId = string.IsNullOrWhiteSpace(form.FormDefinitionId) ? null : form.FormDefinitionId,
                    DesignSchemaVersion = FormDesignPolicy.CurrentSchemaVersion,
                    FormScale = Math.Clamp(form.FormScale, FormBlockLayoutPolicy.MinimumScale, FormBlockLayoutPolicy.MaximumScale)
                },
                CardBlockCreateDto card => new CardBlock
                {
                    Icon = card.Icon,
                    Title = card.Title,
                    Description = SanitizeDictionary(card.Description),
                    Asset = BlockAssetMetadataService.ToModel(card.Asset),
                    ImageAltText = SanitizeDictionary(card.ImageAltText),
                    ButtonLabel = card.ButtonLabel,
                    Href = CleanUrl(card.Href),
                    Action = NormalizeBlockAction(card.Action),
                    FormDefinitionId = IsOpenFormAction(card.Action) ? card.FormDefinitionId : null,
                    ButtonStyle = NormalizeButtonStyle(card.ButtonStyle)
                },
                ButtonBlockCreateDto button => new ButtonBlock
                {
                    Icon = button.Icon,
                    IconPosition = NormalizeIconPosition(button.IconPosition),
                    Label = button.Label,
                    Href = CleanUrl(button.Href),
                    Action = NormalizeBlockAction(button.Action),
                    FormDefinitionId = IsOpenFormAction(button.Action) ? button.FormDefinitionId : null,
                    Style = NormalizeButtonStyle(button.Style)
                },
                MetricBlockCreateDto metric => new MetricBlock
                {
                    Icon = metric.Icon,
                    Label = metric.Label,
                    Value = metric.Value,
                    Prefix = metric.Prefix,
                    Suffix = metric.Suffix,
                    Description = SanitizeDictionary(metric.Description)
                },
                BulletListBlockCreateDto list => new BulletListBlock
                {
                    Title = list.Title,
                    Items = list.Items.Select(MapBulletItem).ToList()
                },
                StepBlockCreateDto step => new StepBlock
                {
                    Icon = step.Icon,
                    AutoNumber = step.AutoNumber,
                    StepLabel = step.StepLabel,
                    Title = step.Title,
                    Description = SanitizeDictionary(step.Description)
                },
                IconBlockCreateDto icon => new IconBlock
                {
                    Icon = icon.Icon,
                    Label = icon.Label,
                    Description = SanitizeDictionary(icon.Description),
                    ActionEnabled = icon.ActionEnabled,
                    Href = icon.ActionEnabled ? CleanUrl(icon.Href) : null,
                    Action = NormalizeBlockAction(icon.Action),
                    FormDefinitionId = icon.ActionEnabled && IsOpenFormAction(icon.Action) ? icon.FormDefinitionId : null
                },
                ContainerBlockCreateDto container => new ContainerBlock
                {
                    PresetKey = containerPreset!.Key,
                    Title = SanitizeDictionary(container.Title),
                    ContainerLayout = BlockContractService.MergeContainerLayout(
                        null,
                        container.ContainerLayout)
                },
                _ => throw new ArgumentException("Unknown block type")
            };

            block.StableId = Guid.NewGuid().ToString();
            block.ColumnSlotId = dto.ColumnSlotId;
            block.ParentBlockId = string.IsNullOrWhiteSpace(dto.ParentBlockId) ? null : dto.ParentBlockId;
            block.Authoring = MapAuthoring(dto.Authoring);
            Block? parentBlock = null;
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId))
                parentBlock = await GetByIdAsync(pageId, sectionId, block.ParentBlockId);
            if (!string.IsNullOrWhiteSpace(block.ParentBlockId) && parentBlock is not ContainerBlock)
                throw new ArgumentException("The selected parent must be a Container in the same Page and Section.");

            if (parentBlock is ContainerBlock governedParent)
            {
                var existingChildren = await GetDirectChildrenAsync(governedParent);
                if (!ContainerCapacityPolicy.CanOwn(BlockType(block)))
                    throw new ArgumentException("Containers cannot own another Container. Add a normal content Block instead.");
                EnforceContainerPresetChildType(governedParent, BlockType(block));
                EnforceContainerCapacity(governedParent, existingChildren);
                await EnforceCollectionChildTypeAsync(governedParent, BlockType(block));
                AssignContainerPresetSlot(governedParent, block, existingChildren);
            }

            block.BlockZone = parentBlock is null
                ? ResolveBlockZone(section, dto.BlockZone, dto.ZoneId)
                : "default";
            block.PositionMode = !string.IsNullOrWhiteSpace(dto.PositionMode)
                ? NormalizePositionMode(dto.PositionMode)
                : parentBlock is ContainerBlock parentContainer
                    ? NormalizeContainerLayout(parentContainer.ContainerLayout.Mode) == "freeform" ? "freeform" : "flow"
                    : ResolvePositionMode(section, null);
            block.PageStableId = page.StableId;       // â† GUID
            block.SectionStableId = section.StableId; // â† GUID
            block.Visible = dto.Visible;
            block.EditorLabel = SanitizeDictionary(dto.EditorLabel);
            block.Layout = MapLayout(dto.Layout);
            if (block is FormBlock createdForm && !string.IsNullOrWhiteSpace(createdForm.FormDefinitionId))
            {
                var definition = await _context.FormDefinitions
                    .Find(item => item.Id == createdForm.FormDefinitionId && item.Active)
                    .FirstOrDefaultAsync()
                    ?? throw new ArgumentException("Choose an active Form Definition for the Form Block.");
                var design = GovernedFormDesign(definition);
                createdForm.DesignSchemaVersion = design.SchemaVersion;
                var defaultSize = FormBlockLayoutPolicy.CalculateDefaultSize(
                    design,
                    FormBlockLayoutPolicy.AvailableContentWidthPx(section.Style?.ContentWidth));
                createdForm.DefaultWidthPercent = defaultSize.WidthPercent;
                createdForm.DefaultWidthPx = defaultSize.WidthPx;
                createdForm.DefaultHeightPx = defaultSize.HeightPx;
                block.Layout = MapLayout(BuildGovernedFormLayout(block.Layout, defaultSize));
            }
            block.Appearance = BlockContractService.MergeAppearance(null, dto.Appearance);
            if (parentBlock is ContainerBlock)
                block.Appearance.InheritFromContainer = dto.Appearance?.InheritFromContainer ?? true;
            block.Responsive = BlockContractService.MergeResponsive(null, dto.Responsive);
            block.Animation = BlockContractService.MergeAnimation(null, dto.Animation);

            var sectionBlocks = await _context.BlocksDraft
                .Find(candidate => candidate.PageStableId == page.StableId &&
                                   candidate.SectionStableId == section.StableId)
                .ToListAsync();
            var peerBlocks = sectionBlocks
                .Where(candidate => NormalizeParent(candidate.ParentBlockId) == NormalizeParent(block.ParentBlockId))
                .Where(candidate => NormalizeBlockZone(candidate.BlockZone) == NormalizeBlockZone(block.BlockZone))
                .Where(candidate => NormalizeSlot(candidate.ColumnSlotId) == NormalizeSlot(block.ColumnSlotId))
                .ToList();
            block.Order = peerBlocks.Select(candidate => candidate.Order).DefaultIfEmpty(-1).Max() + 1;

            ApplyDefaultFreeformPlacement(block, section, parentBlock, peerBlocks.Count, dto.Layout);
            if (dto.Layout?.ZIndex is null && dto.Layout?.ZOrder is null)
                block.Layout.ZIndex = Math.Clamp(
                    peerBlocks.Select(candidate => candidate.Layout?.ZIndex ?? 1).DefaultIfEmpty(0).Max() + 1,
                    1,
                    1000);
            block.Version = 1;
            block.CreatedAt = DateTime.UtcNow;
            block.UpdatedAt = DateTime.UtcNow;
            block.Buttons = dto.Buttons?.Select(MapButton).ToList() ?? new();

            await _context.BlocksDraft.InsertOneAsync(block);
            if (block is FormBlock { DefaultHeightPx: > 0 })
            {
                await GrowSectionForGovernedFormAsync(section, new BlockLayoutDto
                {
                    Y = block.Layout.Y,
                    H = block.Layout.H,
                    TopPx = block.Layout.TopPx,
                    HeightPx = block.Layout.HeightPx
                });
            }
            return block;
        }

        public async Task<string?> PrepareContainerPolicyUpdateAsync(
            string pageId,
            string sectionId,
            string blockId,
            ContainerLayoutSettingsDto? incoming,
            string? requestedPresetKey)
        {
            var existing = await GetByIdAsync(pageId, sectionId, blockId);
            if (existing is not ContainerBlock container) return "Container not found.";

            var effectiveKey = ContainerPresetCatalog.EffectiveKey(container.PresetKey);
            var targetKey = string.IsNullOrWhiteSpace(requestedPresetKey)
                ? effectiveKey
                : requestedPresetKey;
            if (!ContainerPresetCatalog.TryGetGoverned(targetKey, out var preset))
                return "Choose a supported Container preset.";
            if (incoming is null) return null;

            var children = (await GetBySectionAsync(pageId, sectionId))
                .Where(block => block.ParentBlockId == blockId)
                .ToList();

            if (!ContainerCapacityPolicy.CanConvert(targetKey, children.Select(BlockType), out var conversionError))
                return conversionError;

            incoming.SchemaVersion = 3;
            incoming.Mode = preset.LayoutMode;
            incoming.Purpose = preset.Purpose;
            incoming.Columns = preset.Columns;
            incoming.MobileMode = preset.MobileMode;

            var requestedMode = preset.LayoutMode;
            var capacity = preset.MaximumChildren;
            if (children.Count > capacity)
                return $"The {requestedMode} Container supports at most {capacity} direct Blocks. Remove Blocks before changing this layout.";

            var purpose = preset.Purpose;
            if (purpose == "composition")
            {
                incoming.Purpose = "composition";
                incoming.AllowedChildType = null;
                return null;
            }

            var childTypes = children.Select(BlockType).Distinct(StringComparer.Ordinal).ToList();
            if (childTypes.Count > 1)
                return "A Collection Container can contain only one Block type. Remove mixed children before changing its purpose.";

            var lockedType = childTypes.FirstOrDefault() ?? container.ContainerLayout.AllowedChildType;
            if (children.Count == 0)
                lockedType = null;
            if (!string.IsNullOrWhiteSpace(incoming.AllowedChildType) &&
                !string.IsNullOrWhiteSpace(lockedType) &&
                !string.Equals(incoming.AllowedChildType, lockedType, StringComparison.Ordinal))
                return "Collection child type is locked after the first child is added.";

            incoming.Purpose = "collection";
            incoming.AllowedChildType = lockedType;
            return null;
        }

        private async Task EnforceCollectionChildTypeAsync(ContainerBlock parent, string childType)
        {
            if (NormalizeContainerPurpose(parent.ContainerLayout.Purpose) != "collection") return;
            var allowedType = parent.ContainerLayout.AllowedChildType;
            if (string.IsNullOrWhiteSpace(allowedType))
            {
                var existingChildTypes = (await _context.BlocksDraft
                        .Find(block => block.PageStableId == parent.PageStableId &&
                                       block.SectionStableId == parent.SectionStableId &&
                                       block.ParentBlockId == parent.Id)
                        .ToListAsync())
                    .Select(BlockType)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (existingChildTypes.Count > 1)
                    throw new ArgumentException("This Collection already contains mixed Block types and must be changed to Composition.");
                allowedType = existingChildTypes.FirstOrDefault();
            }
            if (!string.IsNullOrWhiteSpace(allowedType) &&
                !string.Equals(allowedType, childType, StringComparison.Ordinal))
                throw new ArgumentException($"This Collection accepts only {allowedType} Blocks.");

            if (!string.IsNullOrWhiteSpace(parent.ContainerLayout.AllowedChildType)) return;
            var lockType = allowedType ?? childType;
            var filter = Builders<Block>.Filter.And(
                Builders<Block>.Filter.Eq(block => block.Id, parent.Id),
                Builders<Block>.Filter.Or(
                    Builders<Block>.Filter.Eq("ContainerLayout.AllowedChildType", BsonNull.Value),
                    Builders<Block>.Filter.Eq("ContainerLayout.AllowedChildType", string.Empty),
                    Builders<Block>.Filter.Eq("ContainerLayout.AllowedChildType", lockType)));
            var schemaVersion = ContainerPresetCatalog.TryGetGoverned(parent.PresetKey, out _) ? 3 : 2;
            var update = Builders<Block>.Update
                .Set("ContainerLayout.SchemaVersion", schemaVersion)
                .Set("ContainerLayout.AllowedChildType", lockType)
                .Set(block => block.UpdatedAt, DateTime.UtcNow)
                .Inc(block => block.Version, 1);
            var result = await _context.BlocksDraft.UpdateOneAsync(filter, update);
            if (result.MatchedCount == 0)
                throw new ArgumentException("Another Block type locked this Collection. Refresh and try again.");
            parent.ContainerLayout.SchemaVersion = schemaVersion;
            parent.ContainerLayout.AllowedChildType = lockType;
        }

        private static void EnforceContainerPresetChildType(ContainerBlock parent, string childType)
        {
            if (!ContainerPresetCatalog.TryGetGoverned(parent.PresetKey, out var preset)) return;
            if (preset.AllowedBlockTypes.Contains(childType, StringComparer.Ordinal)) return;
            throw new ArgumentException(
                $"The {preset.DisplayName} Container preset does not allow {childType} Blocks.");
        }

        private async Task<List<Block>> GetDirectChildrenAsync(ContainerBlock parent) =>
            await _context.BlocksDraft
                .Find(block => block.PageStableId == parent.PageStableId &&
                               block.SectionStableId == parent.SectionStableId &&
                               block.ParentBlockId == parent.Id)
                .ToListAsync();

        private static void EnforceContainerCapacity(
            ContainerBlock parent,
            IReadOnlyCollection<Block> existingChildren)
        {
            var capacity = ContainerCapacityPolicy.MaxChildren(
                parent.PresetKey,
                parent.ContainerLayout.Mode,
                parent.ContainerLayout.Columns);
            if (existingChildren.Count >= capacity)
                throw new ArgumentException(
                    $"This {ContainerCapacityPolicy.NormalizeMode(parent.ContainerLayout.Mode)} Container is full ({capacity} Blocks maximum).");
        }

        private static void AssignContainerPresetSlot(
            ContainerBlock parent,
            Block child,
            IReadOnlyCollection<Block> existingChildren)
        {
            if (!ContainerPresetCatalog.TryGetGoverned(parent.PresetKey, out var preset) ||
                preset.Slots.Count == 0)
                return;

            var childType = BlockType(child);
            var slot = ContainerCapacityPolicy.NextAvailableSlot(
                parent.PresetKey,
                existingChildren.Select(block => block.Authoring?.PresetSlotName),
                childType);

            if (slot is null)
                throw new ArgumentException(
                    $"The {preset.DisplayName} Container has no available slot for {childType} Blocks.");

            child.Authoring ??= new BlockAuthoringPolicy();
            child.Authoring.SchemaVersion = Math.Max(child.Authoring.SchemaVersion, 1);
            child.Authoring.PresetSlotName = slot.Key;
            child.Authoring.PresetSourceId = parent.PresetKey;
        }

        private static BlockAuthoringPolicy MapAuthoring(BlockAuthoringPolicyDto? dto) => new()
        {
            SchemaVersion = Math.Max(dto?.SchemaVersion ?? 1, 1),
            ContentLocked = dto?.ContentLocked ?? false,
            GeometryLocked = (dto?.GeometryLocked ?? false) || (dto?.FullLocked ?? false),
            FullLocked = dto?.FullLocked ?? false,
            PresetSlotName = null,
            PresetSourceId = null
        };

        private static string NormalizeContainerPurpose(string? value) =>
            string.Equals(value, "collection", StringComparison.Ordinal) ? "collection" : "composition";

        private static ContainerPresetDefinition PrepareContainerPresetForCreation(
            ContainerBlockCreateDto container)
        {
            var requestedMode = container.ContainerLayout?.Mode;
            var resolvedKey = ContainerPresetCatalog.ResolveCreationKey(container.PresetKey, requestedMode);
            if (resolvedKey is null ||
                !ContainerPresetCatalog.TryGetGoverned(resolvedKey, out var preset))
            {
                throw new ArgumentException(
                    "Choose a supported Container preset.");
            }

            container.PresetKey = preset.Key;
            container.ContainerLayout ??= new ContainerLayoutSettingsDto();
            container.ContainerLayout.SchemaVersion = 3;
            container.ContainerLayout.Purpose = preset.Purpose;
            container.ContainerLayout.Mode = preset.LayoutMode;
            container.ContainerLayout.Columns = preset.Columns;
            container.ContainerLayout.MobileMode = preset.MobileMode;
            if (preset.Purpose != "collection")
                container.ContainerLayout.AllowedChildType = null;
            return preset;
        }

        private static string BlockType(Block block) => block switch
        {
            TextBlock => "text",
            ImageBlock => "image",
            VideoBlock => "video",
            FileBlock => "file",
            MapBlock => "map",
            FormBlock => "form",
            CardBlock => "card",
            ButtonBlock => "button",
            MetricBlock => "metric",
            BulletListBlock => "bullet-list",
            StepBlock => "step",
            IconBlock => "icon",
            ContainerBlock => "container",
            _ => "text"
        };

        public async Task<Block?> UpdateAsync(string pageId, string sectionId,
            string blockId, BlockUpdateDto dto)
        {
            var existing = await GetByIdAsync(pageId, sectionId, blockId);
            if (existing is null) return null;

            FormBlockDefaultSize? governedFormSize = null;
            Section? governedFormSection = null;
            int? governedFormSchemaVersion = null;
            if (existing is FormBlock existingForm && dto is FormBlockUpdateDto formUpdate)
            {
                if (string.IsNullOrWhiteSpace(formUpdate.FormDefinitionId))
                    throw new ArgumentException("Choose an active Form Definition for the Form Block.");

                var definition = await _context.FormDefinitions
                    .Find(item => item.Id == formUpdate.FormDefinitionId && item.Active)
                    .FirstOrDefaultAsync()
                    ?? throw new ArgumentException("Choose an active Form Definition for the Form Block.");
                var definitionChanged = !string.Equals(
                    existingForm.FormDefinitionId,
                    definition.Id,
                    StringComparison.Ordinal);
                var governedDesign = GovernedFormDesign(definition);
                governedFormSchemaVersion = governedDesign.SchemaVersion;
                var resetToDefinitionSize = definitionChanged ||
                    existingForm.DefaultWidthPx <= 0 || existingForm.DefaultHeightPx <= 0;
                if (resetToDefinitionSize)
                {
                    if (existing.Authoring?.FullLocked == true || existing.Authoring?.GeometryLocked == true)
                        throw new ArgumentException("Unlock this Block's geometry before changing its Form Definition.");

                    governedFormSection = await _context.SectionsDraft
                        .Find(section => section.Id == sectionId)
                        .FirstOrDefaultAsync()
                        ?? throw new ArgumentException("Section not found.");
                    governedFormSize = FormBlockLayoutPolicy.CalculateDefaultSize(
                        governedDesign,
                        FormBlockLayoutPolicy.AvailableContentWidthPx(governedFormSection.Style?.ContentWidth));
                    formUpdate.Layout = BuildGovernedFormLayout(existing.Layout, governedFormSize.Value);
                    formUpdate.FormScale = 1d;
                }
            }

            if (dto.ParentBlockId is not null)
            {
                var currentParentId = string.IsNullOrWhiteSpace(existing.ParentBlockId) ? null : existing.ParentBlockId;
                var requestedParentId = string.IsNullOrWhiteSpace(dto.ParentBlockId) ? null : dto.ParentBlockId;
                if (!string.Equals(currentParentId, requestedParentId, StringComparison.Ordinal))
                    throw new ArgumentException(
                        "A Block's Container membership is fixed after creation. Container-owned Blocks cannot be released or moved to another Container.");
            }

            var baseUpdates = new List<UpdateDefinition<Block>>
            {
                Builders<Block>.Update.Set(b => b.UpdatedAt, DateTime.UtcNow),
                Builders<Block>.Update.Inc(b => b.Version, 1)
            };

            if (dto.Visible.HasValue)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Visible, dto.Visible.Value));
            if (dto.EditorLabel is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.EditorLabel, SanitizeDictionary(dto.EditorLabel)));
            if (dto.Buttons is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Buttons,
                    dto.Buttons.Select(MapButton).ToList()));
            var requestedZone = dto.ZoneId ?? dto.BlockZone;
            if (requestedZone is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.BlockZone, ResolveBlockZone(existing, requestedZone)));
            if (dto.PositionMode is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.PositionMode, NormalizePositionMode(dto.PositionMode)));
            if (dto.ParentBlockId is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.ParentBlockId, string.IsNullOrWhiteSpace(dto.ParentBlockId) ? null : dto.ParentBlockId));
            if (dto.Layout is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Layout, MapLayout(dto.Layout)));
            if (dto.Appearance is not null || dto.Layout is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Appearance,
                    BlockContractService.MergeAppearance(existing.Appearance, dto.Appearance)));
            if (dto.Responsive is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Responsive,
                    BlockContractService.MergeResponsive(existing.Responsive, dto.Responsive)));
            if (dto.Animation is not null)
                baseUpdates.Add(Builders<Block>.Update.Set(b => b.Animation,
                    BlockContractService.MergeAnimation(existing.Animation, dto.Animation)));

            var baseUpdate = Builders<Block>.Update.Combine(baseUpdates);

            switch (existing, dto)
            {
                case (TextBlock _, TextBlockUpdateDto tDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((TextBlock)b).Title, SanitizeDictionary(tDto.Title))
                                .Set(b => ((TextBlock)b).Content, SanitizeDictionary(tDto.Content))));
                    break;

                case (ImageBlock _, ImageBlockUpdateDto imgDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ImageBlock)b).Asset, BlockAssetMetadataService.ToModel(imgDto.Asset))
                                .Set(b => ((ImageBlock)b).AltText, SanitizeDictionary(imgDto.AltText))
                                .Set(b => ((ImageBlock)b).Caption, SanitizeDictionary(imgDto.Caption))
                                .Set(b => ((ImageBlock)b).OpenInLightbox, imgDto.OpenInLightbox)
                                .Set(b => ((ImageBlock)b).FocalPointX, Math.Clamp(imgDto.FocalPointX, 0, 100))
                                .Set(b => ((ImageBlock)b).FocalPointY, Math.Clamp(imgDto.FocalPointY, 0, 100))));
                    break;

                case (VideoBlock _, VideoBlockUpdateDto vDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((VideoBlock)b).Asset, BlockAssetMetadataService.ToModel(vDto.Asset))
                                .Set(b => ((VideoBlock)b).SourceType, NormalizeVideoSourceType(vDto.SourceType))
                                .Set(b => ((VideoBlock)b).Title, SanitizeDictionary(vDto.Title))
                                .Set(b => ((VideoBlock)b).ShowControls, vDto.ShowControls)
                                .Set(b => ((VideoBlock)b).Autoplay, vDto.Autoplay)
                                .Set(b => ((VideoBlock)b).Muted, vDto.Autoplay || vDto.Muted)
                                .Set(b => ((VideoBlock)b).Loop, vDto.Loop)));
                    break;

                case (FileBlock _, FileBlockUpdateDto fDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((FileBlock)b).Asset, BlockAssetMetadataService.ToModel(fDto.Asset))
                                .Set(b => ((FileBlock)b).Filename, fDto.Filename)
                                .Set(b => ((FileBlock)b).DisplayName, SanitizeDictionary(fDto.DisplayName))
                                .Set(b => ((FileBlock)b).OpenBehavior, NormalizeFileOpenBehavior(fDto.OpenBehavior))));
                    break;

                case (MapBlock _, MapBlockUpdateDto mDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((MapBlock)b).CenterLat, mDto.CenterLat)
                                .Set(b => ((MapBlock)b).CenterLng, mDto.CenterLng)
                                .Set(b => ((MapBlock)b).DefaultZoom, mDto.DefaultZoom)
                                .Set(b => ((MapBlock)b).Pins,
                                    mDto.Pins.Select(p => new MapPin
                                    {
                                        Id = string.IsNullOrEmpty(p.Id)
                                            ? ObjectId.GenerateNewId().ToString() : p.Id,
                                        Label = p.Label,
                                        Lat = p.Lat,
                                        Lng = p.Lng,
                                        Href = CleanUrl(p.Href),
                                        Visible = p.Visible,
                                        Order = p.Order
                                    }).ToList())));
                    break;

                case (FormBlock formBlock, FormBlockUpdateDto formDto):
                    var formUpdates = new List<UpdateDefinition<Block>>
                    {
                        Builders<Block>.Update.Set(b => ((FormBlock)b).FormDefinitionId,
                            string.IsNullOrWhiteSpace(formDto.FormDefinitionId) ? null : formDto.FormDefinitionId),
                        Builders<Block>.Update.Set(b => ((FormBlock)b).DesignSchemaVersion,
                            governedFormSchemaVersion ?? formBlock.DesignSchemaVersion),
                        Builders<Block>.Update.Set(b => ((FormBlock)b).FormScale,
                            Math.Clamp(formDto.FormScale ?? formBlock.FormScale,
                                FormBlockLayoutPolicy.MinimumScale,
                                FormBlockLayoutPolicy.MaximumScale)),
                        Builders<Block>.Update.Unset("Fields"),
                        Builders<Block>.Update.Unset("SubmitButtonLabel")
                    };
                    if (governedFormSize is { } formSize)
                    {
                        formUpdates.Add(Builders<Block>.Update.Set(
                            b => ((FormBlock)b).DefaultWidthPx,
                            formSize.WidthPx));
                        formUpdates.Add(Builders<Block>.Update.Set(
                            b => ((FormBlock)b).DefaultWidthPercent,
                            formSize.WidthPercent));
                        formUpdates.Add(Builders<Block>.Update.Set(
                            b => ((FormBlock)b).DefaultHeightPx,
                            formSize.HeightPx));
                    }
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(
                            new[] { baseUpdate }.Concat(formUpdates)));
                    break;

                case (CardBlock _, CardBlockUpdateDto cardDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((CardBlock)b).Icon, cardDto.Icon)
                                .Set(b => ((CardBlock)b).Title, cardDto.Title)
                                .Set(b => ((CardBlock)b).Description, SanitizeDictionary(cardDto.Description))
                                .Set(b => ((CardBlock)b).Asset, BlockAssetMetadataService.ToModel(cardDto.Asset))
                                .Set(b => ((CardBlock)b).ImageAltText, SanitizeDictionary(cardDto.ImageAltText))
                                .Set(b => ((CardBlock)b).ButtonLabel, cardDto.ButtonLabel)
                                .Set(b => ((CardBlock)b).Href, CleanUrl(cardDto.Href))
                                .Set(b => ((CardBlock)b).Action, NormalizeBlockAction(cardDto.Action))
                                .Set(b => ((CardBlock)b).FormDefinitionId,
                                    IsOpenFormAction(cardDto.Action) ? cardDto.FormDefinitionId : null)
                                .Set(b => ((CardBlock)b).ButtonStyle, NormalizeButtonStyle(cardDto.ButtonStyle))));
                    break;

                case (ButtonBlock _, ButtonBlockUpdateDto buttonDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ButtonBlock)b).Icon, buttonDto.Icon)
                                .Set(b => ((ButtonBlock)b).IconPosition, NormalizeIconPosition(buttonDto.IconPosition))
                                .Set(b => ((ButtonBlock)b).Label, buttonDto.Label)
                                .Set(b => ((ButtonBlock)b).Href, CleanUrl(buttonDto.Href))
                                .Set(b => ((ButtonBlock)b).Action, NormalizeBlockAction(buttonDto.Action))
                                .Set(b => ((ButtonBlock)b).FormDefinitionId,
                                    IsOpenFormAction(buttonDto.Action) ? buttonDto.FormDefinitionId : null)
                                .Set(b => ((ButtonBlock)b).Style, NormalizeButtonStyle(buttonDto.Style))));
                    break;

                case (MetricBlock _, MetricBlockUpdateDto metricDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((MetricBlock)b).Icon, metricDto.Icon)
                                .Set(b => ((MetricBlock)b).Label, metricDto.Label)
                                .Set(b => ((MetricBlock)b).Value, metricDto.Value)
                                .Set(b => ((MetricBlock)b).Prefix, metricDto.Prefix)
                                .Set(b => ((MetricBlock)b).Suffix, metricDto.Suffix)
                                .Set(b => ((MetricBlock)b).Description, SanitizeDictionary(metricDto.Description))));
                    break;

                case (BulletListBlock _, BulletListBlockUpdateDto listDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((BulletListBlock)b).Title, listDto.Title)
                                .Set(b => ((BulletListBlock)b).Items, listDto.Items.Select(MapBulletItem).ToList())));
                    break;

                case (StepBlock _, StepBlockUpdateDto stepDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((StepBlock)b).Icon, stepDto.Icon)
                                .Set(b => ((StepBlock)b).AutoNumber, stepDto.AutoNumber)
                                .Set(b => ((StepBlock)b).StepLabel, stepDto.StepLabel)
                                .Set(b => ((StepBlock)b).Title, stepDto.Title)
                                .Set(b => ((StepBlock)b).Description, SanitizeDictionary(stepDto.Description))));
                    break;

                case (IconBlock _, IconBlockUpdateDto iconDto):
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((IconBlock)b).Icon, iconDto.Icon)
                                .Set(b => ((IconBlock)b).Label, iconDto.Label)
                                .Set(b => ((IconBlock)b).Description, SanitizeDictionary(iconDto.Description))
                                .Set(b => ((IconBlock)b).ActionEnabled, iconDto.ActionEnabled)
                                .Set(b => ((IconBlock)b).Href, iconDto.ActionEnabled ? CleanUrl(iconDto.Href) : null)
                                .Set(b => ((IconBlock)b).Action, NormalizeBlockAction(iconDto.Action))
                                .Set(b => ((IconBlock)b).FormDefinitionId,
                                    iconDto.ActionEnabled && IsOpenFormAction(iconDto.Action) ? iconDto.FormDefinitionId : null)));
                    break;

                case (ContainerBlock existingContainer, ContainerBlockUpdateDto containerDto):
                    var nextPresetKey = string.IsNullOrWhiteSpace(containerDto.PresetKey)
                        ? existingContainer.PresetKey
                        : ContainerPresetCatalog.EffectiveKey(containerDto.PresetKey);
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId,
                        Builders<Block>.Update.Combine(baseUpdate,
                            Builders<Block>.Update
                                .Set(b => ((ContainerBlock)b).PresetKey, nextPresetKey)
                                .Set(b => ((ContainerBlock)b).Title, SanitizeDictionary(containerDto.Title))
                                .Set(b => ((ContainerBlock)b).ContainerLayout,
                                    BlockContractService.MergeContainerLayout(
                                        existingContainer.ContainerLayout,
                                        containerDto.ContainerLayout))));
                    if (!string.Equals(existingContainer.PresetKey, nextPresetKey, StringComparison.Ordinal))
                        await ReassignContainerPresetSlotsAsync(pageId, sectionId, existingContainer, nextPresetKey);
                    break;

                default:
                    await _context.BlocksDraft.UpdateOneAsync(b => b.Id == blockId, baseUpdate);
                    break;
            }

            await DeleteReplacedAssetAsync(existing, dto);

            if (governedFormSection is not null && dto.Layout is not null)
                await GrowSectionForGovernedFormAsync(governedFormSection, dto.Layout);

            return await GetByIdAsync(pageId, sectionId, blockId);
        }

        private async Task ReassignContainerPresetSlotsAsync(
            string pageId,
            string sectionId,
            ContainerBlock container,
            string? presetKey)
        {
            var children = (await GetBySectionAsync(pageId, sectionId))
                .Where(block => block.ParentBlockId == container.Id)
                .OrderBy(block => block.Order)
                .ToList();
            if (!ContainerPresetCatalog.TryGetGoverned(presetKey, out var preset)) return;

            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                var slot = preset.Slots.ElementAtOrDefault(index);
                var policy = child.Authoring ?? new BlockAuthoringPolicy();
                policy.SchemaVersion = Math.Max(policy.SchemaVersion, 1);
                policy.PresetSlotName = slot?.Key;
                policy.PresetSourceId = preset.Key;
                await _context.BlocksDraft.UpdateOneAsync(
                    block => block.Id == child.Id,
                    Builders<Block>.Update
                        .Set(block => block.Authoring, policy)
                        .Set(block => block.PositionMode, preset.LayoutMode == "freeform" ? "freeform" : "flow")
                        .Set(block => block.UpdatedAt, DateTime.UtcNow)
                        .Inc(block => block.Version, 1));
            }
        }

        public async Task<string?> ValidateParentAsync(
            string pageId,
            string sectionId,
            string? blockId,
            string? requestedParentId)
        {
            if (!string.IsNullOrWhiteSpace(blockId) && requestedParentId is not null)
            {
                var existing = await GetByIdAsync(pageId, sectionId, blockId);
                if (existing is null) return "Block not found.";
                var currentParentId = string.IsNullOrWhiteSpace(existing.ParentBlockId) ? null : existing.ParentBlockId;
                var normalizedParentId = string.IsNullOrWhiteSpace(requestedParentId) ? null : requestedParentId;
                if (!string.Equals(currentParentId, normalizedParentId, StringComparison.Ordinal))
                    return "A Block's Container membership is fixed after creation. Container-owned Blocks cannot be released or moved to another Container.";
            }

            if (string.IsNullOrWhiteSpace(requestedParentId)) return null;
            if (!string.IsNullOrWhiteSpace(blockId) && requestedParentId == blockId)
                return "A Block cannot be its own parent Container.";

            var parent = await GetByIdAsync(pageId, sectionId, requestedParentId);
            if (parent is not ContainerBlock)
                return "The selected parent must be a Container in the same Page and Section.";

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = parent;
            while (current is not null)
            {
                if (!visited.Add(current.Id))
                    return "The selected parent belongs to an invalid Container cycle.";
                if (!string.IsNullOrWhiteSpace(blockId) && current.Id == blockId)
                    return "The selected parent would create a Container cycle.";
                if (string.IsNullOrWhiteSpace(current.ParentBlockId)) break;
                current = await GetByIdAsync(pageId, sectionId, current.ParentBlockId);
            }

            return null;
        }

        public async Task<Block?> UpdateLayoutAsync(string pageId, string sectionId,
            string blockId, BlockLayoutDto dto)
        {
            var existing = await GetByIdAsync(pageId, sectionId, blockId);
            if (existing is null) return null;

            var nextLayout = MergeLayout(existing.Layout, dto);
            FormBlockDefaultSize? recoveredDefaultSize = null;
            int? recoveredDesignSchemaVersion = null;
            Section? formSection = null;
            if (existing is FormBlock form)
            {
                formSection = await _context.SectionsDraft
                    .Find(section => section.Id == sectionId)
                    .FirstOrDefaultAsync();
                var defaultWidthPercent = form.DefaultWidthPercent;
                var defaultHeightPx = form.DefaultHeightPx;
                if ((defaultWidthPercent <= 0 || defaultHeightPx <= 0) &&
                    formSection is not null &&
                    !string.IsNullOrWhiteSpace(form.FormDefinitionId))
                {
                    var definition = await _context.FormDefinitions
                        .Find(item => item.Id == form.FormDefinitionId && item.Active)
                        .FirstOrDefaultAsync();
                    if (definition is not null)
                    {
                        recoveredDefaultSize = FormBlockLayoutPolicy.CalculateDefaultSize(
                            GovernedFormDesign(definition),
                            FormBlockLayoutPolicy.AvailableContentWidthPx(formSection.Style?.ContentWidth));
                        recoveredDesignSchemaVersion = definition.Design?.SchemaVersion ?? FormDesignPolicy.CurrentSchemaVersion;
                        defaultWidthPercent = recoveredDefaultSize.Value.WidthPercent;
                        defaultHeightPx = recoveredDefaultSize.Value.HeightPx;
                    }
                }

                if (defaultWidthPercent > 0 && defaultHeightPx > 0)
                    nextLayout = GovernFormResize(existing.Layout, nextLayout, defaultWidthPercent, defaultHeightPx);
            }

            var updates = new List<UpdateDefinition<Block>>
            {
                Builders<Block>.Update.Set(b => b.Layout, nextLayout),
                Builders<Block>.Update.Set(b => b.Appearance,
                    BlockContractService.MergeAppearance(existing.Appearance, null)),
                Builders<Block>.Update.Set(b => b.UpdatedAt, DateTime.UtcNow),
                Builders<Block>.Update.Inc(b => b.Version, 1)
            };
            if (recoveredDefaultSize is { } recovered)
            {
                updates.Add(Builders<Block>.Update.Set(
                    b => ((FormBlock)b).DefaultWidthPx,
                    recovered.WidthPx));
                updates.Add(Builders<Block>.Update.Set(
                    b => ((FormBlock)b).DefaultWidthPercent,
                    recovered.WidthPercent));
                updates.Add(Builders<Block>.Update.Set(
                    b => ((FormBlock)b).DefaultHeightPx,
                    recovered.HeightPx));
            }
            if (existing is FormBlock formBlock)
            {
                updates.Add(Builders<Block>.Update.Set(
                    b => ((FormBlock)b).DesignSchemaVersion,
                    recoveredDesignSchemaVersion ?? formBlock.DesignSchemaVersion));
                var baselineWidth = recoveredDefaultSize?.WidthPercent ?? formBlock.DefaultWidthPercent;
                var baselineHeight = recoveredDefaultSize?.HeightPx ?? formBlock.DefaultHeightPx;
                if (baselineWidth > 0 && baselineHeight > 0)
                {
                    var resolvedScale = ResolveFormScale(nextLayout, baselineHeight);
                    updates.Add(Builders<Block>.Update.Set(b => ((FormBlock)b).FormScale, resolvedScale));
                }
            }

            await _context.BlocksDraft.UpdateOneAsync(
                b => b.Id == blockId,
                Builders<Block>.Update.Combine(updates));

            if (formSection is not null)
            {
                await GrowSectionForGovernedFormAsync(formSection, new BlockLayoutDto
                {
                    Y = nextLayout.Y,
                    H = nextLayout.H,
                    TopPx = nextLayout.TopPx,
                    HeightPx = nextLayout.HeightPx
                });
            }

            return await GetByIdAsync(pageId, sectionId, blockId);
        }


        private async Task DeleteReplacedAssetAsync(Block existing, BlockUpdateDto dto)
        {
            switch (existing, dto)
            {
                case (ImageBlock image, ImageBlockUpdateDto imageDto) when imageDto.Asset?.Url != null:
                    await _assetCleanup.DeleteIfUnusedAsync(image.Asset.Url, imageDto.Asset.Url);
                    break;
                case (FileBlock file, FileBlockUpdateDto fileDto) when fileDto.Asset?.Url != null:
                    await _assetCleanup.DeleteIfUnusedAsync(file.Asset.Url, fileDto.Asset.Url);
                    break;
                case (CardBlock card, CardBlockUpdateDto cardDto) when cardDto.Asset?.Url != null:
                    await _assetCleanup.DeleteIfUnusedAsync(card.Asset.Url, cardDto.Asset.Url);
                    break;
            }
        }

        private static BulletListItem MapBulletItem(BulletListItemDto item) => new()
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? ObjectId.GenerateNewId().ToString() : item.Id,
            Icon = item.Icon,
            Text = SanitizeDictionary(item.Text),
            Visible = item.Visible,
            Order = item.Order
        };

        private static string NormalizeButtonStyle(string? style) => style switch
        {
            "outline" => "outline",
            "ghost" => "ghost",
            _ => "filled"
        };

        private static string NormalizeIconPosition(string? position) =>
            string.Equals(position, "right", StringComparison.OrdinalIgnoreCase) ? "right" : "left";
        public async Task<bool> DeleteAsync(string pageId, string sectionId, string blockId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;
            var block = await _context.BlocksDraft.Find(b =>
                    b.PageStableId == page.StableId &&
                    b.SectionStableId == section.StableId &&
                    b.Id == blockId)
                .FirstOrDefaultAsync();
            if (block is null) return false;

            var removedAssetUrls = _assetCleanup.BlockAssetUrls(block).ToList();
            var result = await _context.BlocksDraft.DeleteOneAsync(
                b => b.PageStableId == page.StableId &&
                     b.SectionStableId == section.StableId &&
                     b.Id == blockId);
            if (result.DeletedCount > 0)
                await _assetCleanup.DeleteUnusedAsync(removedAssetUrls);
            return result.DeletedCount > 0;
        }

        public async Task DeleteBySectionAsync(string pageStableId, string sectionStableId)
        {
            var blocks = await _context.BlocksDraft
                .Find(b => b.PageStableId == pageStableId && b.SectionStableId == sectionStableId)
                .ToListAsync();
            var removedAssetUrls = blocks.SelectMany(_assetCleanup.BlockAssetUrls).ToList();

            await _context.BlocksDraft.DeleteManyAsync(
                b => b.PageStableId == pageStableId && b.SectionStableId == sectionStableId);

            await _assetCleanup.DeleteUnusedAsync(removedAssetUrls);
        }

        public async Task DeleteByColumnSlotsAsync(string pageStableId, string sectionStableId, IEnumerable<string> slotIds)
        {
            var ids = slotIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (ids.Count == 0) return;

            var blocks = await _context.BlocksDraft
                .Find(b => b.PageStableId == pageStableId &&
                           b.SectionStableId == sectionStableId &&
                           b.ColumnSlotId != null &&
                           ids.Contains(b.ColumnSlotId))
                .ToListAsync();
            var removedAssetUrls = blocks.SelectMany(_assetCleanup.BlockAssetUrls).ToList();

            var result = await _context.BlocksDraft.DeleteManyAsync(
                b => b.PageStableId == pageStableId &&
                     b.SectionStableId == sectionStableId &&
                     b.ColumnSlotId != null &&
                     ids.Contains(b.ColumnSlotId));
            if (result.DeletedCount > 0)
                await _assetCleanup.DeleteUnusedAsync(removedAssetUrls);
        }

        private static BlockLayoutDto BuildGovernedFormLayout(
            BlockLayout? current,
            FormBlockDefaultSize defaultSize)
        {
            current ??= new BlockLayout();
            var widthPercent = Math.Clamp(defaultSize.WidthPercent, 1d, 100d);
            var heightPx = Math.Clamp(
                (double)defaultSize.HeightPx,
                24d,
                FormBlockLayoutPolicy.MaximumHeightPx);
            var leftPercent = Math.Clamp(
                current.LeftPercent ?? (Math.Clamp(current.X, 0, 11) / 12d * 100d),
                0d,
                Math.Max(0d, 100d - widthPercent));
            var maximumTopPx = Math.Max(
                0d,
                FormBlockLayoutPolicy.MaximumSectionHeightPx -
                FormBlockLayoutPolicy.SectionBottomPaddingPx -
                heightPx);
            var topPx = Math.Clamp(
                current.TopPx ?? (Math.Clamp(current.Y, 0, 60) * 48d),
                0d,
                maximumTopPx);
            var x = Math.Clamp(
                (int)Math.Round(leftPercent / 100d * 12d),
                0,
                Math.Max(0, 12 - defaultSize.WidthUnits));

            return new BlockLayoutDto
            {
                Width = "custom",
                ColumnSpan = defaultSize.WidthUnits,
                Align = current.Align,
                Justify = current.Justify,
                Padding = current.Padding,
                Margin = current.Margin,
                BackgroundColor = current.BackgroundColor,
                BorderRadius = current.BorderRadius,
                ZIndex = current.ZIndex,
                X = x,
                Y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60),
                W = defaultSize.WidthUnits,
                H = Math.Clamp((int)Math.Ceiling(heightPx / 48d), 1, 40),
                LeftPercent = leftPercent,
                TopPx = topPx,
                WidthPercent = widthPercent,
                HeightPx = heightPx
            };
        }

        private static BlockLayout GovernFormResize(
            BlockLayout? current,
            BlockLayout requested,
            double defaultWidthPercent,
            double defaultHeightPx)
        {
            current ??= new BlockLayout();
            var safeDefaultWidth = Math.Clamp(defaultWidthPercent, 1d, 100d);
            var safeDefaultHeight = Math.Clamp(
                defaultHeightPx,
                24d,
                FormBlockLayoutPolicy.MaximumHeightPx);
            var requestedHeight = requested.HeightPx ?? (Math.Clamp(requested.H, 1, 40) * 48d);
            var requestedScale = Math.Clamp(
                requestedHeight / safeDefaultHeight,
                FormBlockLayoutPolicy.MinimumScale,
                FormBlockLayoutPolicy.MaximumScale);
            var widthPercent = safeDefaultWidth * requestedScale;
            var heightPx = safeDefaultHeight * requestedScale;
            var widthUnits = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);
            var leftPercent = Math.Clamp(
                requested.LeftPercent ?? (Math.Clamp(requested.X, 0, 11) / 12d * 100d),
                0d,
                Math.Max(0d, 100d - widthPercent));
            var maximumTopPx = Math.Max(
                0d,
                FormBlockLayoutPolicy.MaximumSectionHeightPx -
                FormBlockLayoutPolicy.SectionBottomPaddingPx -
                heightPx);
            var topPx = Math.Clamp(
                requested.TopPx ?? (Math.Clamp(requested.Y, 0, 60) * 48d),
                0d,
                maximumTopPx);

            requested.Width = "custom";
            requested.ColumnSpan = widthUnits;
            requested.X = Math.Clamp(
                (int)Math.Round(leftPercent / 100d * 12d),
                0,
                Math.Max(0, 12 - widthUnits));
            requested.Y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60);
            requested.W = widthUnits;
            requested.H = Math.Clamp((int)Math.Ceiling(heightPx / 48d), 1, 40);
            requested.LeftPercent = leftPercent;
            requested.TopPx = topPx;
            requested.WidthPercent = widthPercent;
            requested.HeightPx = heightPx;
            return requested;
        }

        private static double ResolveFormScale(
            BlockLayout layout,
            double defaultHeightPx)
        {
            var height = layout.HeightPx ?? (Math.Clamp(layout.H, 1, 40) * 48d);
            return Math.Clamp(
                height / Math.Max(24d, defaultHeightPx),
                FormBlockLayoutPolicy.MinimumScale,
                FormBlockLayoutPolicy.MaximumScale);
        }

        private static FormDesignSettingsDto GovernedFormDesign(FormDefinition definition)
        {
            var source = FormDefinitionService.MapDesign(definition.Design);
            if (source.V2 is null)
                throw new InvalidOperationException($"Form Definition '{definition.Key}' does not contain a schema-v2 design.");
            var fields = definition.Fields.OrderBy(field => field.Order).Select(field => new FormFieldDefinitionDto
                {
                    Key = field.Key,
                    Type = field.Type,
                    Label = field.Label,
                    Placeholder = field.Placeholder,
                    Required = field.Required,
                    InputBoxSize = field.InputBoxSize,
                    Order = field.Order
                }).ToList();
            return FormDefinitionService.NormalizeV2WriteDesign(
                definition.Id,
                source,
                fields,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel,
                FormDefinitionService.MapInformationItems(definition.InformationItems),
                FormDefinitionService.MapAuxiliaryActions(definition.AuxiliaryActions));
        }

        private async Task GrowSectionForGovernedFormAsync(Section section, BlockLayoutDto layout)
        {
            var topPx = Math.Max(0d, layout.TopPx ?? ((layout.Y ?? 0) * 48d));
            var heightPx = Math.Max(24d, layout.HeightPx ?? ((layout.H ?? 1) * 48d));
            var desiredHeightPx = Math.Clamp(
                (int)Math.Ceiling(topPx + heightPx + FormBlockLayoutPolicy.SectionBottomPaddingPx),
                120,
                FormBlockLayoutPolicy.MaximumSectionHeightPx);
            if (desiredHeightPx <= (section.Style?.CustomMinHeightPx ?? 0))
                return;

            await _context.SectionsDraft.UpdateOneAsync(
                item => item.Id == section.Id,
                Builders<Section>.Update
                    .Set(item => item.Style.Height, "custom")
                    .Set(item => item.Style.CustomMinHeightPx, desiredHeightPx)
                    .Set(item => item.UpdatedAt, DateTime.UtcNow)
                    .Inc(item => item.Version, 1));
        }

        private static BlockLayout MapLayout(BlockLayoutDto? dto)
        {
            if (dto is null) return new BlockLayout();

            return new BlockLayout
            {
                Width = NormalizeChoice(dto.Width, new[] { "auto", "full", "half", "third", "custom" }, "auto"),
                ColumnSpan = Math.Clamp(dto.ColumnSpan ?? 12, 1, 12),
                Align = NormalizeChoice(dto.Align, new[] { "stretch", "start", "center", "end" }, "stretch"),
                Justify = NormalizeChoice(dto.Justify, new[] { "start", "center", "end" }, "start"),
                Padding = NormalizeChoice(dto.Padding, new[] { "none", "small", "medium", "large" }, "none"),
                Margin = NormalizeChoice(dto.Margin, new[] { "none", "small", "medium", "large" }, "none"),
                BackgroundColor = string.IsNullOrWhiteSpace(dto.BackgroundColor) ? null : dto.BackgroundColor,
                BorderRadius = NormalizeChoice(dto.BorderRadius, new[] { "none", "small", "medium", "large" }, "none"),
                ZIndex = Math.Clamp(dto.ZOrder ?? dto.ZIndex ?? 1, 0, 1000),
                X = Math.Clamp(dto.X ?? 0, 0, 11),
                Y = Math.Clamp(dto.Y ?? 0, 0, 60),
                W = Math.Clamp(dto.W ?? 4, 1, 12),
                H = Math.Clamp(dto.H ?? 2, 1, 40),
                LeftPercent = ClampDouble(dto.LeftPercent, 0, 100),
                TopPx = ClampDouble(dto.TopPx, 0, 10000),
                WidthPercent = ClampDouble(dto.WidthPercent, 1, 100),
                HeightPx = ClampDouble(dto.HeightPx, 24, 10000)
            };
        }

        private static void ApplyDefaultFreeformPlacement(
            Block block,
            Section section,
            Block? parentBlock,
            int existingPeerCount,
            BlockLayoutDto? requestedLayout)
        {
            if (!string.Equals(block.PositionMode, "freeform", StringComparison.OrdinalIgnoreCase))
                return;

            var hasExplicitPosition = requestedLayout?.LeftPercent.HasValue == true ||
                                      requestedLayout?.TopPx.HasValue == true ||
                                      requestedLayout?.X.HasValue == true ||
                                      requestedLayout?.Y.HasValue == true;
            if (hasExplicitPosition) return;

            var zoneHeight = parentBlock?.Layout?.HeightPx ??
                             (parentBlock?.Layout is not null ? parentBlock.Layout.H * 48d : (double?)null) ??
                             section.Style?.CustomMinHeightPx ??
                             640d;
            zoneHeight = Math.Clamp(zoneHeight, 120d, 3000d);

            var widthPercent = block.Layout.WidthPercent ?? block.Layout.W / 12d * 100d;
            var heightPx = block.Layout.HeightPx ?? block.Layout.H * 48d;
            widthPercent = Math.Clamp(widthPercent, 1d, 100d);
            heightPx = Math.Clamp(heightPx, 24d, zoneHeight);

            var offsetStep = (existingPeerCount + 1) / 2;
            var offsetDirection = existingPeerCount % 2 == 0 ? -1d : 1d;
            var leftOffset = existingPeerCount == 0 ? 0d : offsetDirection * offsetStep * 2d;
            var topOffset = existingPeerCount == 0 ? 0d : offsetStep * 14d;
            var leftPercent = Math.Clamp((100d - widthPercent) / 2d + leftOffset, 0d, 100d - widthPercent);
            var topPx = Math.Clamp((zoneHeight - heightPx) / 2d + topOffset, 0d, zoneHeight - heightPx);

            block.Layout.LeftPercent = leftPercent;
            block.Layout.TopPx = topPx;
            block.Layout.WidthPercent = widthPercent;
            block.Layout.HeightPx = heightPx;
            block.Layout.X = Math.Clamp((int)Math.Round(leftPercent / 100d * 12d), 0, 11);
            block.Layout.Y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60);
            block.Layout.W = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);
            block.Layout.H = Math.Clamp((int)Math.Round(heightPx / 48d), 1, 40);
        }

        private static BlockLayout MergeLayout(BlockLayout? current, BlockLayoutDto dto)
        {
            current ??= new BlockLayout();

            return new BlockLayout
            {
                Width = dto.Width is null
                    ? current.Width
                    : NormalizeChoice(dto.Width, new[] { "auto", "full", "half", "third", "custom" }, "auto"),
                ColumnSpan = Math.Clamp(dto.ColumnSpan ?? current.ColumnSpan, 1, 12),
                Align = dto.Align is null
                    ? current.Align
                    : NormalizeChoice(dto.Align, new[] { "stretch", "start", "center", "end" }, "stretch"),
                Justify = dto.Justify is null
                    ? current.Justify
                    : NormalizeChoice(dto.Justify, new[] { "start", "center", "end" }, "start"),
                Padding = dto.Padding is null
                    ? current.Padding
                    : NormalizeChoice(dto.Padding, new[] { "none", "small", "medium", "large" }, "none"),
                Margin = dto.Margin is null
                    ? current.Margin
                    : NormalizeChoice(dto.Margin, new[] { "none", "small", "medium", "large" }, "none"),
                BackgroundColor = dto.BackgroundColor ?? current.BackgroundColor,
                BorderRadius = dto.BorderRadius is null
                    ? current.BorderRadius
                    : NormalizeChoice(dto.BorderRadius, new[] { "none", "small", "medium", "large" }, "none"),
                ZIndex = Math.Clamp(dto.ZOrder ?? dto.ZIndex ?? current.ZIndex, 0, 1000),
                X = Math.Clamp(dto.X ?? current.X, 0, 11),
                Y = Math.Clamp(dto.Y ?? current.Y, 0, 60),
                W = Math.Clamp(dto.W ?? current.W, 1, 12),
                H = Math.Clamp(dto.H ?? current.H, 1, 40),
                LeftPercent = dto.LeftPercent.HasValue ? ClampDouble(dto.LeftPercent, 0, 100) : current.LeftPercent,
                TopPx = dto.TopPx.HasValue ? ClampDouble(dto.TopPx, 0, 10000) : current.TopPx,
                WidthPercent = dto.WidthPercent.HasValue ? ClampDouble(dto.WidthPercent, 1, 100) : current.WidthPercent,
                HeightPx = dto.HeightPx.HasValue ? ClampDouble(dto.HeightPx, 24, 10000) : current.HeightPx
            };
        }

        private static double? ClampDouble(double? value, double min, double max)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                return null;

            return Math.Clamp(value.Value, min, max);
        }

        private static string NormalizeChoice(string? value, IReadOnlyCollection<string> allowed, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return allowed.Contains(value) ? value : fallback;
        }

        private static string NormalizeBlockZone(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();

        private static string NormalizeParent(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizeSlot(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizePositionMode(string? value) =>
            string.Equals(value, "freeform", StringComparison.OrdinalIgnoreCase) ? "freeform" : "flow";

        private static string ResolvePositionMode(Section section, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return NormalizePositionMode(value);

            return section is CanvasSection ? "freeform" : "flow";
        }

        private static string ResolveBlockZone(Section section, string? blockZone, string? zoneId) =>
            ResolveBlockZone(section, zoneId ?? blockZone);

        private static string ResolveBlockZone(Section section, string? zone)
        {
            if (string.IsNullOrWhiteSpace(zone) && section is CanvasSection)
                return "canvas";

            return NormalizeBlockZone(zone);
        }

        private static string ResolveBlockZone(Block block, string? zone)
        {
            if (string.IsNullOrWhiteSpace(zone) && block.BlockZone == "canvas")
                return "canvas";

            return NormalizeBlockZone(zone);
        }

        private static string NormalizeContainerLayout(string? value) => value switch
        {
            "row" => "row",
            "grid" => "grid",
            "split" => "split",
            "orbit" => "orbit",
            "semicircle" => "semicircle",
            "freeform" => "freeform",
            _ => "stack"
        };

        private static string NormalizeVideoSourceType(string? value) =>
            string.Equals(value, "upload", StringComparison.OrdinalIgnoreCase) ? "upload" : "youtube";

        private static string NormalizeFileOpenBehavior(string? value) =>
            string.Equals(value, "download", StringComparison.OrdinalIgnoreCase) ? "download" : "open";

        private static string NormalizeBlockAction(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "openform" => "openForm",
            "downloadfile" => "downloadFile",
            "externalurl" => "externalUrl",
            _ => "linkToPage"
        };

        private static bool IsOpenFormAction(string? value) =>
            string.Equals(NormalizeBlockAction(value), "openForm", StringComparison.Ordinal);

        private static void AddFormReference(
            string? action,
            string? formDefinitionId,
            ISet<string> formIds)
        {
            if (IsOpenFormAction(action) && !string.IsNullOrWhiteSpace(formDefinitionId))
                formIds.Add(formDefinitionId);
        }

        private static string? ValidateAction(
            string? action,
            string? href,
            string? formDefinitionId,
            bool required)
        {
            var normalized = NormalizeBlockAction(action);
            if (normalized == "openForm")
                return required && string.IsNullOrWhiteSpace(formDefinitionId)
                    ? "Choose an active Form Definition for the Open Form action."
                    : null;
            if (!required && string.IsNullOrWhiteSpace(href)) return null;
            if (string.IsNullOrWhiteSpace(href)) return "The selected Button action requires a destination.";
            var cleaned = CleanUrl(href);
            if (cleaned is null) return "Button destinations must be safe http, https, or site-relative URLs.";
            if (normalized == "externalUrl" &&
                (!Uri.TryCreate(cleaned, UriKind.Absolute, out var external) || external.Scheme is not ("http" or "https")))
                return "External links require a complete http or https URL.";
            if (normalized == "linkToPage" && !cleaned.StartsWith("/", StringComparison.Ordinal) &&
                !cleaned.StartsWith("#", StringComparison.Ordinal))
                return "Internal page links must start with / or #.";
            return null;
        }

        private static string? ValidateAction(
            BlockButtonAction action,
            string? href,
            string? formDefinitionId,
            bool required) =>
            ValidateAction(action switch
            {
                BlockButtonAction.OpenForm => "openForm",
                BlockButtonAction.DownloadFile => "downloadFile",
                BlockButtonAction.ExternalUrl => "externalUrl",
                _ => "linkToPage"
            }, href, formDefinitionId, required);

        private static string NormalizeBlockGap(string? value) => value switch
        {
            "none" => "none",
            "small" => "small",
            "large" => "large",
            _ => "medium"
        };

        public async Task<bool> SetVisibilityAsync(string pageId, string sectionId,
            string blockId, bool visible)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;
            var result = await _context.BlocksDraft.UpdateOneAsync(
                b => b.PageStableId == page.StableId &&
                     b.SectionStableId == section.StableId &&
                     b.Id == blockId,
                Builders<Block>.Update
                    .Set(b => b.Visible, visible)
                    .Inc(b => b.Version, 1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ReorderAsync(string pageId, string sectionId, List<string> orderedIds)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;
            var section = await _context.SectionsDraft.Find(s => s.Id == sectionId).FirstOrDefaultAsync();
            if (section is null) return false;

            var cleanIds = orderedIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (cleanIds.Count != orderedIds.Count || cleanIds.Count == 0) return false;

            var sectionBlocks = await _context.BlocksDraft
                .Find(block => block.PageStableId == page.StableId &&
                               block.SectionStableId == section.StableId)
                .ToListAsync();
            var requestedBlocks = sectionBlocks
                .Where(block => cleanIds.Contains(block.Id, StringComparer.Ordinal))
                .ToList();
            if (requestedBlocks.Count != cleanIds.Count) return false;

            var first = requestedBlocks[0];
            var parent = NormalizeParent(first.ParentBlockId);
            var zone = NormalizeBlockZone(first.BlockZone);
            var slot = NormalizeSlot(first.ColumnSlotId);
            if (requestedBlocks.Any(block =>
                    NormalizeParent(block.ParentBlockId) != parent ||
                    NormalizeBlockZone(block.BlockZone) != zone ||
                    NormalizeSlot(block.ColumnSlotId) != slot))
                return false;

            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentContainer = sectionBlocks.OfType<ContainerBlock>()
                    .FirstOrDefault(container => container.Id == parent);
                if (parentContainer is not null &&
                    ContainerPresetCatalog.TryGetGoverned(parentContainer.PresetKey, out var preset) &&
                    !preset.ChildOrderingAllowed)
                    return false;
            }

            var peerIds = sectionBlocks
                .Where(block => NormalizeParent(block.ParentBlockId) == parent)
                .Where(block => NormalizeBlockZone(block.BlockZone) == zone)
                .Where(block => NormalizeSlot(block.ColumnSlotId) == slot)
                .Select(block => block.Id)
                .ToHashSet(StringComparer.Ordinal);
            if (!peerIds.SetEquals(cleanIds)) return false;

            var updatedAt = DateTime.UtcNow;
            var writes = cleanIds.Select((id, i) =>
                new UpdateOneModel<Block>(
                    Builders<Block>.Filter.Where(b =>
                        b.PageStableId == page.StableId &&
                        b.SectionStableId == section.StableId &&
                        b.Id == id),
                    Builders<Block>.Update
                        .Set(b => b.Order, i)
                        .Set(b => b.Layout.ZIndex, i + 1)
                        .Inc(b => b.Version, 1)
                        .Set(b => b.UpdatedAt, updatedAt))
            ).Cast<WriteModel<Block>>().ToList();

            var result = await _context.BlocksDraft.BulkWriteAsync(writes);
            return result.MatchedCount == cleanIds.Count;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PUBLIC USER SITE RENDER METHODS (PUBLISHED EXCLUSIVE)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public async Task<List<Block>> GetPublicByPageAsync(string pageStableId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId && b.Visible == true)
                .SortBy(b => b.Order)
                .ToListAsync();

        public async Task<List<Block>> GetPublicBySectionAsync(string pageStableId, string sectionStableId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId &&
                           b.SectionStableId == sectionStableId &&
                           b.Visible == true)
                .SortBy(b => b.Order)
                .ToListAsync();

        public async Task<Block?> GetPublicByIdAsync(string pageStableId, string blockId) =>
            await _context.BlocksPublished
                .Find(b => b.PageStableId == pageStableId && b.Id == blockId && b.Visible == true)
                .FirstOrDefaultAsync();

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static BlockButton MapButton(BlockButtonDto dto) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = dto.Label,
            Action = dto.Action,
            Href = CleanUrl(dto.Href),
            FormDefinitionId = dto.Action == BlockButtonAction.OpenForm ? dto.FormDefinitionId : null,
            Visible = dto.Visible,
            Order = dto.Order
        };

        private static string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)
                    || url.StartsWith("#", StringComparison.Ordinal)
                    ? url
                    : null;
            return uri.Scheme is "http" or "https" ? url : null;
        }

        private static string? CleanVideoUrl(string? url)
        {
            var cleaned = CleanUrl(url);
            if (string.IsNullOrWhiteSpace(cleaned)) return cleaned;
            return VideoUrlHelper.ToEmbedUrl(cleaned) ?? cleaned;
        }
    }
}


