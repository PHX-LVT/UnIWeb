using System.Text.RegularExpressions;
using Contracts.Forms;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.FormServices;

public sealed class FormDefinitionService
{
    public const string InsightSubscriptionDefinitionId = "6a3507ee4a176a86ce0c2697";
    private static readonly Regex FormKeyRegex = new(
        "^[a-z][a-z0-9-]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<FormDefinition> _definitions;
    private readonly FormInputTypeService _inputTypes;
    private readonly FormDefinitionOrderService _order;
    private readonly FormDesignV2RuntimeSettings _v2Settings;

    public FormDefinitionService(
        IMongoDatabase database,
        FormInputTypeService inputTypes,
        FormDefinitionOrderService order,
        IOptions<FormDesignV2RuntimeSettings> v2Settings)
    {
        _database = database;
        _inputTypes = inputTypes;
        _order = order;
        _v2Settings = v2Settings.Value;
        _definitions = database.GetCollection<FormDefinition>("form_definitions");
    }

    public async Task<FormDefinition?> GetActiveByKeyAsync(string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null) return null;

        return await _definitions
            .Find(definition => definition.Key == normalizedKey && definition.Active)
            .FirstOrDefaultAsync();
    }

    public async Task<FormDefinition?> GetByKeyAsync(string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null) return null;

        return await _definitions
            .Find(definition => definition.Key == normalizedKey)
            .FirstOrDefaultAsync();
    }

    public async Task<List<FormDefinition>> GetAllAsync()
    {
        var definitions = await _definitions
            .Find(_ => true)
            .SortBy(definition => definition.Key)
            .ToListAsync();
        return await _order.ApplyReadOrderAsync(definitions);
    }

    public async Task<FormDefinition?> GetByIdAsync(string id) =>
        await _definitions.Find(definition => definition.Id == id).FirstOrDefaultAsync();

    public async Task<FormDefinition?> GetActiveByIdAsync(string id) =>
        await _definitions.Find(definition => definition.Id == id && definition.Active).FirstOrDefaultAsync();

    public async Task<List<FormDefinition>> GetActiveByIdsAsync(IEnumerable<string> ids)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedIds.Count == 0)
            return new List<FormDefinition>();

        return await _definitions
            .Find(definition => normalizedIds.Contains(definition.Id) && definition.Active)
            .ToListAsync();
    }

    public async Task<FormDefinition> UpsertAsync(FormDefinitionUpsertRequest request, string? id = null)
    {
        var key = NormalizeKey(request.Key) ?? throw new ArgumentException("Invalid form key.", nameof(request));
        var now = DateTime.UtcNow;
        FormDefinition? existing;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (await _definitions.Find(definition => definition.Key == key).Limit(1).AnyAsync())
                throw new InvalidOperationException("Form Key already exists.");

            existing = null;
        }
        else
        {
            existing = await GetByIdAsync(id);
        }

        if (existing?.Design?.SchemaVersion >= FormDesignV2Policy.TargetSchemaVersion && !_v2Settings.CanWriteV2)
            throw new InvalidOperationException("This schema-v2 Form Definition is read-only until v2 writes are activated.");

        var previousDesign = existing?.Design is null ? null : MapDesign(existing.Design);
        var rawDefinitions = _database.GetCollection<BsonDocument>("form_definitions");
        var previousDefinitionDocument = existing is null
            ? null
            : await rawDefinitions.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(existing.Id))).FirstOrDefaultAsync();

        if (existing is not null)
            EnsureRetainedFieldTypesUnchanged(existing.Fields, request.Fields);

        var definition = existing ?? new FormDefinition
        {
            Id = ObjectId.GenerateNewId().ToString(),
            CreatedAt = now
        };

        definition.Key = existing?.Key ?? key;
        definition.Name = CleanTextMap(request.Name);
        definition.Introduction = CleanTextMap(request.Introduction);
        definition.SubmitButtonLabel = CleanTextMap(request.SubmitButtonLabel);
        definition.Active = request.Active;
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
        definition.Fields = request.Fields
            .OrderBy(field => field.Order)
            .Select((field, index) => MapRequestField(field, index, capabilities))
            .ToList();
        var fieldDtos = definition.Fields.Select(MapFieldDto).ToList();
        var normalizedDesign = _v2Settings.CanWriteV2
            ? NormalizeV2WriteDesign(
                definition.Id,
                request.Design,
                fieldDtos,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel,
                request.InformationItems,
                request.AuxiliaryActions)
            : FormDesignPolicy.Normalize(
                request.Design,
                fieldDtos,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel);
        definition.Design = _v2Settings.CanWriteV2
            ? MapDesignV2Write(normalizedDesign)
            : MapDesign(normalizedDesign);
        if (_v2Settings.CanWriteV2)
        {
            definition.InformationItems = MapInformationItems(request.InformationItems);
            definition.AuxiliaryActions = MapAuxiliaryActions(request.AuxiliaryActions);
        }
        definition.UpdatedAt = now;

        try
        {
            await _definitions.ReplaceOneAsync(
                item => item.Id == definition.Id,
                definition,
                new ReplaceOptions { IsUpsert = true });

            if (existing is not null &&
                (previousDesign is null || !DesignsEqual(previousDesign, normalizedDesign)))
            {
                await PropagateDesignToFormBlocksAsync(definition.Id, normalizedDesign, now);
            }
        }
        catch
        {
            if (previousDefinitionDocument is not null)
            {
                await rawDefinitions.ReplaceOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", previousDefinitionDocument["_id"]),
                    previousDefinitionDocument);
            }
            throw;
        }

        return definition;
    }

    private async Task PropagateDesignToFormBlocksAsync(
        string formDefinitionId,
        FormDesignSettingsDto design,
        DateTime now)
    {
        var snapshots = new List<FormUsageSnapshot>();
        try
        {
            foreach (var source in new[]
                     {
                         new FormUsageCollections("blocks_draft", "sections_draft"),
                         new FormUsageCollections("blocks_published", "sections_published")
                     })
            {
            var blocksCollection = _database.GetCollection<BsonDocument>(source.Blocks);
            var sectionsCollection = _database.GetCollection<BsonDocument>(source.Sections);
            var filter = FormBlockReferenceFilter(formDefinitionId);
            var originals = await blocksCollection.Find(filter).ToListAsync();
            if (originals.Count == 0) continue;

            var sectionStableIds = originals
                .Select(block => ReadString(block, "SectionStableId"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var sectionFilter = Builders<BsonDocument>.Filter.In("StableId", sectionStableIds);
            var originalSections = sectionStableIds.Count == 0
                ? new List<BsonDocument>()
                : await sectionsCollection.Find(sectionFilter).ToListAsync();
            var sectionBlocksBefore = sectionStableIds.Count == 0
                ? new List<BsonDocument>()
                : await blocksCollection.Find(Builders<BsonDocument>.Filter.In("SectionStableId", sectionStableIds)).ToListAsync();
            snapshots.Add(new FormUsageSnapshot(source, originals, originalSections));
            var sectionsByStableId = originalSections
                .Where(section => !string.IsNullOrWhiteSpace(ReadString(section, "StableId")))
                .ToDictionary(
                    section => ReadString(section, "StableId")!,
                    section => (BsonDocument)section.DeepClone(),
                    StringComparer.Ordinal);
            var changedSections = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (var original in originals)
                {
                    var block = (BsonDocument)original.DeepClone();
                    var sectionStableId = ReadString(block, "SectionStableId");
                    sectionsByStableId.TryGetValue(sectionStableId ?? string.Empty, out var section);
                    var contentWidth = section is not null &&
                                       section.TryGetValue("Style", out var styleValue) &&
                                       styleValue.IsBsonDocument
                        ? ReadString(styleValue.AsBsonDocument, "ContentWidth")
                        : null;
                    var defaultSize = FormBlockLayoutPolicy.CalculateDefaultSize(
                        design,
                        FormBlockLayoutPolicy.AvailableContentWidthPx(contentWidth));
                    var layout = block.TryGetValue("Layout", out var layoutValue) && layoutValue.IsBsonDocument
                        ? layoutValue.AsBsonDocument
                        : new BsonDocument();
                    var oldDefaultWidth = ReadNumber(block, "DefaultWidthPercent", defaultSize.WidthPercent);
                    var oldDefaultHeight = ReadNumber(block, "DefaultHeightPx", defaultSize.HeightPx);
                    var currentWidth = ReadNumber(layout, "WidthPercent", oldDefaultWidth);
                    var currentHeight = ReadNumber(layout, "HeightPx", oldDefaultHeight);
                    var scale = block.TryGetValue("FormScale", out var scaleValue) && scaleValue.IsNumeric
                        ? scaleValue.ToDouble()
                        : Math.Min(
                            currentWidth / Math.Max(1d, oldDefaultWidth),
                            currentHeight / Math.Max(24d, oldDefaultHeight));
                    scale = Math.Clamp(scale, FormBlockLayoutPolicy.MinimumScale, FormBlockLayoutPolicy.MaximumScale);

                    var widthPercent = defaultSize.WidthPercent * scale;
                    var heightPx = defaultSize.HeightPx * scale;
                    var leftPercent = Math.Clamp(
                        ReadNumber(layout, "LeftPercent", ReadNumber(layout, "X", 0d) / 12d * 100d),
                        0d,
                        Math.Max(0d, 100d - widthPercent));
                    var topPx = Math.Clamp(
                        ReadNumber(layout, "TopPx", ReadNumber(layout, "Y", 0d) * 48d),
                        0d,
                        Math.Max(0d, FormBlockLayoutPolicy.MaximumSectionHeightPx - FormBlockLayoutPolicy.SectionBottomPaddingPx - heightPx));
                    var widthUnits = Math.Clamp((int)Math.Round(widthPercent / 100d * 12d), 1, 12);
                    var x = Math.Clamp((int)Math.Round(leftPercent / 100d * 12d), 0, Math.Max(0, 12 - widthUnits));
                    var y = Math.Clamp((int)Math.Round(topPx / 48d), 0, 60);
                    var heightUnits = Math.Clamp((int)Math.Ceiling(heightPx / 48d), 1, 40);
                    var blockNeedsUpdate =
                        !string.Equals(ReadString(layout, "Width"), "custom", StringComparison.Ordinal) ||
                        ReadInteger(layout, "ColumnSpan", 0) != widthUnits ||
                        ReadInteger(layout, "X", -1) != x ||
                        ReadInteger(layout, "Y", -1) != y ||
                        ReadInteger(layout, "W", 0) != widthUnits ||
                        ReadInteger(layout, "H", 0) != heightUnits ||
                        !NumbersEqual(ReadNumber(layout, "LeftPercent", double.NaN), leftPercent) ||
                        !NumbersEqual(ReadNumber(layout, "TopPx", double.NaN), topPx) ||
                        !NumbersEqual(ReadNumber(layout, "WidthPercent", double.NaN), widthPercent) ||
                        !NumbersEqual(ReadNumber(layout, "HeightPx", double.NaN), heightPx) ||
                        !NumbersEqual(ReadNumber(block, "FormScale", double.NaN), scale) ||
                        ReadInteger(block, "DesignSchemaVersion", 0) != design.SchemaVersion ||
                        ReadInteger(block, "DefaultWidthPx", 0) != defaultSize.WidthPx ||
                        !NumbersEqual(ReadNumber(block, "DefaultWidthPercent", double.NaN), defaultSize.WidthPercent) ||
                        !NumbersEqual(ReadNumber(block, "DefaultHeightPx", double.NaN), defaultSize.HeightPx) ||
                        block.Contains("Fields") ||
                        block.Contains("SubmitButtonLabel");

                    if (blockNeedsUpdate)
                    {
                        layout["Width"] = "custom";
                        layout["ColumnSpan"] = widthUnits;
                        layout["X"] = x;
                        layout["Y"] = y;
                        layout["W"] = widthUnits;
                        layout["H"] = heightUnits;
                        layout["LeftPercent"] = leftPercent;
                        layout["TopPx"] = topPx;
                        layout["WidthPercent"] = widthPercent;
                        layout["HeightPx"] = heightPx;
                        block["Layout"] = layout;
                        block["FormScale"] = scale;
                        block["DesignSchemaVersion"] = design.SchemaVersion;
                        block["DefaultWidthPx"] = defaultSize.WidthPx;
                        block["DefaultWidthPercent"] = defaultSize.WidthPercent;
                        block["DefaultHeightPx"] = defaultSize.HeightPx;
                        block.Remove("Fields");
                        block.Remove("SubmitButtonLabel");
                        block["UpdatedAt"] = now;
                        block["Version"] = ReadInteger(block, "Version", 1) + 1;

                        await blocksCollection.ReplaceOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", original["_id"]),
                            block);
                    }

                }

                var sectionBlocksAfter = sectionStableIds.Count == 0
                    ? new List<BsonDocument>()
                    : await blocksCollection.Find(Builders<BsonDocument>.Filter.In("SectionStableId", sectionStableIds)).ToListAsync();
                foreach (var (sectionStableId, section) in sectionsByStableId)
                {
                    var beforeHeight = RequiredFreeformSectionHeight(sectionBlocksBefore, sectionStableId);
                    var afterHeight = RequiredFreeformSectionHeight(sectionBlocksAfter, sectionStableId);
                    var style = section.TryGetValue("Style", out var sectionStyleValue) && sectionStyleValue.IsBsonDocument
                        ? sectionStyleValue.AsBsonDocument
                        : new BsonDocument();
                    var currentHeight = ReadInteger(style, "CustomMinHeightPx", 0);
                    var managedHeight = ReadInteger(style, "FormManagedMinHeightPx", 0);
                    var canShrink = managedHeight > 0 || currentHeight <= beforeHeight + 1;
                    var targetHeight = afterHeight > currentHeight || canShrink ? afterHeight : currentHeight;
                    if (!canShrink && targetHeight == currentHeight) continue;
                    if (targetHeight == currentHeight && managedHeight == afterHeight) continue;

                    style["Height"] = "custom";
                    style["CustomMinHeightPx"] = targetHeight;
                    style["FormManagedMinHeightPx"] = afterHeight;
                    section["Style"] = style;
                    section["UpdatedAt"] = now;
                    section["Version"] = ReadInteger(section, "Version", 1) + 1;
                    changedSections.Add(sectionStableId);
                }

                foreach (var sectionStableId in changedSections)
                {
                    var section = sectionsByStableId[sectionStableId];
                    await sectionsCollection.ReplaceOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", section["_id"]),
                        section);
                }
            }
            catch
            {
                throw;
            }
        }
        }
        catch
        {
            foreach (var snapshot in snapshots)
            {
                var snapshotBlocks = _database.GetCollection<BsonDocument>(snapshot.Source.Blocks);
                var snapshotSections = _database.GetCollection<BsonDocument>(snapshot.Source.Sections);
                foreach (var original in snapshot.Blocks)
                {
                    await snapshotBlocks.ReplaceOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", original["_id"]),
                        original);
                }
                foreach (var originalSection in snapshot.Sections)
                {
                    await snapshotSections.ReplaceOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", originalSection["_id"]),
                        originalSection);
                }
            }
            throw;
        }
    }

    private static int RequiredFreeformSectionHeight(IEnumerable<BsonDocument> blocks, string sectionStableId)
    {
        var maximumBottom = blocks
            .Where(block => string.Equals(ReadString(block, "SectionStableId"), sectionStableId, StringComparison.Ordinal) &&
                            string.Equals(ReadString(block, "PositionMode"), "freeform", StringComparison.OrdinalIgnoreCase))
            .Select(block => block.TryGetValue("Layout", out var layoutValue) && layoutValue.IsBsonDocument
                ? layoutValue.AsBsonDocument
                : new BsonDocument())
            .Select(layout => ReadNumber(layout, "TopPx", ReadNumber(layout, "Y", 0d) * 48d) +
                              ReadNumber(layout, "HeightPx", Math.Max(1d, ReadNumber(layout, "H", 1d)) * 48d))
            .DefaultIfEmpty(80d)
            .Max();
        return Math.Clamp(
            (int)Math.Ceiling(maximumBottom + FormBlockLayoutPolicy.SectionBottomPaddingPx),
            120,
            FormBlockLayoutPolicy.MaximumSectionHeightPx);
    }

    public async Task ReflowThemeInheritedFormsAsync(string? spacingScale)
    {
        var scale = ParseThemeSpacingScale(spacingScale);
        var definitions = await _definitions
            .Find(definition => definition.Design != null && definition.Design.UseThemeDefaults == true)
            .ToListAsync();
        var rawDefinitions = _database.GetCollection<BsonDocument>("form_definitions");

        foreach (var definition in definitions)
        {
            var previous = MapDesign(definition.Design);
            var requested = MapDesign(definition.Design);
            requested.PaddingPx = Math.Clamp(
                (int)Math.Round(32d * scale),
                FormDesignPolicy.MinimumPaddingPx,
                FormDesignPolicy.MaximumPaddingPx);
            requested.FieldGapPx = Math.Clamp(
                (int)Math.Round(16d * scale),
                FormDesignV2Policy.MinimumFieldGapPx,
                FormDesignV2Policy.MaximumFieldGapPx);
            var fields = definition.Fields.Select(MapFieldDto).ToList();
            var normalized = requested.V2 is not null
                ? NormalizeV2WriteDesign(
                    definition.Id,
                    requested,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel,
                    MapInformationItems(definition.InformationItems),
                    MapAuxiliaryActions(definition.AuxiliaryActions))
                : FormDesignPolicy.Normalize(
                    requested,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel);
            if (DesignsEqual(previous, normalized)) continue;

            var rawId = ObjectId.TryParse(definition.Id, out var objectId)
                ? (BsonValue)new BsonObjectId(objectId)
                : new BsonString(definition.Id);
            var original = await rawDefinitions
                .Find(Builders<BsonDocument>.Filter.Eq("_id", rawId))
                .FirstOrDefaultAsync();
            definition.Design = normalized.V2 is not null ? MapDesignV2Write(normalized) : MapDesign(normalized);
            definition.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _definitions.ReplaceOneAsync(item => item.Id == definition.Id, definition);
                await PropagateDesignToFormBlocksAsync(definition.Id, normalized, definition.UpdatedAt);
            }
            catch
            {
                if (original is not null)
                    await rawDefinitions.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", original["_id"]), original);
                throw;
            }
        }
    }

    private static double ParseThemeSpacingScale(string? value)
    {
        value = value?.Trim().ToLowerInvariant();
        if (value == "compact") return .85d;
        if (value == "spacious") return 1.2d;
        return double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, .5d, 2d)
            : 1d;
    }

    private static void EnsureRetainedFieldTypesUnchanged(
        IEnumerable<FormDefinitionField> existingFields,
        IEnumerable<FormFieldDefinitionDto> requestedFields)
    {
        var requestedByKey = requestedFields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .GroupBy(field => field.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var existingField in existingFields)
        {
            if (!requestedByKey.TryGetValue(existingField.Key, out var requestedField))
                continue;

            if (string.Equals(
                    FormInputTypeCatalog.NormalizeType(existingField.Type),
                    FormInputTypeCatalog.NormalizeType(requestedField.Type),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            throw new InvalidOperationException(
                $"Form-Field Type for saved Field Key \"{existingField.Key}\" cannot be changed. Delete the field and create a new one.");
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _definitions.DeleteOneAsync(definition => definition.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> HasSubmissionsAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        return await _database.GetCollection<FormSubmission>("form_submissions")
            .Find(submission => submission.FormId == id)
            .Limit(1)
            .AnyAsync();
    }

    public async Task<long> CountSubmissionsAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;

        return await _database.GetCollection<FormSubmission>("form_submissions")
            .CountDocumentsAsync(submission => submission.FormId == id);
    }

    public async Task<bool> IsReferencedAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        if (await HasReferenceAsync("global_buttons", Builders<BsonDocument>.Filter.Eq("FormDefinitionId", id))) return true;

        var sectionFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("Button.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("ActionButton.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Buttons.FormDefinitionId", id));
        if (await HasReferenceAsync("sections_draft", sectionFilter)) return true;
        if (await HasReferenceAsync("sections_published", sectionFilter)) return true;

        var blockFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Buttons.FormDefinitionId", id));
        if (await HasReferenceAsync("blocks_draft", blockFilter)) return true;
        if (await HasReferenceAsync("blocks_published", blockFilter)) return true;

        var presetFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("Section.Button.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Section.ActionButton.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Section.Buttons.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Blocks.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Blocks.Buttons.FormDefinitionId", id));
        if (await HasReferenceAsync("canvas_section_presets", presetFilter)) return true;

        return false;
    }


    public async Task<FormDefinitionUsageResponse> GetUsageAsync(string id)
    {
        var items = new List<FormDefinitionUsageItemDto>();

        await AddGlobalButtonUsageAsync(id, items);

        var draftPages = await LoadPageLookupAsync("pages_draft");
        var publishedPages = await LoadPageLookupAsync("pages_published");
        var draftSections = await LoadSectionLookupAsync("sections_draft", draftPages);
        var publishedSections = await LoadSectionLookupAsync("sections_published", publishedPages);

        await AddSectionUsageAsync("sections_draft", "Draft", id, draftPages, items);
        await AddSectionUsageAsync("sections_published", "Published", id, publishedPages, items);
        await AddBlockUsageAsync("blocks_draft", "Draft", id, draftPages, draftSections, items);
        await AddBlockUsageAsync("blocks_published", "Published", id, publishedPages, publishedSections, items);
        await AddPresetUsageAsync(id, items);

        var ordered = items
            .OrderBy(item => item.Area == "FormBlock" ? 0 : item.Area == "Block" ? 1 : item.Area == "Section" ? 2 : 3)
            .ThenBy(item => item.Source)
            .ThenBy(item => item.Location)
            .ToList();

        return new FormDefinitionUsageResponse
        {
            FormDefinitionId = id,
            TotalCount = ordered.Count,
            SubmissionCount = await CountSubmissionsAsync(id),
            Items = ordered
        };
    }

    private async Task AddGlobalButtonUsageAsync(string id, List<FormDefinitionUsageItemDto> items)
    {
        var buttons = await _database.GetCollection<BsonDocument>("global_buttons")
            .Find(Builders<BsonDocument>.Filter.Eq("FormDefinitionId", id))
            .ToListAsync();

        foreach (var button in buttons)
        {
            var label = ReadText(button, "LabelText", "Label") ?? "Global Button";
            items.Add(new FormDefinitionUsageItemDto
            {
                Area = "Global Button",
                Source = "Global",
                ElementLabel = label,
                Location = $"Global Button > {label}"
            });
        }
    }

    private async Task AddPresetUsageAsync(string id, List<FormDefinitionUsageItemDto> items)
    {
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("Section.Button.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Section.ActionButton.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Section.Buttons.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Blocks.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Blocks.Buttons.FormDefinitionId", id));
        var presets = await _database.GetCollection<BsonDocument>("canvas_section_presets")
            .Find(filter)
            .ToListAsync();

        foreach (var preset in presets)
        {
            var name = ReadText(preset, "Name") ?? "Untitled preset";
            items.Add(new FormDefinitionUsageItemDto
            {
                Area = "Section Preset",
                Source = "Preset",
                SectionType = ReadString(preset, "SectionType") ?? "Section",
                SectionTitle = name,
                ElementLabel = "Form reference",
                Location = $"Saved Presets > {name}"
            });
        }
    }

    private async Task AddSectionUsageAsync(
        string collectionName,
        string source,
        string id,
        IReadOnlyDictionary<string, PageUsageInfo> pages,
        List<FormDefinitionUsageItemDto> items)
    {
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("Button.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("ActionButton.FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Buttons.FormDefinitionId", id));
        var sections = await _database.GetCollection<BsonDocument>(collectionName).Find(filter).ToListAsync();

        foreach (var section in sections)
        {
            var page = ResolvePage(pages, ReadString(section, "PageStableId"));
            var sectionType = ReadType(section, "Section");
            var sectionTitle = ResolveSectionTitle(section, sectionType);

            if (TryGetDocument(section, "Button", out var button) && FieldMatches(button, "FormDefinitionId", id))
                AddUsage(items, "Section", source, page, sectionType, sectionTitle, ReadText(button, "Label") ?? "Main Button");

            if (TryGetDocument(section, "ActionButton", out var actionButton) && FieldMatches(actionButton, "FormDefinitionId", id))
                AddUsage(items, "Section", source, page, sectionType, sectionTitle, ReadText(actionButton, "Label") ?? "Action Button");

            foreach (var childButton in ReadDocumentArray(section, "Buttons").Where(buttonDoc => FieldMatches(buttonDoc, "FormDefinitionId", id)))
                AddUsage(items, "Section", source, page, sectionType, sectionTitle, ReadText(childButton, "Label") ?? "Button");
        }
    }

    private async Task AddBlockUsageAsync(
        string collectionName,
        string source,
        string id,
        IReadOnlyDictionary<string, PageUsageInfo> pages,
        IReadOnlyDictionary<string, SectionUsageInfo> sections,
        List<FormDefinitionUsageItemDto> items)
    {
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("FormDefinitionId", id),
            Builders<BsonDocument>.Filter.Eq("Buttons.FormDefinitionId", id));
        var blocks = await _database.GetCollection<BsonDocument>(collectionName).Find(filter).ToListAsync();

        foreach (var block in blocks)
        {
            var page = ResolvePage(pages, ReadString(block, "PageStableId"));
            var section = ResolveSection(sections, ReadString(block, "SectionStableId"));
            var blockType = ReadType(block, "Block");
            var blockLabel = ReadText(block, "Label", "ButtonLabel", "Title", "Filename") ?? $"{blockType} Block";

            if (FieldMatches(block, "FormDefinitionId", id))
                AddUsage(items, string.Equals(blockType, "Form", StringComparison.OrdinalIgnoreCase) ? "FormBlock" : "Block", source, page, section.Type, section.Title, blockLabel);

            foreach (var childButton in ReadDocumentArray(block, "Buttons").Where(buttonDoc => FieldMatches(buttonDoc, "FormDefinitionId", id)))
                AddUsage(items, "Block", source, page, section.Type, section.Title, ReadText(childButton, "Label") ?? blockLabel);
        }
    }

    private async Task<Dictionary<string, PageUsageInfo>> LoadPageLookupAsync(string collectionName)
    {
        var pages = await _database.GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync();

        return pages
            .Select(page => new { StableId = ReadString(page, "StableId"), Page = page })
            .Where(item => !string.IsNullOrWhiteSpace(item.StableId))
            .GroupBy(item => item.StableId!)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var page = group.First().Page;
                    var slug = ReadString(page, "FullSlug") ?? ReadString(page, "Slug") ?? string.Empty;
                    return new PageUsageInfo(
                        ReadText(page, "Name") ?? (string.IsNullOrWhiteSpace(slug) ? "Untitled Page" : slug),
                        slug);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, SectionUsageInfo>> LoadSectionLookupAsync(string collectionName, IReadOnlyDictionary<string, PageUsageInfo> pages)
    {
        var sections = await _database.GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync();

        return sections
            .Select(section => new { StableId = ReadString(section, "StableId"), Section = section })
            .Where(item => !string.IsNullOrWhiteSpace(item.StableId))
            .GroupBy(item => item.StableId!)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var section = group.First().Section;
                    var type = ReadType(section, "Section");
                    return new SectionUsageInfo(type, ResolveSectionTitle(section, type));
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static void AddUsage(
        List<FormDefinitionUsageItemDto> items,
        string area,
        string source,
        PageUsageInfo page,
        string sectionType,
        string sectionTitle,
        string elementLabel)
    {
        var locationParts = new[] { page.Name, sectionTitle, elementLabel }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        items.Add(new FormDefinitionUsageItemDto
        {
            Area = area,
            Source = source,
            PageName = page.Name,
            PageSlug = page.Slug,
            SectionType = sectionType,
            SectionTitle = sectionTitle,
            ElementLabel = elementLabel,
            Location = string.Join(" > ", locationParts)
        });
    }

    private static PageUsageInfo ResolvePage(IReadOnlyDictionary<string, PageUsageInfo> pages, string? stableId) =>
        !string.IsNullOrWhiteSpace(stableId) && pages.TryGetValue(stableId, out var page)
            ? page
            : new PageUsageInfo("Unknown Page", string.Empty);

    private static SectionUsageInfo ResolveSection(IReadOnlyDictionary<string, SectionUsageInfo> sections, string? stableId) =>
        !string.IsNullOrWhiteSpace(stableId) && sections.TryGetValue(stableId, out var section)
            ? section
            : new SectionUsageInfo("Section", "Unknown Section");

    private static string ResolveSectionTitle(BsonDocument section, string sectionType) =>
        ReadText(section, "Heading", "Title", "Eyebrow", "Subheading", "Subtext") ?? $"{sectionType} Section";

    private static bool FieldMatches(BsonDocument document, string fieldName, string expected) =>
        document.TryGetValue(fieldName, out var value) && value.IsString && string.Equals(value.AsString, expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetDocument(BsonDocument parent, string fieldName, out BsonDocument document)
    {
        if (parent.TryGetValue(fieldName, out var value) && value.IsBsonDocument)
        {
            document = value.AsBsonDocument;
            return true;
        }

        document = new BsonDocument();
        return false;
    }

    private static IEnumerable<BsonDocument> ReadDocumentArray(BsonDocument parent, string fieldName) =>
        parent.TryGetValue(fieldName, out var value) && value.IsBsonArray
            ? value.AsBsonArray.Where(item => item.IsBsonDocument).Select(item => item.AsBsonDocument)
            : Enumerable.Empty<BsonDocument>();

    private static string? ReadString(BsonDocument document, string fieldName) =>
        document.TryGetValue(fieldName, out var value) && value.IsString ? value.AsString : null;

    private static double ReadNumber(BsonDocument document, string fieldName, double fallback) =>
        document.TryGetValue(fieldName, out var value) && value.IsNumeric ? value.ToDouble() : fallback;

    private static bool NumbersEqual(double left, double right) =>
        !double.IsNaN(left) && !double.IsNaN(right) && Math.Abs(left - right) <= 0.001d;

    private static int ReadInteger(BsonDocument document, string fieldName, int fallback) =>
        document.TryGetValue(fieldName, out var value) && value.IsNumeric ? value.ToInt32() : fallback;

    private static string? ReadText(BsonDocument document, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!document.TryGetValue(fieldName, out var value)) continue;
            var text = ReadTextValue(value);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }

    private static string? ReadTextValue(BsonValue value)
    {
        if (value.IsString) return value.AsString;
        if (!value.IsBsonDocument) return null;

        var document = value.AsBsonDocument;
        if (document.TryGetValue("en", out var english) && english.IsString && !string.IsNullOrWhiteSpace(english.AsString))
            return english.AsString;

        return document.Values.FirstOrDefault(item => item.IsString && !string.IsNullOrWhiteSpace(item.AsString))?.AsString;
    }

    private static string ReadType(BsonDocument document, string fallback)
    {
        if (!document.TryGetValue("_t", out var discriminator)) return fallback;

        if (discriminator.IsString) return CleanType(discriminator.AsString);
        if (discriminator.IsBsonArray)
        {
            var last = discriminator.AsBsonArray.LastOrDefault(item => item.IsString);
            if (last is not null) return CleanType(last.AsString);
        }

        return fallback;
    }

    private static string CleanType(string value)
    {
        var cleaned = value.Replace("Section", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Block", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? value : cleaned;
    }

    private sealed record PageUsageInfo(string Name, string Slug);
    private sealed record SectionUsageInfo(string Type, string Title);
    private sealed record FormUsageCollections(string Blocks, string Sections);
    private sealed record FormUsageSnapshot(
        FormUsageCollections Source,
        List<BsonDocument> Blocks,
        List<BsonDocument> Sections);

    private async Task<bool> HasReferenceAsync(string collectionName, FilterDefinition<BsonDocument> filter) =>
        await _database.GetCollection<BsonDocument>(collectionName).Find(filter).Limit(1).AnyAsync();

    private static FilterDefinition<BsonDocument> FormBlockReferenceFilter(string formDefinitionId) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("FormDefinitionId", formDefinitionId),
            Builders<BsonDocument>.Filter.Eq("_t", "form"));

    public async Task EnsureDefaultDefinitionsAsync()
    {
        foreach (var definition in DefaultDefinitions())
        {
            var exists = await _definitions.Find(item => item.Key == definition.Key).Limit(1).AnyAsync();
            if (!exists)
            {
                await _definitions.InsertOneAsync(definition);
            }
        }

        await EnsureDesignMigrationAsync();
        await EnsureThemeInheritanceMigrationAsync();
        await EnsureFormBlockLayoutMigrationAsync();
        await EnsureInsightSubscriptionMigrationAsync();
    }

    private async Task EnsureThemeInheritanceMigrationAsync()
    {
        var missingMarker = Builders<FormDefinition>.Filter.Exists("Design.UseThemeDefaults", false);
        var definitions = await _definitions.Find(missingMarker).ToListAsync();
        foreach (var definition in definitions.Where(item => item.Design is not null))
        {
            var inheritTheme = IsUntouchedLegacyAppearance(definition.Design!);
            await _definitions.UpdateOneAsync(
                item => item.Id == definition.Id,
                Builders<FormDefinition>.Update.Set("Design.UseThemeDefaults", inheritTheme));
        }
    }

    private async Task EnsureFormBlockLayoutMigrationAsync()
    {
        var definitions = await _definitions
            .Find(definition => definition.Active)
            .ToListAsync();
        foreach (var definition in definitions)
        {
            if (!await HasOutdatedFormBlockAsync(definition.Id, definition.Design?.SchemaVersion ?? FormDesignPolicy.CurrentSchemaVersion))
                continue;

            var source = MapDesign(definition.Design);
            var fields = definition.Fields.Select(MapFieldDto).ToList();
            var normalized = source.V2 is not null
                ? NormalizeV2WriteDesign(
                    definition.Id,
                    source,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel,
                    MapInformationItems(definition.InformationItems),
                    MapAuxiliaryActions(definition.AuxiliaryActions))
                : FormDesignPolicy.Normalize(
                    source,
                    fields,
                    definition.Name,
                    definition.Introduction,
                    definition.SubmitButtonLabel);
            await PropagateDesignToFormBlocksAsync(definition.Id, normalized, DateTime.UtcNow);
        }
    }

    private async Task<bool> HasOutdatedFormBlockAsync(string formDefinitionId, int targetSchemaVersion)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            FormBlockReferenceFilter(formDefinitionId),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Exists("DesignSchemaVersion", false),
                Builders<BsonDocument>.Filter.Lt("DesignSchemaVersion", targetSchemaVersion),
                Builders<BsonDocument>.Filter.Exists("Fields", true),
                Builders<BsonDocument>.Filter.Exists("SubmitButtonLabel", true)));
        foreach (var collectionName in new[] { "blocks_draft", "blocks_published" })
        {
            if (await _database.GetCollection<BsonDocument>(collectionName).Find(filter).Limit(1).AnyAsync())
                return true;
        }
        return false;
    }

    private async Task EnsureInsightSubscriptionMigrationAsync()
    {
        var definition = await _definitions
            .Find(item => item.Key == "insight-subscription")
            .FirstOrDefaultAsync();
        if (definition is null) return;

        var sourceDesign = MapDesign(definition.Design);
        var fields = definition.Fields.Select(MapFieldDto).ToList();
        var design = sourceDesign.V2 is not null
            ? NormalizeV2WriteDesign(
                definition.Id,
                sourceDesign,
                fields,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel,
                MapInformationItems(definition.InformationItems),
                MapAuxiliaryActions(definition.AuxiliaryActions))
            : FormDesignPolicy.Normalize(
                sourceDesign,
                fields,
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel);

        foreach (var source in new[]
                 {
                     new FormUsageCollections("blocks_draft", "sections_draft"),
                     new FormUsageCollections("blocks_published", "sections_published")
                 })
        {
            var sections = _database.GetCollection<BsonDocument>(source.Sections);
            var blocks = _database.GetCollection<BsonDocument>(source.Blocks);
            var section = await sections
                .Find(Builders<BsonDocument>.Filter.Eq("StableId", "insight-stay-ahead-subscribe"))
                .FirstOrDefaultAsync();
            if (section is null) continue;

            var blockFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("StableId", "insight-subscribe-form-block"),
                Builders<BsonDocument>.Filter.Eq("SectionStableId", "insight-stay-ahead-subscribe"));
            var block = await blocks.Find(blockFilter).FirstOrDefaultAsync();
            if (block is null) continue;

            var style = section.TryGetValue("Style", out var styleValue) && styleValue.IsBsonDocument
                ? styleValue.AsBsonDocument
                : new BsonDocument();
            var defaultSize = FormBlockLayoutPolicy.CalculateDefaultSize(
                design,
                FormBlockLayoutPolicy.AvailableContentWidthPx(ReadString(style, "ContentWidth")));
            var sectionHeight = Math.Clamp(
                defaultSize.HeightPx + FormBlockLayoutPolicy.SectionBottomPaddingPx,
                120,
                FormBlockLayoutPolicy.MaximumSectionHeightPx);

            var sectionNeedsMigration = ReadType(section, string.Empty) != "canvas" ||
                                        section.Contains("Content") ||
                                        !string.Equals(ReadString(style, "BlockLayoutMode"), "freeform", StringComparison.Ordinal) ||
                                        ReadInteger(style, "CustomMinHeightPx", 0) != sectionHeight;
            if (sectionNeedsMigration)
            {
                await sections.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", section["_id"]),
                    Builders<BsonDocument>.Update
                        .Set("_t", new BsonArray { "Section", "canvas" })
                        .Set("Style.BlockLayoutMode", "freeform")
                        .Set("Style.Height", "custom")
                        .Set("Style.CustomMinHeightPx", sectionHeight)
                        .Set("Style.Padding", "none")
                        .Set("UpdatedAt", DateTime.UtcNow)
                        .Inc("Version", 1)
                        .Unset("Content"));
            }

            var layout = block.TryGetValue("Layout", out var layoutValue) && layoutValue.IsBsonDocument
                ? layoutValue.AsBsonDocument
                : new BsonDocument();
            layout["Width"] = "custom";
            layout["ColumnSpan"] = defaultSize.WidthUnits;
            layout["X"] = 0;
            layout["Y"] = 0;
            layout["W"] = defaultSize.WidthUnits;
            layout["H"] = Math.Clamp((int)Math.Ceiling(defaultSize.HeightPx / 48d), 1, 40);
            layout["LeftPercent"] = 0d;
            layout["TopPx"] = 0d;
            layout["WidthPercent"] = defaultSize.WidthPercent;
            layout["HeightPx"] = defaultSize.HeightPx;

            var blockNeedsMigration = !string.Equals(ReadString(block, "FormDefinitionId"), definition.Id, StringComparison.Ordinal) ||
                                      ReadInteger(block, "DesignSchemaVersion", 0) != design.SchemaVersion ||
                                      block.Contains("Fields") ||
                                      block.Contains("SubmitButtonLabel") ||
                                      Math.Abs(ReadNumber(block, "DefaultWidthPercent", 0d) - defaultSize.WidthPercent) > 0.001d ||
                                      Math.Abs(ReadNumber(block, "DefaultHeightPx", 0d) - defaultSize.HeightPx) > 0.001d;
            if (blockNeedsMigration)
            {
                await blocks.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", block["_id"]),
                    Builders<BsonDocument>.Update
                        .Set("FormDefinitionId", definition.Id)
                        .Set("DesignSchemaVersion", design.SchemaVersion)
                        .Set("FormScale", 1d)
                        .Set("DefaultWidthPx", defaultSize.WidthPx)
                        .Set("DefaultWidthPercent", defaultSize.WidthPercent)
                        .Set("DefaultHeightPx", defaultSize.HeightPx)
                        .Set("PositionMode", "freeform")
                        .Set("BlockZone", "canvas")
                        .Set("Layout", layout)
                        .Set("UpdatedAt", DateTime.UtcNow)
                        .Inc("Version", 1)
                        .Unset("Fields")
                        .Unset("SubmitButtonLabel"));
            }
        }
    }

    public async Task<int> EnsureDesignMigrationAsync()
    {
        var definitions = await _definitions.Find(_ => true).ToListAsync();
        var migrated = 0;
        foreach (var definition in definitions)
        {
            if (definition.Design?.SchemaVersion >= FormDesignV2Policy.TargetSchemaVersion && definition.Design.V2 is not null)
                continue;

            var source = definition.Design?.SchemaVersion > 0
                ? MapDesign(definition.Design)
                : FormDesignPolicy.CreateDefault(
                    definition.Layout == LegacyFormLayout.TwoColumns
                        ? FormDesignShape.TwoColumns
                        : FormDesignShape.Stacked);
            var normalized = FormDesignPolicy.Normalize(
                source,
                definition.Fields.Select(MapFieldDto),
                definition.Name,
                definition.Introduction,
                definition.SubmitButtonLabel);
            var current = definition.Design is null ? null : MapDesign(definition.Design);
            if (current is not null && DesignsEqual(current, normalized))
                continue;

            definition.Design = MapDesign(normalized);
            definition.UpdatedAt = definition.UpdatedAt == default ? DateTime.UtcNow : definition.UpdatedAt;
            await _definitions.ReplaceOneAsync(item => item.Id == definition.Id, definition);
            migrated++;
        }
        return migrated;
    }

    public static string? NormalizeKey(string? key)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(normalized) && FormKeyRegex.IsMatch(normalized)
            ? normalized
            : null;
    }

    public async Task<FormDefinitionResponse> MapPublicAsync(FormDefinition definition)
    {
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
        var resourceUrls = await LoadManagedResourceUrlsAsync(new[] { definition });
        return MapPublic(definition, capabilities, resourceUrls, _v2Settings.CanReadV2);
    }

    public async Task<List<FormDefinitionResponse>> MapPublicAsync(IEnumerable<FormDefinition> definitions)
    {
        var source = definitions.ToList();
        var capabilities = await _inputTypes.GetCapabilityLookupAsync();
        var resourceUrls = await LoadManagedResourceUrlsAsync(source);
        return source.Select(definition => MapPublic(definition, capabilities, resourceUrls, _v2Settings.CanReadV2)).ToList();
    }

    public static FormDefinitionResponse MapPublic(
        FormDefinition definition,
        IReadOnlyDictionary<string, FormInputTypeCapability>? capabilities = null,
        IReadOnlyDictionary<string, string>? managedResourceUrls = null,
        bool readStoredV2 = true)
    {
        var fields = definition.Fields
            .OrderBy(field => field.Order)
            .Select(field => MapPublicFieldDto(field, capabilities))
            .ToList();
        return new FormDefinitionResponse
        {
            Id = definition.Id,
            Key = definition.Key,
            Name = new(definition.Name),
            Introduction = new(definition.Introduction),
            SubmitButtonLabel = new(definition.SubmitButtonLabel),
            InformationItems = readStoredV2
                ? MapInformationItems(definition.InformationItems, managedResourceUrls)
                : new List<FormInformationItemDto>(),
            AuxiliaryActions = readStoredV2
                ? MapAuxiliaryActions(definition.AuxiliaryActions, managedResourceUrls)
                : new List<FormAuxiliaryActionDto>(),
            Design = MapDesign(definition.Design, includeV2Projection: true, definition.Id, fields, readStoredV2),
            Active = definition.Active,
            Fields = fields,
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt
        };
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadManagedResourceUrlsAsync(
        IEnumerable<FormDefinition> definitions)
    {
        var resourceIds = definitions
            .SelectMany(definition => definition.AuxiliaryActions ?? new())
            .Where(action => action.Target?.Type == FormActionTargetType.ManagedResource)
            .Select(action => action.Target.ResourceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (resourceIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var resources = await _database.GetCollection<ManagedResource>("managed_resources")
            .Find(resource => resourceIds.Contains(resource.Id) && resource.Active)
            .ToListAsync();
        return resources
            .Where(resource => !string.IsNullOrWhiteSpace(resource.Url))
            .ToDictionary(resource => resource.Id, resource => resource.Url, StringComparer.Ordinal);
    }

    private static FormInputTypeCapability Capability(
        string? type,
        IReadOnlyDictionary<string, FormInputTypeCapability>? capabilities) =>
        FormInputTypeService.Capability(type, capabilities);

    private static Dictionary<string, string> CleanTextMap(Dictionary<string, string>? values) =>
        values is null
            ? new()
            : values
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key.Trim().ToLowerInvariant(),
                    pair => pair.Value?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

    private static string CleanFieldKey(string? key, int index)
    {
        var candidate = key?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(candidate)
            ? $"field{index + 1}"
            : candidate;
    }

    private static IEnumerable<FormDefinition> DefaultDefinitions()
    {
        var now = DateTime.UtcNow;
        yield return new FormDefinition
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Key = "quote",
            Name = new() { ["en"] = "Get a Quote", ["vi"] = "Nhận báo giá" },
            Introduction = new()
            {
                ["en"] = "Fill out the form below and our sales team will contact you shortly.",
                ["vi"] = "Điền thông tin bên dưới và đội ngũ tư vấn sẽ liên hệ với bạn sớm."
            },
            SubmitButtonLabel = new() { ["en"] = "Submit Request", ["vi"] = "Gửi yêu cầu" },
            DisplayMode = LegacyFormDisplayMode.Modal,
            Design = MapDesign(FormDesignPolicy.Normalize(
                new FormDesignSettingsDto
                {
                    UseThemeDefaults = true,
                    Shape = FormDesignShape.TwoColumns,
                    WidthPx = FormDesignPolicy.TwoColumnDefaultWidthPx,
                    LabelMode = FormLabelMode.Visible
                },
                new[]
                {
                    new FormFieldDefinitionDto { Type = "select" },
                    new FormFieldDefinitionDto { Type = "text" },
                    new FormFieldDefinitionDto { Type = "email" },
                    new FormFieldDefinitionDto { Type = "tel" }
                })),
            Active = true,
            CreatedAt = now,
            UpdatedAt = now,
            Fields =
            [
                Field("ServiceType", "select", "Select Service", true, 0, 100, 0,
                [
                    Option("Logistics", "Logistics", 0),
                    Option("Warehouse", "Warehouse", 1),
                    Option("Transport", "Transport", 2)
                ]),
                Field("Route", "text", "Route / Volume / Duration", true, 0, 300, 1),
                Field("Email", "email", "Email Address", true, 0, 254, 2),
                Field("Phone", "tel", "Phone Number", true, 0, 40, 3)
            ]
        };

        yield return new FormDefinition
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Key = "expert",
            Name = new() { ["en"] = "Talk to an Expert", ["vi"] = "Trao đổi với chuyên gia" },
            Introduction = new()
            {
                ["en"] = "Our specialists are here to answer your questions and help you find the best solution.",
                ["vi"] = "Chuyên gia của chúng tôi sẽ hỗ trợ câu hỏi và đề xuất giải pháp phù hợp."
            },
            SubmitButtonLabel = new() { ["en"] = "Submit", ["vi"] = "Gửi" },
            DisplayMode = LegacyFormDisplayMode.Modal,
            Design = MapDesign(FormDesignPolicy.Normalize(
                new FormDesignSettingsDto
                {
                    UseThemeDefaults = true,
                    Shape = FormDesignShape.TwoColumns,
                    WidthPx = FormDesignPolicy.TwoColumnDefaultWidthPx,
                    LabelMode = FormLabelMode.Visible
                },
                new[]
                {
                    new FormFieldDefinitionDto { Type = "text" },
                    new FormFieldDefinitionDto { Type = "email" },
                    new FormFieldDefinitionDto { Type = "tel" },
                    new FormFieldDefinitionDto { Type = "text" },
                    new FormFieldDefinitionDto { Type = "select" },
                    new FormFieldDefinitionDto { Type = "textarea", InputBoxSize = 2 }
                })),
            Active = true,
            CreatedAt = now,
            UpdatedAt = now,
            Fields =
            [
                Field("Name", "text", "Full Name", true, 0, 150, 0),
                Field("Email", "email", "Email Address", true, 0, 254, 1),
                Field("Phone", "tel", "Phone Number", true, 0, 40, 2),
                Field("Company", "text", "Company Name", false, 0, 200, 3),
                Field("Service", "select", "Select Service", true, 0, 100, 4,
                [
                    Option("Consulting", "Consulting", 0),
                    Option("Implementation", "Implementation", 1),
                    Option("Support", "Support", 2),
                    Option("Other", "Other", 3)
                ]),
                Field("Message", "textarea", "Your Message", false, 0, 2000, 5)
            ]
        };

        var insightFields = new List<FormDefinitionField>
        {
            Field("Name", "text", "Your name", true, 0, 150, 0),
            Field("Company", "text", "Company", false, 0, 200, 1),
            Field("Email", "email", "Email address", true, 0, 254, 2)
        };
        var insightName = new Dictionary<string, string>
        {
            ["en"] = "Stay Ahead with U&I Logistics Intelligence",
            ["vi"] = "Đón đầu xu hướng cùng tri thức logistics từ U&I",
            ["cn"] = "借助 U&I 物流洞察把握先机"
        };
        var insightIntroduction = new Dictionary<string, string>
        {
            ["en"] = "Receive our monthly insights, reports, and industry analyses.",
            ["vi"] = "Nhận thông tin chuyên sâu, báo cáo và phân tích ngành hằng tháng.",
            ["cn"] = "每月接收我们的洞察、报告和行业分析。"
        };
        var insightSubmit = new Dictionary<string, string>
        {
            ["en"] = "Subscribe",
            ["vi"] = "Đăng ký",
            ["cn"] = "订阅"
        };
        var insightDesign = FormDesignPolicy.Normalize(
            new FormDesignSettingsDto
            {
                Shape = FormDesignShape.Cta,
                WidthPx = FormDesignPolicy.CtaDefaultWidthPx,
                BackgroundMode = FormDesignBackgroundMode.Solid,
                BackgroundColor = "#001a33",
                TextColor = "#ffffff",
                AccentColor = "#d6b15e",
                BorderColor = "#24445f",
                BorderWidthPx = 1,
                BorderRadiusPx = 18,
                Shadow = "medium",
                PaddingPx = 40,
                FieldGapPx = 14,
                TextAlign = FormDesignTextAlign.Left,
                LabelMode = FormLabelMode.InsideInputs,
                ButtonStyle = "filled",
                ButtonWidth = FormDesignButtonWidth.Content
            },
            insightFields.Select(MapFieldDto),
            insightName,
            insightIntroduction,
            insightSubmit);
        yield return new FormDefinition
        {
            Id = InsightSubscriptionDefinitionId,
            Key = "insight-subscription",
            Name = insightName,
            Introduction = insightIntroduction,
            SubmitButtonLabel = insightSubmit,
            DisplayMode = LegacyFormDisplayMode.Embedded,
            Layout = LegacyFormLayout.Stacked,
            Design = MapDesign(insightDesign),
            Active = true,
            CreatedAt = now,
            UpdatedAt = now,
            Fields = insightFields
        };
    }

    private static FormDefinitionField Field(
        string key,
        string type,
        string label,
        bool required,
        int min,
        int max,
        int order,
        List<FormDefinitionFieldOption>? options = null)
    {
        var normalizedType = FormInputTypeCatalog.NormalizeType(type);
        var capability = FormInputTypeCatalog.Get(normalizedType);
        return new FormDefinitionField
        {
            Key = key,
            Type = normalizedType,
            Label = new() { ["en"] = label },
            Placeholder = new() { ["en"] = label },
            Required = required,
            MinLength = capability.SupportsMaxCharacters ? min : 0,
            MaxLength = FormInputTypeCatalog.NormalizeMaxCharacters(normalizedType, max),
            InputBoxSize = FormInputTypeCatalog.DefaultInputBoxSize(normalizedType),
            Order = order,
            Options = capability.SupportsOptions ? options ?? new() : new()
        };
    }

    private static FormDefinitionFieldOption Option(string value, string label, int order) => new()
    {
        Value = value,
        Label = new() { ["en"] = label },
        Order = order
    };

    public static FormDesignSettingsDto MapDesign(
        FormDesignSettings? design,
        bool includeV2Projection = false,
        string? definitionId = null,
        IEnumerable<FormFieldDefinitionDto>? fields = null,
        bool readStoredV2 = true)
    {
        var mapped = design is null
            ? FormDesignPolicy.CreateDefault()
            : new FormDesignSettingsDto
        {
            UseThemeDefaults = design.UseThemeDefaults ?? IsUntouchedLegacyAppearance(design),
            SchemaVersion = design.SchemaVersion,
            Shape = design.Shape,
            WidthPx = design.WidthPx,
            CalculatedHeightPx = design.CalculatedHeightPx,
            BackgroundMode = design.BackgroundMode,
            BackgroundColor = design.BackgroundColor,
            TextColor = design.TextColor,
            AccentColor = design.AccentColor,
            BorderColor = design.BorderColor,
            BorderWidthPx = design.BorderWidthPx,
            BorderRadiusPx = design.BorderRadiusPx,
            Shadow = design.Shadow,
            PaddingPx = design.PaddingPx,
            FieldGapPx = design.FieldGapPx,
            TextAlign = design.TextAlign,
            LabelMode = design.LabelMode,
            ButtonStyle = design.ButtonStyle,
            ButtonWidth = design.ButtonWidth
        };

        if (design?.V2 is not null && readStoredV2)
            mapped.V2 = MapDesignV2(design.V2);
        else if (includeV2Projection)
            mapped.V2 = FormDesignV2Policy.ProjectFromV1(definitionId, mapped, fields);
        return mapped;
    }

    public static FormDesignSettings MapDesign(FormDesignSettingsDto design) => new()
    {
        // Phases 2-15 are deliberately v1-write only. The v2 projection is never
        // copied into Mongo by an ordinary Form Definition save.
        UseThemeDefaults = design.UseThemeDefaults,
        SchemaVersion = FormDesignPolicy.CurrentSchemaVersion,
        Shape = design.Shape,
        WidthPx = design.WidthPx,
        CalculatedHeightPx = design.CalculatedHeightPx,
        BackgroundMode = design.BackgroundMode,
        BackgroundColor = design.BackgroundColor,
        TextColor = design.TextColor,
        AccentColor = design.AccentColor,
        BorderColor = design.BorderColor,
        BorderWidthPx = design.BorderWidthPx,
        BorderRadiusPx = design.BorderRadiusPx,
        Shadow = design.Shadow,
        PaddingPx = design.PaddingPx,
        FieldGapPx = design.FieldGapPx,
        TextAlign = design.TextAlign,
        LabelMode = design.LabelMode,
        ButtonStyle = design.ButtonStyle,
        ButtonWidth = design.ButtonWidth,
        V2 = null
    };

    public static bool IsUntouchedLegacyAppearance(FormDesignSettings design)
    {
        static bool Same(string? left, string right) =>
            string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);

        var expectedWidths = design.Shape switch
        {
            FormDesignShape.TwoColumns => new[] { FormDesignPolicy.TwoColumnDefaultWidthPx, 980 },
            FormDesignShape.Cta => new[] { FormDesignPolicy.CtaDefaultWidthPx, 980 },
            _ => new[] { FormDesignPolicy.StackedDefaultWidthPx }
        };
        var v2UsesDefaultColors = design.V2 is null ||
            Same(design.V2.InformationBackgroundColor, "#0f2740") &&
            Same(design.V2.InformationTextColor, "#ffffff") &&
            Same(design.V2.FormBackgroundColor, "#ffffff") &&
            Same(design.V2.FormTextColor, "#0f172a");

        return expectedWidths.Contains(design.WidthPx) &&
               design.BackgroundMode == FormDesignBackgroundMode.Solid &&
               Same(design.BackgroundColor, "#ffffff") &&
               Same(design.TextColor, "#0f172a") &&
               Same(design.AccentColor, "#1d4ed8") &&
               Same(design.BorderColor, "#dbe3ef") &&
               design.BorderWidthPx == 1 &&
               design.BorderRadiusPx == 16 &&
               Same(design.Shadow, "small") &&
               design.PaddingPx == 32 &&
               design.FieldGapPx == 16 &&
               design.TextAlign == FormDesignTextAlign.Left &&
               design.LabelMode == FormLabelMode.Visible &&
               Same(design.ButtonStyle, "filled") &&
               design.ButtonWidth == FormDesignButtonWidth.Full &&
               v2UsesDefaultColors;
    }

    public static FormDesignSettingsDto NormalizeV2WriteDesign(
        string definitionId,
        FormDesignSettingsDto design,
        IReadOnlyCollection<FormFieldDefinitionDto> fields,
        IReadOnlyDictionary<string, string>? name = null,
        IReadOnlyDictionary<string, string>? introduction = null,
        IReadOnlyDictionary<string, string>? submitLabel = null,
        IEnumerable<FormInformationItemDto>? informationItems = null,
        IEnumerable<FormAuxiliaryActionDto>? auxiliaryActions = null)
    {
        var normalizedLegacy = FormDesignPolicy.Normalize(
            design,
            fields,
            name,
            introduction,
            submitLabel);
        var normalizedV2 = design.V2 is null
            ? FormDesignV2Policy.ProjectFromV1(definitionId, normalizedLegacy, fields)
            : FormDesignV2Policy.Normalize(design.V2);

        normalizedLegacy.SchemaVersion = FormDesignV2Policy.TargetSchemaVersion;
        normalizedLegacy.Shape = FormDesignV2Policy.LegacyShape(normalizedV2.OuterLayout);
        normalizedLegacy.WidthPx = Math.Clamp(
            design.WidthPx,
            FormDesignV2Policy.MinimumWidth(normalizedV2.OuterLayout),
            FormDesignV2Policy.MaximumWidth(normalizedV2.OuterLayout));
        normalizedLegacy.FieldGapPx = Math.Clamp(
            design.FieldGapPx,
            FormDesignV2Policy.MinimumFieldGapPx,
            FormDesignV2Policy.MaximumFieldGapPx);
        normalizedLegacy.BackgroundColor = normalizedV2.FormBackgroundColor;
        normalizedLegacy.TextColor = normalizedV2.FormTextColor;
        normalizedLegacy.ButtonWidth = normalizedV2.SubmitLayout == FormSubmitLayout.Full
            ? FormDesignButtonWidth.Full
            : FormDesignButtonWidth.Content;
        normalizedLegacy.V2 = normalizedV2;
        normalizedLegacy.CalculatedHeightPx = FormDesignV2Policy.CalculateHeight(
            normalizedLegacy,
            normalizedV2,
            fields,
            name,
            introduction,
            submitLabel,
            informationItems,
            auxiliaryActions);
        return normalizedLegacy;
    }

    public static FormDesignSettings MapDesignV2Write(FormDesignSettingsDto design)
    {
        if (design.V2 is null)
            throw new ArgumentException("A normalized Form Design v2 payload is required.", nameof(design));

        var mapped = MapDesign(design);
        mapped.SchemaVersion = FormDesignV2Policy.TargetSchemaVersion;
        mapped.V2 = MapDesignV2Model(design.V2);
        return mapped;
    }

    private static FormDesignV2Settings MapDesignV2Model(FormDesignV2SettingsDto design)
    {
        var normalized = FormDesignV2Policy.Normalize(design);
        return new FormDesignV2Settings
        {
            OuterLayout = normalized.OuterLayout,
            FieldRows = normalized.FieldRows.Select(row => new FormFieldRow
            {
                Id = row.Id,
                Order = row.Order,
                FieldKeys = new(row.FieldKeys)
            }).ToList(),
            InformationBackgroundColor = normalized.InformationBackgroundColor,
            InformationTextColor = normalized.InformationTextColor,
            FormBackgroundColor = normalized.FormBackgroundColor,
            FormTextColor = normalized.FormTextColor,
            SplitPanelPercent = normalized.SplitPanelPercent,
            SubmitLayout = normalized.SubmitLayout,
            AuxiliaryActionLayouts = normalized.AuxiliaryActionLayouts.Select(layout => new FormAuxiliaryActionLayout
            {
                ActionId = layout.ActionId,
                Style = layout.Style,
                Placement = layout.Placement
            }).ToList()
        };
    }

    public static FormDesignV2SettingsDto MapDesignV2(FormDesignV2Settings design) =>
        FormDesignV2Policy.Normalize(new FormDesignV2SettingsDto
        {
            OuterLayout = design.OuterLayout,
            FieldRows = design.FieldRows
                .OrderBy(row => row.Order)
                .Select(row => new FormFieldRowDto
                {
                    Id = row.Id,
                    Order = row.Order,
                    FieldKeys = new(row.FieldKeys)
                }).ToList(),
            InformationBackgroundColor = design.InformationBackgroundColor,
            InformationTextColor = design.InformationTextColor,
            FormBackgroundColor = design.FormBackgroundColor,
            FormTextColor = design.FormTextColor,
            SplitPanelPercent = design.SplitPanelPercent,
            SubmitLayout = design.SubmitLayout,
            AuxiliaryActionLayouts = design.AuxiliaryActionLayouts.Select(layout => new FormAuxiliaryActionLayoutDto
            {
                ActionId = layout.ActionId,
                Style = layout.Style,
                Placement = layout.Placement
            }).ToList()
        });

    public static List<FormInformationItemDto> MapInformationItems(
        IEnumerable<FormInformationItem>? source,
        IReadOnlyDictionary<string, string>? managedResourceUrls = null) =>
        (source ?? Array.Empty<FormInformationItem>())
            .OrderBy(item => item.Order)
            .Select(item => new FormInformationItemDto
            {
                Id = item.Id,
                Icon = item.Icon,
                Text = new(item.Text),
                Target = MapTarget(item.Target, managedResourceUrls),
                Order = item.Order
            }).ToList();

    public static List<FormAuxiliaryActionDto> MapAuxiliaryActions(
        IEnumerable<FormAuxiliaryAction>? source,
        IReadOnlyDictionary<string, string>? managedResourceUrls = null) =>
        (source ?? Array.Empty<FormAuxiliaryAction>())
            .OrderBy(action => action.Order)
            .Select(action => new FormAuxiliaryActionDto
            {
                Id = action.Id,
                Label = new(action.Label),
                Target = MapTarget(action.Target, managedResourceUrls) ?? new FormActionTargetDto(),
                Order = action.Order
            }).ToList();

    public static List<FormInformationItem> MapInformationItems(
        IEnumerable<FormInformationItemDto>? source) =>
        (source ?? Array.Empty<FormInformationItemDto>())
            .OrderBy(item => item.Order)
            .Select((item, index) => new FormInformationItem
            {
                Id = item.Id?.Trim() ?? string.Empty,
                Icon = item.Icon?.Trim() ?? string.Empty,
                Text = CleanTextMap(item.Text),
                Target = MapTarget(item.Target),
                Order = index
            }).ToList();

    public static List<FormAuxiliaryAction> MapAuxiliaryActions(
        IEnumerable<FormAuxiliaryActionDto>? source) =>
        (source ?? Array.Empty<FormAuxiliaryActionDto>())
            .OrderBy(action => action.Order)
            .Select((action, index) => new FormAuxiliaryAction
            {
                Id = action.Id?.Trim() ?? string.Empty,
                Label = CleanTextMap(action.Label),
                Target = MapTarget(action.Target) ?? new FormActionTarget(),
                Order = index
            }).ToList();

    private static FormActionTarget? MapTarget(FormActionTargetDto? target)
    {
        if (target is null) return null;
        return new FormActionTarget
        {
            Type = target.Type,
            PageId = CleanOptional(target.PageId),
            Path = CleanOptional(target.Path),
            Url = CleanOptional(target.Url),
            ResourceId = CleanOptional(target.ResourceId),
            Phone = CleanOptional(target.Phone),
            Email = CleanOptional(target.Email)
        };
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FormActionTargetDto? MapTarget(
        FormActionTarget? target,
        IReadOnlyDictionary<string, string>? managedResourceUrls)
    {
        if (target is null) return null;
        var mapped = new FormActionTargetDto
        {
            Type = target.Type,
            PageId = target.PageId,
            Path = target.Path,
            Url = target.Url,
            ResourceId = target.ResourceId,
            Phone = target.Phone,
            Email = target.Email
        };
        mapped.ResolvedHref = FormDesignV2Policy.ResolveHref(mapped, managedResourceUrls);
        return mapped;
    }

    private static FormFieldDefinitionDto MapPublicFieldDto(
        FormDefinitionField field,
        IReadOnlyDictionary<string, FormInputTypeCapability>? capabilities) => new()
    {
        Key = field.Key,
        Type = field.Type,
        Label = new(field.Label),
        Placeholder = new(field.Placeholder),
        Required = field.Required,
        MinLength = field.MinLength,
        MaxLength = FormInputTypeCatalog.MaximumInputLength(Capability(field.Type, capabilities)),
        InputBoxSize = FormInputTypeCatalog.NormalizeInputBoxSize(Capability(field.Type, capabilities), field.InputBoxSize),
        Order = field.Order,
        Options = field.Options
            .OrderBy(option => option.Order)
            .Select(option => new FormFieldOptionDto
            {
                Value = option.Value,
                Label = new(option.Label),
                Order = option.Order
            }).ToList()
    };

    private static FormFieldDefinitionDto MapFieldDto(FormDefinitionField field) => new()
    {
        Key = field.Key,
        Type = field.Type,
        Label = new(field.Label),
        Placeholder = new(field.Placeholder),
        Required = field.Required,
        MinLength = field.MinLength,
        MaxLength = field.MaxLength,
        InputBoxSize = field.InputBoxSize,
        Order = field.Order,
        Options = field.Options.Select(option => new FormFieldOptionDto
        {
            Value = option.Value,
            Label = new(option.Label),
            Order = option.Order
        }).ToList()
    };

    private static bool DesignsEqual(FormDesignSettingsDto left, FormDesignSettingsDto right) =>
        left.UseThemeDefaults == right.UseThemeDefaults &&
        left.SchemaVersion == right.SchemaVersion &&
        left.Shape == right.Shape &&
        left.WidthPx == right.WidthPx &&
        left.CalculatedHeightPx == right.CalculatedHeightPx &&
        left.BackgroundMode == right.BackgroundMode &&
        left.BackgroundColor == right.BackgroundColor &&
        left.TextColor == right.TextColor &&
        left.AccentColor == right.AccentColor &&
        left.BorderColor == right.BorderColor &&
        left.BorderWidthPx == right.BorderWidthPx &&
        left.BorderRadiusPx == right.BorderRadiusPx &&
        left.Shadow == right.Shadow &&
        left.PaddingPx == right.PaddingPx &&
        left.FieldGapPx == right.FieldGapPx &&
        left.TextAlign == right.TextAlign &&
        left.LabelMode == right.LabelMode &&
        left.ButtonStyle == right.ButtonStyle &&
        left.ButtonWidth == right.ButtonWidth &&
        FormDesignV2Policy.AreEquivalent(left.V2, right.V2);

    private static FormDefinitionField MapRequestField(
        FormFieldDefinitionDto field,
        int index,
        IReadOnlyDictionary<string, FormInputTypeCapability>? capabilities = null)
    {
        var type = FormInputTypeCatalog.NormalizeType(field.Type);
        var capability = Capability(type, capabilities);
        var options = capability.SupportsOptions
            ? field.Options
                .OrderBy(option => option.Order)
                .Select((option, optionIndex) => new FormDefinitionFieldOption
                {
                    Value = option.Value.Trim(),
                    Label = CleanTextMap(option.Label),
                    Order = optionIndex
                })
                .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                .ToList()
            : new List<FormDefinitionFieldOption>();

        return new FormDefinitionField
        {
            Key = CleanFieldKey(field.Key, index),
            Type = type,
            Label = CleanTextMap(field.Label),
            Placeholder = CleanTextMap(field.Placeholder),
            Required = field.Required,
            MinLength = capability.SupportsMaxCharacters
                ? Math.Clamp(field.MinLength, 0, FormInputTypeCatalog.MaxCharactersLimit)
                : 0,
            MaxLength = FormInputTypeCatalog.MaximumInputLength(capability),
            InputBoxSize = FormInputTypeCatalog.NormalizeInputBoxSize(capability, field.InputBoxSize),
            Order = index,
            Options = options
        };
    }

}
