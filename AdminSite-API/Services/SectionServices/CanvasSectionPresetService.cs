using Contracts.Admin;
using Contracts.Forms;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services.CloneServices;
using FullProject.Services.AssetService;
using FullProject.Services.FormServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.SectionServices;

public sealed class SectionPresetService
{
    private readonly MongoDbContext _context;
    private readonly PageGraphCloneService _cloneService;
    private readonly SectionPresetContractService _contracts;
    private readonly AssetCleanupService _assetCleanup;

    public SectionPresetService(
        MongoDbContext context,
        PageGraphCloneService cloneService,
        SectionPresetContractService contracts,
        AssetCleanupService assetCleanup)
    {
        _context = context;
        _cloneService = cloneService;
        _contracts = contracts;
        _assetCleanup = assetCleanup;
    }

    public async Task<List<SectionPreset>> GetAllAsync()
    {
        var presets = await _context.SectionPresets
            .Find(_ => true)
            .SortByDescending(preset => preset.UpdatedAt)
            .ToListAsync();
        foreach (var preset in presets.Where(preset => _contracts.Compatibility(preset).IsCompatible))
            _contracts.UpgradeInPlace(preset);
        return presets;
    }

    public async Task<(SectionPreset? Preset, string? Error)> CreateFromSectionAsync(
        SectionPresetCreateDto dto)
    {
        var page = await _context.PagesDraft.Find(item => item.Id == dto.PageId).FirstOrDefaultAsync();
        if (page is null) return (null, "Page not found.");
        var source = await _context.SectionsDraft
            .Find(item => item.PageStableId == page.StableId && item.Id == dto.SectionId)
            .FirstOrDefaultAsync();
        if (source is null) return (null, "Section not found.");

        var sourceBlocks = await _context.BlocksDraft
            .Find(block => block.PageStableId == page.StableId && block.SectionStableId == source.StableId)
            .SortBy(block => block.Order)
            .ToListAsync();
        var snapshot = _cloneService.CloneSection(source, CloneProfile.PresetCapture);
        var capturedBlocks = _cloneService.CloneBlocksForPresetCapture(sourceBlocks);
        var now = DateTime.UtcNow;
        var preset = new SectionPreset
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = NormalizeLocalized(dto.Name),
            Description = NormalizeLocalized(dto.Description),
            SectionType = SectionPresetContractService.SectionType(snapshot),
            Section = snapshot,
            Style = snapshot.Style,
            Blocks = capturedBlocks,
            SchemaVersion = SectionPresetContractService.CurrentSchemaVersion,
            EditableSlots = new(),
            LockPolicy = new(),
            ThumbnailUrl = ResolveThumbnail(snapshot, capturedBlocks),
            ThumbnailBackground = ResolveThumbnailBackground(snapshot),
            PreviewText = ResolvePreviewText(snapshot),
            CreatedAt = now,
            UpdatedAt = now
        };

        var error = _contracts.PrepareAndValidate(preset);
        if (error is not null) return (null, error);
        await _context.SectionPresets.InsertOneAsync(preset);
        return (preset, null);
    }

    public async Task<(Section? Section, string? Error)> ApplyAsync(
        string presetId,
        SectionPresetApplyDto dto)
    {
        var preset = await _context.SectionPresets.Find(item => item.Id == presetId).FirstOrDefaultAsync();
        if (preset is null) return (null, "Section preset not found.");
        var error = _contracts.PrepareAndValidate(preset);
        if (error is not null) return (null, error);
        error = await ValidateLinkedReferencesAsync(preset);
        if (error is not null) return (null, error);
        var page = await _context.PagesDraft.Find(item => item.Id == dto.PageId).FirstOrDefaultAsync();
        if (page is null) return (null, "Page not found.");

        var count = await _context.SectionsDraft.CountDocumentsAsync(section => section.PageStableId == page.StableId);
        var now = DateTime.UtcNow;
        var section = _cloneService.CloneSection(preset.Section!, CloneProfile.PresetApply, now);
        section.SourceId = preset.Id;
        section.PageStableId = page.StableId;
        section.Visible = true;
        section.Order = (int)count;
        section.CreatedAt = now;
        section.UpdatedAt = now;
        var ownedIdMap = _cloneService.RegenerateSectionOwnedIds(section);

        var blocks = _cloneService.CloneBlocksForPresetApply(
            preset.Blocks,
            page.StableId,
            section.StableId,
            now);
        foreach (var block in blocks)
        {
            if (!string.IsNullOrWhiteSpace(block.ColumnSlotId) &&
                ownedIdMap.TryGetValue(block.ColumnSlotId, out var targetSlotId))
                block.ColumnSlotId = targetSlotId;
        }
        _contracts.ApplyPolicy(preset, blocks);
        await GovernAppliedFormBlocksAsync(section, blocks);

        await _context.SectionsDraft.InsertOneAsync(section);
        try
        {
            if (blocks.Count > 0) await _context.BlocksDraft.InsertManyAsync(blocks);
        }
        catch
        {
            await _context.BlocksDraft.DeleteManyAsync(block =>
                block.PageStableId == page.StableId && block.SectionStableId == section.StableId);
            await _context.SectionsDraft.DeleteOneAsync(item => item.Id == section.Id);
            throw;
        }
        return (section, null);
    }

    private async Task GovernAppliedFormBlocksAsync(Section section, IReadOnlyCollection<Block> blocks)
    {
        var forms = blocks.OfType<FormBlock>()
            .Where(form => !string.IsNullOrWhiteSpace(form.FormDefinitionId))
            .ToList();
        if (forms.Count == 0) return;

        var ids = forms.Select(form => form.FormDefinitionId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var definitions = await _context.FormDefinitions
            .Find(definition => definition.Active && ids.Contains(definition.Id))
            .ToListAsync();
        var byId = definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        var requiredSectionHeight = section.Style?.CustomMinHeightPx ?? 0;

        foreach (var form in forms)
        {
            if (!byId.TryGetValue(form.FormDefinitionId!, out var definition))
                continue;

            var source = FormDefinitionService.MapDesign(definition.Design);
            var fields = definition.Fields.Select(field => new FormFieldDefinitionDto
                {
                    Key = field.Key,
                    Type = field.Type,
                    Label = field.Label,
                    Placeholder = field.Placeholder,
                    Required = field.Required,
                    InputBoxSize = field.InputBoxSize,
                    Order = field.Order
                }).ToList();
            var design = source.V2 is not null
                ? FormDefinitionService.NormalizeV2WriteDesign(
                    definition.Id,
                    source,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel,
                    FormDefinitionService.MapInformationItems(definition.InformationItems),
                    FormDefinitionService.MapAuxiliaryActions(definition.AuxiliaryActions))
                : FormDesignPolicy.Normalize(
                    source,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel);
            var baseline = FormBlockLayoutPolicy.CalculateDefaultSize(
                design,
                FormBlockLayoutPolicy.AvailableContentWidthPx(section.Style?.ContentWidth));
            var scale = Math.Clamp(form.FormScale, FormBlockLayoutPolicy.MinimumScale, FormBlockLayoutPolicy.MaximumScale);
            var widthPercent = baseline.WidthPercent * scale;
            var heightPx = baseline.HeightPx * scale;
            var layout = form.Layout ?? new BlockLayout();
            var leftPercent = Math.Clamp(
                layout.LeftPercent ?? (Math.Clamp(layout.X, 0, 11) / 12d * 100d),
                0d,
                Math.Max(0d, 100d - widthPercent));
            var topPx = Math.Clamp(
                layout.TopPx ?? (Math.Clamp(layout.Y, 0, 60) * 48d),
                0d,
                Math.Max(0d, FormBlockLayoutPolicy.MaximumSectionHeightPx - FormBlockLayoutPolicy.SectionBottomPaddingPx - heightPx));
            var widthUnits = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);

            layout.Width = "custom";
            layout.ColumnSpan = widthUnits;
            layout.X = Math.Clamp((int)Math.Round(leftPercent / 100d * 12d), 0, Math.Max(0, 12 - widthUnits));
            layout.Y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60);
            layout.W = widthUnits;
            layout.H = Math.Clamp((int)Math.Ceiling(heightPx / 48d), 1, 40);
            layout.LeftPercent = leftPercent;
            layout.TopPx = topPx;
            layout.WidthPercent = widthPercent;
            layout.HeightPx = heightPx;
            form.Layout = layout;
            form.FormScale = scale;
            form.DesignSchemaVersion = design.SchemaVersion;
            form.DefaultWidthPx = baseline.WidthPx;
            form.DefaultWidthPercent = baseline.WidthPercent;
            form.DefaultHeightPx = baseline.HeightPx;
            requiredSectionHeight = Math.Max(
                requiredSectionHeight,
                Math.Clamp(
                    (int)Math.Ceiling(topPx + heightPx + FormBlockLayoutPolicy.SectionBottomPaddingPx),
                    120,
                    FormBlockLayoutPolicy.MaximumSectionHeightPx));
        }

        if (requiredSectionHeight > (section.Style?.CustomMinHeightPx ?? 0))
        {
            section.Style ??= new SectionStyle();
            section.Style.Height = "custom";
            section.Style.CustomMinHeightPx = requiredSectionHeight;
        }
    }

    public async Task<bool> DeleteAsync(string presetId)
    {
        var preset = await _context.SectionPresets.Find(item => item.Id == presetId).FirstOrDefaultAsync();
        if (preset is null) return false;
        if (_contracts.Compatibility(preset).IsCompatible)
            _contracts.UpgradeInPlace(preset);
        var result = await _context.SectionPresets.DeleteOneAsync(preset => preset.Id == presetId);
        if (result.DeletedCount <= 0) return false;

        await _assetCleanup.DeleteUnusedPageGraphAssetsAsync(
            Array.Empty<Page>(),
            preset.Section is null ? Array.Empty<Section>() : [preset.Section],
            preset.Blocks);
        await _assetCleanup.DeleteUnusedAsync([preset.ThumbnailUrl]);
        return true;
    }

    public SectionPresetCompatibility Compatibility(SectionPreset preset) =>
        _contracts.Compatibility(preset);

    private static Dictionary<string, string> NormalizeLocalized(Dictionary<string, string>? value) =>
        value is null
            ? new()
            : value
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key.Trim(), item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string? ResolveThumbnail(Section section, IReadOnlyList<Block> blocks)
    {
        if (!string.IsNullOrWhiteSpace(section.Style.BackgroundImageUrl))
            return section.Style.BackgroundImageUrl;

        var sectionImage = section switch
        {
            HeroSection hero => hero.ImageUrl,
            ListSection list => list.Items.FirstOrDefault(item => item.Visible && !string.IsNullOrWhiteSpace(item.ImageUrl))?.ImageUrl,
            CarouselSection carousel => carousel.Items.FirstOrDefault(item => item.Visible && !string.IsNullOrWhiteSpace(item.ImageUrl))?.ImageUrl,
            ShowcaseSection showcase => showcase.ItemOverrides.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.CardImageUrl))?.CardImageUrl,
            TestimonialSection testimonials => testimonials.Items.FirstOrDefault(item => item.Visible && !string.IsNullOrWhiteSpace(item.ImageUrl))?.ImageUrl,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(sectionImage)) return sectionImage;

        return blocks.OfType<ImageBlock>().Select(block => block.Asset.Url)
                   .Concat(blocks.OfType<CardBlock>().Select(block => block.Asset.Url))
                   .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
    }

    private static string ResolveThumbnailBackground(Section section)
    {
        if (string.Equals(section.Style.BackgroundType, "gradient", StringComparison.OrdinalIgnoreCase))
        {
            var from = SafeColor(section.Style.GradientFrom, "#eef2ff");
            var to = SafeColor(section.Style.GradientTo, "#e0e7ff");
            return $"linear-gradient(135deg, {from}, {to})";
        }
        return SafeColor(section.Style.BackgroundColor, "#f3f4f6");
    }

    private static Dictionary<string, string> ResolvePreviewText(Section section)
    {
        Dictionary<string, string>? value = section switch
        {
            HeroSection hero => hero.Heading,
            CtaSection cta => cta.Heading,
            ListSection list => list.SectionTitle,
            ShowcaseSection showcase => showcase.SectionTitle,
            LibrarySection library => library.SectionTitle,
            StatsSection stats => stats.SectionTitle,
            CarouselSection carousel => carousel.SectionTitle,
            NetworkMapSection map => map.SectionTitle,
            TestimonialSection testimonials => testimonials.SectionTitle,
            CanvasSection canvas => canvas.AdminLabel,
            _ => null
        };
        return value is null ? new() : new Dictionary<string, string>(value, StringComparer.OrdinalIgnoreCase);
    }

    private static string SafeColor(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{3,8}$")
            ? value
            : fallback;

    private async Task<string?> ValidateLinkedReferencesAsync(SectionPreset preset)
    {
        if (preset.Section is ShowcaseSection showcase && !string.IsNullOrWhiteSpace(showcase.SourcePageId))
        {
            var sourceExists = await _context.PagesDraft.Find(page =>
                    page.Id == showcase.SourcePageId || page.StableId == showcase.SourcePageId)
                .AnyAsync();
            if (!sourceExists)
                return "This preset's Showcase source Page no longer exists.";
        }

        if (preset.Section is LibrarySection library && library.ContentTypes.Count > 0)
        {
            var requested = library.ContentTypes
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existing = await _context.ContentTypes
                .Find(type => requested.Contains(type.Key))
                .Project(type => type.Key)
                .ToListAsync();
            var missing = requested.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
                return $"This preset references missing Content Types: {string.Join(", ", missing)}.";
        }

        var formIds = FormDefinitionIds(preset)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (formIds.Count == 0) return null;

        var existingFormIds = await _context.FormDefinitions
            .Find(definition => definition.Active && formIds.Contains(definition.Id))
            .Project(definition => definition.Id)
            .ToListAsync();
        var missingFormIds = formIds.Except(existingFormIds, StringComparer.OrdinalIgnoreCase).ToList();
        return missingFormIds.Count == 0
            ? null
            : "This preset references a Form Definition that is missing or inactive.";
    }

    private static IEnumerable<string?> FormDefinitionIds(SectionPreset preset)
    {
        if (preset.Section is HeroSection hero)
            foreach (var button in hero.Buttons) yield return button.FormDefinitionId;
        if (preset.Section is CtaSection cta)
        {
            yield return cta.Button?.FormDefinitionId;
            foreach (var button in cta.Buttons) yield return button.FormDefinitionId;
        }
        if (preset.Section is ShowcaseSection showcase)
            yield return showcase.ActionButton?.FormDefinitionId;

        foreach (var block in preset.Blocks)
        {
            foreach (var button in block.Buttons) yield return button.FormDefinitionId;
            switch (block)
            {
                case FormBlock form:
                    yield return form.FormDefinitionId;
                    break;
                case CardBlock card:
                    yield return card.FormDefinitionId;
                    break;
                case ButtonBlock button:
                    yield return button.FormDefinitionId;
                    break;
            }
        }
    }
}
