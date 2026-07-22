using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

var options = CleanupOptions.Parse(args);
var configuration = await LoadConfigurationAsync(options.ConfigurationPath);
var client = new MongoClient(configuration.ConnectionString);
var database = client.GetDatabase(configuration.DatabaseName);

Console.WriteLine($"Fallback cleanup phase : {options.Phase}");
Console.WriteLine($"Target database        : {configuration.DatabaseName}");
Console.WriteLine($"Mode                   : {(options.Apply ? "APPLY" : "DRY RUN")}");

switch (options.Phase)
{
    case "logs":
        await RunLogCleanupAsync(database, options.Apply);
        break;
    case "roles":
        await RunRoleCleanupAsync(database, options.Apply);
        break;
    case "content":
        await RunContentCleanupAsync(database, options.Apply);
        break;
    case "forms":
        await RunFormCleanupAsync(database, options.Apply);
        break;
    case "form-seed":
        await RunFormSeedCleanupAsync(database, options.Apply);
        break;
    case "blocks":
        await RunBlockCleanupAsync(database, options.Apply);
        break;
    case "block-seed":
        await RunBlockSeedCleanupAsync(database, options.Apply);
        break;
    case "localization":
        await RunLocalizationCleanupAsync(database, options.Apply);
        break;
    case "localization-seed":
        await RunLocalizationSeedCleanupAsync(database, options.Apply);
        break;
    case "sections":
        await RunSectionAuditAsync(database, options.Apply);
        break;
    case "columns":
        await RunColumnCleanupAsync(database, options.Apply);
        break;
    case "columns-seed":
        await RunColumnSeedCleanupAsync(options.Apply);
        break;
    case "resource-albums":
        await RunResourceAlbumCleanupAsync(database, options.Apply);
        break;
    case "section-presets":
        await RunSectionPresetCleanupAsync(database, options.Apply);
        break;
    case "section-preset-seed":
        await RunSectionPresetSeedCleanupAsync(database, options.Apply);
        break;
    case "cta-layouts":
        await RunCtaLayoutCleanupAsync(database, options.Apply);
        break;
    case "cta-layout-seed":
        await RunCtaLayoutSeedCleanupAsync(options.Apply);
        break;
    default:
        throw new InvalidOperationException($"Unknown cleanup phase '{options.Phase}'.");
}

static async Task RunCtaLayoutCleanupAsync(IMongoDatabase database, bool apply)
{
    var allowed = new HashSet<string>(["center", "left", "right", "final-card", "about-final"], StringComparer.Ordinal);
    var plans = new List<(string Collection, BsonDocument Document)>();
    var ctaCount = 0;
    var oldLayouts = 0;
    var invalid = new List<string>();
    foreach (var collectionName in new[] { "sections_draft", "sections_published", "canvas_section_presets" })
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (var row in rows)
        {
            var cta = collectionName == "canvas_section_presets"
                ? row.GetValue("Section", BsonNull.Value) is { IsBsonDocument: true } section && ReadDiscriminator(section.AsBsonDocument) == "cta"
                    ? section.AsBsonDocument
                    : null
                : ReadDiscriminator(row) == "cta" ? row : null;
            if (cta is null) continue;
            ctaCount++;
            var layout = cta.GetValue("Layout", string.Empty).ToString() ?? string.Empty;
            if (layout is "centered" or "stacked" or "")
            {
                cta["Layout"] = "center";
                plans.Add((collectionName, row));
                oldLayouts++;
            }
            else if (!allowed.Contains(layout))
            {
                invalid.Add($"{collectionName}:{ReadId(row)}:{layout}");
            }
        }
    }
    Console.WriteLine($"CTA Sections              : {ctaCount}");
    Console.WriteLine($"Old CTA layout tokens     : {oldLayouts}");
    Console.WriteLine($"Unsupported CTA layouts   : {invalid.Count}");
    foreach (var item in invalid) Console.WriteLine($"  BLOCKED {item}");
    if (invalid.Count > 0)
        throw new InvalidOperationException("CTA layout cleanup found unsupported tokens without a deterministic mapping.");
    Console.WriteLine("CTA layout reconciliation : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }
    foreach (var plan in plans)
        await database.GetCollection<BsonDocument>(plan.Collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", plan.Document["_id"]), plan.Document);
    Console.WriteLine($"Canonical CTA documents   : {plans.Count}");
}

static async Task RunCtaLayoutSeedCleanupAsync(bool apply)
{
    var sourceDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "demo-seed", "collections"));
    var publishDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections"));
    var plans = new List<(string SourcePath, string PublishPath, BsonValue Value)>();
    var updated = 0;
    var invalid = new List<string>();
    foreach (var fileName in new[] { "sections_draft.json", "sections_published.json", "canvas_section_presets.json" })
    {
        var sourcePath = Path.Combine(sourceDirectory, fileName);
        var value = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath));
        var before = updated;
        ReconcileCtaLayouts(value, fileName, invalid, ref updated);
        if (updated > before)
            plans.Add((sourcePath, Path.Combine(publishDirectory, fileName), value));
    }
    Console.WriteLine($"Seed CTA layouts updated  : {updated}");
    Console.WriteLine($"Unsupported seed layouts  : {invalid.Count}");
    foreach (var item in invalid) Console.WriteLine($"  BLOCKED {item}");
    if (invalid.Count > 0)
        throw new InvalidOperationException("CTA seed cleanup found unsupported tokens without a deterministic mapping.");
    Console.WriteLine("CTA layout seed reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }
    foreach (var plan in plans)
    {
        var json = plan.Value.ToJson(new JsonWriterSettings { Indent = true, OutputMode = JsonOutputMode.CanonicalExtendedJson });
        await File.WriteAllTextAsync(plan.SourcePath, json);
        await File.WriteAllTextAsync(plan.PublishPath, json);
    }
    Console.WriteLine($"Canonical CTA seed files  : {plans.Count}");
}

static void ReconcileCtaLayouts(BsonValue value, string path, ICollection<string> invalid, ref int updated)
{
    if (value.IsBsonArray)
    {
        for (var index = 0; index < value.AsBsonArray.Count; index++)
            ReconcileCtaLayouts(value.AsBsonArray[index], $"{path}[{index}]", invalid, ref updated);
        return;
    }
    if (!value.IsBsonDocument) return;
    var document = value.AsBsonDocument;
    if (ReadDiscriminator(document) == "cta")
    {
        var layout = document.GetValue("Layout", string.Empty).ToString() ?? string.Empty;
        if (layout is "centered" or "stacked" or "")
        {
            document["Layout"] = "center";
            updated++;
        }
        else if (layout is not ("center" or "left" or "right" or "final-card" or "about-final"))
            invalid.Add($"{path}:{layout}");
    }
    foreach (var element in document.Elements.ToList())
        ReconcileCtaLayouts(element.Value, $"{path}.{element.Name}", invalid, ref updated);
}

static async Task RunSectionPresetCleanupAsync(IMongoDatabase database, bool apply)
{
    const int currentSchema = 4;
    var collection = database.GetCollection<BsonDocument>("canvas_section_presets");
    var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var invalidVersion = rows.Count(row => ReadEnumValue(row.GetValue("SchemaVersion", 0)) != currentSchema);
    var missingSection = rows.Count(row => !HasDocument(row, "Section"));
    var topLevelStyle = rows.Count(row => row.Contains("Style"));
    var embeddedBlocks = rows.Sum(row => row.GetValue("Blocks", new BsonArray()).AsBsonArray.Count);
    var nonCanonicalBlocks = rows.Sum(row => row.GetValue("Blocks", new BsonArray()).AsBsonArray
        .Where(value => value.IsBsonDocument)
        .Select(value => value.AsBsonDocument)
        .Count(block =>
            !HasDocument(block, "Layout") || !HasVersion(block, "Appearance", 2) ||
            !HasVersion(block, "Responsive", 1) || !HasVersion(block, "Animation", 1) ||
            !HasVersion(block, "Authoring", 1) ||
            block.Contains("ImageUrl") || block.Contains("EmbedUrl") ||
            block.Contains("FileUrl") || block.Contains("FileType")));

    Console.WriteLine($"Section presets            : {rows.Count}");
    Console.WriteLine($"Non-v{currentSchema} presets         : {invalidVersion}");
    Console.WriteLine($"Missing Section snapshots  : {missingSection}");
    Console.WriteLine($"Top-level Style mirrors    : {topLevelStyle}");
    Console.WriteLine($"Embedded Blocks            : {embeddedBlocks}");
    Console.WriteLine($"Non-canonical Blocks       : {nonCanonicalBlocks}");
    if (missingSection > 0 || rows.Any(row => ReadEnumValue(row.GetValue("SchemaVersion", 0)) > currentSchema))
        throw new InvalidOperationException("Section preset cleanup requires supported presets with complete Section snapshots.");

    Console.WriteLine("Section preset reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    var updated = 0;
    foreach (var row in rows)
    {
        var changed = false;
        var section = row["Section"].AsBsonDocument;
        if (!HasDocument(section, "Style") && row.TryGetValue("Style", out var style) && style.IsBsonDocument)
        {
            section["Style"] = style.DeepClone();
            changed = true;
        }
        if (row.Contains("Style"))
        {
            row.Remove("Style");
            changed = true;
        }
        if (ReadEnumValue(row.GetValue("SchemaVersion", 0)) != currentSchema)
        {
            row["SchemaVersion"] = currentSchema;
            changed = true;
        }
        foreach (var block in row.GetValue("Blocks", new BsonArray()).AsBsonArray
                     .Where(value => value.IsBsonDocument)
                     .Select(value => value.AsBsonDocument))
            changed |= CanonicalizePresetBlock(block);
        if (!changed) continue;
        await collection.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", row["_id"]), row);
        updated++;
    }
    Console.WriteLine($"Canonical presets written  : {updated}");
}

static bool CanonicalizePresetBlock(BsonDocument row)
{
    var changed = false;
    var layout = GetOrCreateDocument(row, "Layout", CreateDefaultBlockLayout(), ref changed);
    if (!HasVersion(row, "Appearance", 2))
    {
        row["Appearance"] = CreateCanonicalAppearance(row, layout);
        changed = true;
    }
    if (!HasVersion(row, "Responsive", 1))
    {
        row["Responsive"] = new BsonDocument { ["SchemaVersion"] = 1 };
        changed = true;
    }
    if (!HasVersion(row, "Animation", 1))
    {
        row["Animation"] = new BsonDocument
        {
            ["SchemaVersion"] = 1, ["Effect"] = "none", ["Trigger"] = "enter-viewport",
            ["DurationMs"] = 500, ["DelayMs"] = 0, ["Easing"] = "ease-out",
            ["PlayOnce"] = true, ["StaggerMs"] = 0, ["ContinuousEffect"] = "none",
            ["DisableForReducedMotion"] = true
        };
        changed = true;
    }
    if (!HasVersion(row, "Authoring", 1))
    {
        row["Authoring"] = new BsonDocument
        {
            ["SchemaVersion"] = 1, ["ContentLocked"] = false,
            ["GeometryLocked"] = false, ["FullLocked"] = false
        };
        changed = true;
    }
    changed |= CanonicalizeAsset(row);
    changed |= CanonicalizeContainer(row);
    return changed;
}

static async Task RunSectionPresetSeedCleanupAsync(IMongoDatabase database, bool apply)
{
    var sourcePath = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "demo-seed", "collections", "canvas_section_presets.json"));
    var publishPath = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections", "canvas_section_presets.json"));
    var seed = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath)).AsBsonArray;
    var ids = seed.Select(value => ReadId(value.AsBsonDocument)).ToList();
    var live = await database.GetCollection<BsonDocument>("canvas_section_presets")
        .Find(Builders<BsonDocument>.Filter.In("_id", ids.Select(ObjectId.Parse)))
        .ToListAsync();
    var liveById = live.ToDictionary(ReadId, StringComparer.Ordinal);
    var canonical = ids.Where(liveById.ContainsKey).Select(id => liveById[id]).ToList();
    var valid = canonical.Count(row =>
        ReadEnumValue(row.GetValue("SchemaVersion", 0)) == 4 && HasDocument(row, "Section") && !row.Contains("Style"));
    Console.WriteLine($"Seed presets               : {ids.Count}");
    Console.WriteLine($"Matching live presets      : {canonical.Count}");
    Console.WriteLine($"Canonical live presets     : {valid}");
    if (canonical.Count != ids.Count || valid != ids.Count)
        throw new InvalidOperationException("Every seeded Section preset must have a canonical live source.");
    Console.WriteLine("Section preset seed reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }
    var json = new BsonArray(canonical).ToJson(new JsonWriterSettings
    {
        Indent = true,
        OutputMode = JsonOutputMode.CanonicalExtendedJson
    });
    await File.WriteAllTextAsync(sourcePath, json);
    await File.WriteAllTextAsync(publishPath, json);
    Console.WriteLine("Canonical Section preset seeds were written.");
}

static async Task RunResourceAlbumCleanupAsync(IMongoDatabase database, bool apply)
{
    var albums = database.GetCollection<BsonDocument>("resource_albums");
    var resources = database.GetCollection<BsonDocument>("managed_resources");
    var legacyFilter = Builders<BsonDocument>.Filter.Or(
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("Scope", "media"),
            Builders<BsonDocument>.Filter.Eq("Name", "Unsorted Media")),
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("Scope", "file"),
            Builders<BsonDocument>.Filter.Eq("Name", "Unsorted Files")));
    var legacyAlbums = await albums.Find(legacyFilter).ToListAsync();
    var albumIds = legacyAlbums.Select(row => row["_id"]).ToList();
    var assignedResources = albumIds.Count == 0
        ? 0
        : await resources.CountDocumentsAsync(Builders<BsonDocument>.Filter.In("AlbumId", albumIds));

    Console.WriteLine($"Legacy default albums      : {legacyAlbums.Count}");
    Console.WriteLine($"Resources to unfile        : {assignedResources}");
    Console.WriteLine("Resource-album reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    if (albumIds.Count > 0)
    {
        await resources.UpdateManyAsync(
            Builders<BsonDocument>.Filter.In("AlbumId", albumIds),
            Builders<BsonDocument>.Update
                .Unset("AlbumId")
                .Set("UpdatedById", "system")
                .Set("UpdatedAt", DateTime.UtcNow));
        await albums.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", albumIds));
    }
    Console.WriteLine($"Removed legacy albums      : {legacyAlbums.Count}");
}

static async Task RunColumnCleanupAsync(IMongoDatabase database, bool apply)
{
    var plans = new List<(string Collection, BsonDocument Document)>();
    var embeddedBlocks = 0;
    var compatibilityFields = 0;

    foreach (var collectionName in new[] { "sections_draft", "sections_published" })
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (var row in rows.Where(row => ReadDiscriminator(row) == "columns"))
        {
            var changed = false;
            foreach (var slot in row.GetValue("Columns", new BsonArray()).AsBsonArray
                         .Where(value => value.IsBsonDocument)
                         .Select(value => value.AsBsonDocument))
            {
                if (!slot.TryGetValue("Blocks", out var blocks)) continue;
                compatibilityFields++;
                if (blocks.IsBsonArray) embeddedBlocks += blocks.AsBsonArray.Count;
                slot.Remove("Blocks");
                changed = true;
            }
            if (changed) plans.Add((collectionName, row));
        }
    }

    Console.WriteLine($"Embedded Block records      : {embeddedBlocks}");
    Console.WriteLine($"Compatibility fields       : {compatibilityFields}");
    Console.WriteLine($"Documents to canonicalize  : {plans.Count}");
    if (embeddedBlocks > 0)
        throw new InvalidOperationException("Column cleanup is blocked until embedded Blocks are reconciled with the flat Block collections.");

    Console.WriteLine("Column storage reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    foreach (var plan in plans)
    {
        await database.GetCollection<BsonDocument>(plan.Collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", plan.Document["_id"]),
            plan.Document);
    }
    Console.WriteLine($"Canonical Column documents : {plans.Count}");
}

static async Task RunColumnSeedCleanupAsync(bool apply)
{
    var sourceDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "demo-seed", "collections"));
    var publishDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections"));
    var plans = new List<(string SourcePath, string PublishPath, BsonValue Value)>();
    var embeddedBlocks = 0;
    var compatibilityFields = 0;

    foreach (var fileName in new[] { "sections_draft.json", "sections_published.json", "canvas_section_presets.json" })
    {
        var sourcePath = Path.Combine(sourceDirectory, fileName);
        var seedValue = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath));
        var changed = RemoveEmptyEmbeddedBlocks(seedValue, ref compatibilityFields, ref embeddedBlocks);
        if (changed)
            plans.Add((sourcePath, Path.Combine(publishDirectory, fileName), seedValue));
    }

    Console.WriteLine($"Seed embedded Block records : {embeddedBlocks}");
    Console.WriteLine($"Seed compatibility fields  : {compatibilityFields}");
    Console.WriteLine($"Seed files to canonicalize : {plans.Count}");
    if (embeddedBlocks > 0)
        throw new InvalidOperationException("Column seed cleanup is blocked by non-empty embedded Block arrays.");

    Console.WriteLine("Column seed reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }

    foreach (var plan in plans)
    {
        var json = plan.Value.ToJson(new JsonWriterSettings
        {
            Indent = true,
            OutputMode = JsonOutputMode.CanonicalExtendedJson
        });
        await File.WriteAllTextAsync(plan.SourcePath, json);
        await File.WriteAllTextAsync(plan.PublishPath, json);
    }
    Console.WriteLine($"Canonical Column seed files: {plans.Count}");
}

static bool RemoveEmptyEmbeddedBlocks(BsonValue value, ref int fields, ref int embeddedBlocks)
{
    var changed = false;
    if (value.IsBsonArray)
    {
        foreach (var child in value.AsBsonArray)
            changed |= RemoveEmptyEmbeddedBlocks(child, ref fields, ref embeddedBlocks);
        return changed;
    }
    if (!value.IsBsonDocument) return false;

    var document = value.AsBsonDocument;
    if (!document.Contains("_id") &&
        document.Contains("Id") &&
        document.Contains("Order") &&
        document.TryGetValue("Blocks", out var blocks) &&
        blocks.IsBsonArray)
    {
        fields++;
        embeddedBlocks += blocks.AsBsonArray.Count;
        if (blocks.AsBsonArray.Count == 0)
        {
            document.Remove("Blocks");
            changed = true;
        }
    }
    foreach (var element in document.Elements.ToList())
        changed |= RemoveEmptyEmbeddedBlocks(element.Value, ref fields, ref embeddedBlocks);
    return changed;
}

static async Task RunSectionAuditAsync(IMongoDatabase database, bool apply)
{
    if (apply)
        throw new InvalidOperationException("The Section inventory is read-only.");

    foreach (var collectionName in new[] { "sections_draft", "sections_published" })
    {
        var rows = await database.GetCollection<BsonDocument>(collectionName)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("PageStableId").Ascending("Order"))
            .ToListAsync();
        var blocks = await database.GetCollection<BsonDocument>(
                collectionName == "sections_draft" ? "blocks_draft" : "blocks_published")
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
        var htmlRows = rows.Where(row => ReadDiscriminator(row) == "html").ToList();
        var columnRows = rows.Where(row => ReadDiscriminator(row) == "columns").ToList();

        Console.WriteLine($"{collectionName} HTML Sections: {htmlRows.Count}");
        foreach (var row in htmlRows)
        {
            var content = row.GetValue("Content", new BsonDocument()).AsBsonDocument;
            var english = content.GetValue("en", string.Empty).ToString() ?? string.Empty;
            var marker = ReadFirstCssClass(english);
            Console.WriteLine(
                $"  {ReadId(row)} | {row.GetValue("StableId", string.Empty)} | page={row.GetValue("PageStableId", string.Empty)} | " +
                $"order={ReadEnumValue(row.GetValue("Order", 0))} | chars={english.Length} | class={marker}");
        }

        Console.WriteLine($"{collectionName} Columns Sections: {columnRows.Count}");
        foreach (var row in columnRows)
        {
            var stableId = row.GetValue("StableId", string.Empty).ToString();
            var slots = row.GetValue("Columns", new BsonArray()).AsBsonArray
                .Where(value => value.IsBsonDocument)
                .Select(value => value.AsBsonDocument)
                .ToList();
            var embedded = slots.Sum(slot => slot.GetValue("Blocks", new BsonArray()).AsBsonArray.Count);
            var flat = blocks.Count(block =>
                string.Equals(block.GetValue("SectionStableId", string.Empty).ToString(), stableId, StringComparison.Ordinal));
            Console.WriteLine(
                $"  {ReadId(row)} | {stableId} | page={row.GetValue("PageStableId", string.Empty)} | " +
                $"slots={slots.Count} | embedded={embedded} | flat={flat} | ratio={row.GetValue("ColumnRatio", "equal")}");
        }
    }
    Console.WriteLine("Section inventory completed. No changes were made.");
}

static string ReadFirstCssClass(string html)
{
    const string marker = "class=\"";
    var start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (start < 0) return "<none>";
    start += marker.Length;
    var end = html.IndexOf('"', start);
    if (end <= start) return "<none>";
    return html[start..end].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "<none>";
}

static async Task RunLocalizationSeedCleanupAsync(IMongoDatabase database, bool apply)
{
    var settings = await database.GetCollection<BsonDocument>("settings")
        .Find(FilterDefinition<BsonDocument>.Empty)
        .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("Localization seed cleanup requires Site Settings.");
    var fallbackLanguage = (settings.GetValue("DefaultLanguage", "en").ToString() ?? "en").Trim().ToLowerInvariant();
    var languages = settings.GetValue("Languages", new BsonArray()).AsBsonArray
        .Where(value => value.IsBsonDocument)
        .Select(value => value.AsBsonDocument)
        .Where(value => value.GetValue("Active", false).ToBoolean() && value.GetValue("UserEnabled", false).ToBoolean())
        .Select(value => (value.GetValue("Slug", string.Empty).ToString() ?? string.Empty).Trim().ToLowerInvariant())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    var languageSet = languages.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var sourceDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "demo-seed", "collections"));
    var publishDirectory = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections"));
    var plans = new List<(string SourcePath, string PublishPath, BsonValue Value)>();
    var blockers = new List<string>();
    var localizedDictionaries = 0;
    var filledCells = 0;

    foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*.json").OrderBy(path => path))
    {
        var seedValue = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath));
        var before = filledCells;
        ReconcileLocalizedValues(
            seedValue,
            Path.GetFileNameWithoutExtension(sourcePath),
            languages,
            languageSet,
            fallbackLanguage,
            blockers,
            ref localizedDictionaries,
            ref filledCells);
        if (filledCells == before) continue;
        var publishPath = Path.Combine(publishDirectory, Path.GetFileName(sourcePath));
        plans.Add((sourcePath, publishPath, seedValue));
        Console.WriteLine($"  {Path.GetFileName(sourcePath),-32} {filledCells - before,4} value(s)");
    }

    Console.WriteLine($"Seed localized dictionaries: {localizedDictionaries}");
    Console.WriteLine($"Seed values to materialize : {filledCells}");
    Console.WriteLine($"Blocked seed dictionaries  : {blockers.Count}");
    foreach (var blocker in blockers.Take(20)) Console.WriteLine($"  BLOCKED {blocker}");
    if (blockers.Count > 0)
        throw new InvalidOperationException("Localization seed cleanup found dictionaries without a deterministic source value.");

    Console.WriteLine("Localization seed reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }

    foreach (var plan in plans)
    {
        var json = plan.Value.ToJson(new JsonWriterSettings
        {
            Indent = true,
            OutputMode = JsonOutputMode.CanonicalExtendedJson
        });
        await File.WriteAllTextAsync(plan.SourcePath, json);
        await File.WriteAllTextAsync(plan.PublishPath, json);
    }
    Console.WriteLine($"Canonical localized seed files: {plans.Count}");
}

static async Task RunLocalizationCleanupAsync(IMongoDatabase database, bool apply)
{
    var settings = await database.GetCollection<BsonDocument>("settings")
        .Find(FilterDefinition<BsonDocument>.Empty)
        .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("Localization cleanup requires Site Settings.");
    var fallbackLanguage = (settings.GetValue("DefaultLanguage", "en").ToString() ?? "en").Trim().ToLowerInvariant();
    var languages = settings.GetValue("Languages", new BsonArray()).AsBsonArray
        .Where(value => value.IsBsonDocument)
        .Select(value => value.AsBsonDocument)
        .Where(value => value.GetValue("Active", false).ToBoolean() && value.GetValue("UserEnabled", false).ToBoolean())
        .Select(value => (value.GetValue("Slug", string.Empty).ToString() ?? string.Empty).Trim().ToLowerInvariant())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (!languages.Contains(fallbackLanguage, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("The configured fallback language must be active for the user site.");

    var collectionNames = new[]
    {
        "pages_draft", "pages_published",
        "sections_draft", "sections_published",
        "blocks_draft", "blocks_published",
        "canvas_section_presets",
        "content_draft", "content_published", "content_types",
        "form_definitions", "managed_resources",
        "branding", "footer", "global_buttons", "social", "theme"
    };
    var languageSet = languages.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var plans = new List<(string Collection, BsonDocument Document)>();
    var blockers = new List<string>();
    var localizedDictionaries = 0;
    var filledCells = 0;

    Console.WriteLine($"Fallback language          : {fallbackLanguage}");
    Console.WriteLine($"Required user languages    : {string.Join(", ", languages)}");
    foreach (var collectionName in collectionNames)
    {
        if (!await CollectionExistsAsync(database, collectionName)) continue;
        var collection = database.GetCollection<BsonDocument>(collectionName);
        var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var changedRows = 0;
        var collectionCells = 0;
        foreach (var row in rows)
        {
            var before = filledCells;
            ReconcileLocalizedValues(
                row,
                $"{collectionName}:{ReadId(row)}",
                languages,
                languageSet,
                fallbackLanguage,
                blockers,
                ref localizedDictionaries,
                ref filledCells);
            if (filledCells == before) continue;
            plans.Add((collectionName, row));
            changedRows++;
            collectionCells += filledCells - before;
        }
        if (changedRows > 0)
            Console.WriteLine($"  {collectionName,-24} {changedRows,4} document(s), {collectionCells,4} value(s)");
    }

    Console.WriteLine($"Localized dictionaries     : {localizedDictionaries}");
    Console.WriteLine($"Values to materialize      : {filledCells}");
    Console.WriteLine($"Blocked dictionaries       : {blockers.Count}");
    foreach (var blocker in blockers.Take(20)) Console.WriteLine($"  BLOCKED {blocker}");
    if (blockers.Count > 0)
        throw new InvalidOperationException("Localization cleanup found dictionaries without a configured fallback-language value.");

    Console.WriteLine("Localization reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    foreach (var plan in plans)
    {
        await database.GetCollection<BsonDocument>(plan.Collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", plan.Document["_id"]),
            plan.Document);
    }
    Console.WriteLine($"Canonical localized documents: {plans.Count}");
}

static void ReconcileLocalizedValues(
    BsonValue value,
    string path,
    IReadOnlyList<string> languages,
    IReadOnlySet<string> languageSet,
    string fallbackLanguage,
    ICollection<string> blockers,
    ref int localizedDictionaries,
    ref int filledCells)
{
    if (value.IsBsonArray)
    {
        var values = value.AsBsonArray;
        for (var index = 0; index < values.Count; index++)
            ReconcileLocalizedValues(values[index], $"{path}[{index}]", languages, languageSet,
                fallbackLanguage, blockers, ref localizedDictionaries, ref filledCells);
        return;
    }
    if (!value.IsBsonDocument) return;

    var document = value.AsBsonDocument;
    var isLocalized = document.ElementCount > 0 &&
                      document.Elements.All(element => languageSet.Contains(element.Name) &&
                          (element.Value.IsString || element.Value.IsBsonNull)) &&
                      document.Elements.Any(element => element.Value.IsString &&
                          !string.IsNullOrWhiteSpace(element.Value.AsString));
    if (isLocalized)
    {
        localizedDictionaries++;
        var fallbackValue = document.Elements
            .Where(element => string.Equals(element.Name, fallbackLanguage, StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value)
            .FirstOrDefault();
        var fallback = fallbackValue?.IsString == true ? fallbackValue.AsString : string.Empty;
        if (!string.IsNullOrWhiteSpace(fallback)) return;

        var source = languages
            .Where(language => !string.Equals(language, fallbackLanguage, StringComparison.OrdinalIgnoreCase))
            .Select(language => new
            {
                Language = language,
                Value = document.Elements
                    .Where(item => string.Equals(item.Name, language, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Value)
                    .FirstOrDefault()
            })
            .FirstOrDefault(candidate => candidate.Value?.IsString == true &&
                                         !string.IsNullOrWhiteSpace(candidate.Value.AsString));
        if (source is null)
        {
            blockers.Add($"{path} (missing {fallbackLanguage})");
            return;
        }

        var existingName = document.Names.FirstOrDefault(name =>
            string.Equals(name, fallbackLanguage, StringComparison.OrdinalIgnoreCase));
        document[existingName ?? fallbackLanguage] = source.Value!.AsString;
        filledCells++;
        Console.WriteLine($"  SOURCE {path} <- {source.Language}");
        return;
    }

    foreach (var element in document.Elements.ToList())
        ReconcileLocalizedValues(element.Value, $"{path}.{element.Name}", languages, languageSet,
            fallbackLanguage, blockers, ref localizedDictionaries, ref filledCells);
}

static async Task RunBlockSeedCleanupAsync(IMongoDatabase database, bool apply)
{
    var collectionNames = new[] { "blocks_draft", "blocks_published" };
    var plans = new List<(string SourcePath, string PublishPath, List<BsonDocument> Rows)>();

    foreach (var collectionName in collectionNames)
    {
        var fileName = $"{collectionName}.json";
        var sourcePath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "Tool", "Tool For Demo Import", "demo-seed", "collections", fileName));
        var publishPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections", fileName));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"The canonical demo {collectionName} seed was not found.", sourcePath);

        var seedValue = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath));
        if (!seedValue.IsBsonArray)
            throw new InvalidOperationException($"The {collectionName} seed must be a JSON array.");
        var seedRows = seedValue.AsBsonArray.Select(value => value.AsBsonDocument).ToList();
        var seedIds = seedRows.Select(ReadId).ToList();
        if (seedIds.Distinct(StringComparer.Ordinal).Count() != seedIds.Count)
            throw new InvalidOperationException($"The {collectionName} seed contains duplicate IDs.");

        var liveRows = await database.GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.In("_id", seedIds.Select(ObjectId.Parse)))
            .ToListAsync();
        var liveById = liveRows.ToDictionary(ReadId, StringComparer.Ordinal);
        var canonicalRows = seedIds
            .Where(liveById.ContainsKey)
            .Select(id => liveById[id])
            .ToList();
        var validRows = canonicalRows.Count(row =>
            HasDocument(row, "Layout") &&
            HasVersion(row, "Appearance", 2) &&
            HasVersion(row, "Responsive", 1) &&
            HasVersion(row, "Animation", 1) &&
            HasVersion(row, "Authoring", 1));

        Console.WriteLine($"{collectionName} seed rows       : {seedRows.Count}");
        Console.WriteLine($"{collectionName} live matches    : {canonicalRows.Count}");
        Console.WriteLine($"{collectionName} canonical rows  : {validRows}");
        if (canonicalRows.Count != seedRows.Count || validRows != seedRows.Count)
            throw new InvalidOperationException(
                $"Every seeded {collectionName} block must have a validated canonical live source.");

        plans.Add((sourcePath, publishPath, canonicalRows));
    }

    Console.WriteLine("Block seed reconciliation: PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }

    foreach (var plan in plans)
    {
        var json = new BsonArray(plan.Rows).ToJson(new JsonWriterSettings
        {
            Indent = true,
            OutputMode = JsonOutputMode.CanonicalExtendedJson
        });
        await File.WriteAllTextAsync(plan.SourcePath, json);
        await File.WriteAllTextAsync(plan.PublishPath, json);
    }
    Console.WriteLine("Canonical Block seeds were written.");
}

static async Task RunBlockCleanupAsync(IMongoDatabase database, bool apply)
{
    var collectionNames = new[] { "blocks_draft", "blocks_published" };
    var allRows = new List<(string Collection, BsonDocument Document)>();
    foreach (var collectionName in collectionNames)
    {
        var rows = await database.GetCollection<BsonDocument>(collectionName)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
        allRows.AddRange(rows.Select(row => (collectionName, row)));
    }

    var missingLayout = allRows.Count(item => !HasDocument(item.Document, "Layout"));
    var missingAppearance = allRows.Count(item => !HasVersion(item.Document, "Appearance", 2));
    var missingResponsive = allRows.Count(item => !HasVersion(item.Document, "Responsive", 1));
    var missingAnimation = allRows.Count(item => !HasVersion(item.Document, "Animation", 1));
    var missingAuthoring = allRows.Count(item => !HasVersion(item.Document, "Authoring", 1));
    var legacyAssetRows = allRows.Where(item =>
        item.Document.Contains("ImageUrl") || item.Document.Contains("EmbedUrl") ||
        item.Document.Contains("FileUrl") || item.Document.Contains("FileType")).ToList();
    var containers = allRows.Where(item => ReadDiscriminator(item.Document) == "container").ToList();
    var legacyContainerFields = new[]
    {
        "LayoutMode", "Columns", "Gap", "OrbitRadius", "OrbitStartAngle",
        "SemicircleRadius", "SemicircleStartAngle", "SemicircleEndAngle"
    };
    var legacyContainers = containers.Where(item =>
        !HasDocument(item.Document, "ContainerLayout") ||
        legacyContainerFields.Any(item.Document.Contains) ||
        string.IsNullOrWhiteSpace(item.Document.GetValue("PresetKey", string.Empty).ToString()) ||
        string.Equals(item.Document.GetValue("PresetKey", string.Empty).ToString(), "legacy-freeform", StringComparison.Ordinal))
        .ToList();
    var duplicateStableIds = allRows
        .GroupBy(item => $"{item.Collection}:{item.Document.GetValue("StableId", string.Empty)}", StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    Console.WriteLine($"Block documents           : {allRows.Count}");
    Console.WriteLine($"Missing canonical Layout  : {missingLayout}");
    Console.WriteLine($"Appearance below v2       : {missingAppearance}");
    Console.WriteLine($"Responsive below v1       : {missingResponsive}");
    Console.WriteLine($"Animation below v1        : {missingAnimation}");
    Console.WriteLine($"Authoring below v1        : {missingAuthoring}");
    Console.WriteLine($"Legacy asset fields       : {legacyAssetRows.Count}");
    Console.WriteLine($"Container documents       : {containers.Count}");
    Console.WriteLine($"Legacy-dependent containers: {legacyContainers.Count}");
    Console.WriteLine($"Duplicate stable IDs      : {duplicateStableIds.Count}");
    Console.WriteLine("Canonicalization groups:");
    foreach (var group in allRows
                 .Where(item =>
                     !HasDocument(item.Document, "Layout") ||
                     !HasVersion(item.Document, "Appearance", 2) ||
                     !HasVersion(item.Document, "Responsive", 1) ||
                     !HasVersion(item.Document, "Animation", 1) ||
                     !HasVersion(item.Document, "Authoring", 1))
                 .GroupBy(item => $"{item.Collection}:{ReadDiscriminator(item.Document)}", StringComparer.Ordinal)
                 .OrderBy(group => group.Key))
        Console.WriteLine($"  {group.Key} | {group.Count()} record(s)");
    foreach (var item in legacyContainers)
    {
        var layout = item.Document.GetValue("ContainerLayout", new BsonDocument()).AsBsonDocument;
        Console.WriteLine(
            $"  {item.Collection}:{ReadId(item.Document)} | mode={ReadContainerValue(item.Document, layout, "Mode", "LayoutMode", "stack")} | " +
            $"columns={ReadContainerValue(item.Document, layout, "Columns", "Columns", "2")} | " +
            $"preset={item.Document.GetValue("PresetKey", "<missing>")}");
    }
    if (duplicateStableIds.Count > 0)
        throw new InvalidOperationException("Block stable IDs must be unique within each collection.");

    Console.WriteLine("Block reconciliation      : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    var updated = 0;
    foreach (var item in allRows)
    {
        var row = item.Document;
        var changed = false;
        var layout = GetOrCreateDocument(row, "Layout", CreateDefaultBlockLayout(), ref changed);

        if (!HasVersion(row, "Appearance", 2))
        {
            row["Appearance"] = CreateCanonicalAppearance(row, layout);
            changed = true;
        }
        if (!HasVersion(row, "Responsive", 1))
        {
            row["Responsive"] = new BsonDocument { ["SchemaVersion"] = 1 };
            changed = true;
        }
        if (!HasVersion(row, "Animation", 1))
        {
            row["Animation"] = new BsonDocument
            {
                ["SchemaVersion"] = 1,
                ["Effect"] = "none",
                ["Trigger"] = "enter-viewport",
                ["DurationMs"] = 500,
                ["DelayMs"] = 0,
                ["Easing"] = "ease-out",
                ["PlayOnce"] = true,
                ["StaggerMs"] = 0,
                ["ContinuousEffect"] = "none",
                ["DisableForReducedMotion"] = true
            };
            changed = true;
        }
        if (!HasVersion(row, "Authoring", 1))
        {
            row["Authoring"] = new BsonDocument
            {
                ["SchemaVersion"] = 1,
                ["ContentLocked"] = false,
                ["GeometryLocked"] = false,
                ["FullLocked"] = false
            };
            changed = true;
        }

        changed |= CanonicalizeAsset(row);
        changed |= CanonicalizeContainer(row);
        if (!changed) continue;

        await database.GetCollection<BsonDocument>(item.Collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", row["_id"]),
            row);
        updated++;
    }

    Console.WriteLine($"Canonical Block documents : {updated}");
}

static BsonDocument GetOrCreateDocument(
    BsonDocument owner,
    string name,
    BsonDocument defaultValue,
    ref bool changed)
{
    if (owner.TryGetValue(name, out var value) && value.IsBsonDocument)
        return value.AsBsonDocument;
    owner[name] = defaultValue;
    changed = true;
    return defaultValue;
}

static BsonDocument CreateDefaultBlockLayout() => new()
{
    ["Width"] = "auto",
    ["ColumnSpan"] = 12,
    ["Align"] = "stretch",
    ["Justify"] = "start",
    ["Padding"] = "none",
    ["Margin"] = "none",
    ["BackgroundColor"] = BsonNull.Value,
    ["BorderRadius"] = "none",
    ["ZIndex"] = 1,
    ["X"] = 0,
    ["Y"] = 0,
    ["W"] = 4,
    ["H"] = 2
};

static BsonDocument CreateCanonicalAppearance(BsonDocument row, BsonDocument layout)
{
    var appearance = row.TryGetValue("Appearance", out var value) && value.IsBsonDocument
        ? value.AsBsonDocument.DeepClone().AsBsonDocument
        : new BsonDocument();
    var oldSchema = ReadEnumValue(appearance.GetValue("SchemaVersion", 0));
    var appearanceColor = appearance.GetValue("BackgroundColor", BsonNull.Value);
    var layoutColor = layout.GetValue("BackgroundColor", BsonNull.Value);
    var effectiveColor = !appearanceColor.IsBsonNull && !string.IsNullOrWhiteSpace(appearanceColor.ToString())
        ? appearanceColor
        : oldSchema == 0 ? layoutColor : BsonNull.Value;
    appearance["SchemaVersion"] = 2;
    SetDefault(appearance, "BackgroundMode",
        !effectiveColor.IsBsonNull && !string.IsNullOrWhiteSpace(effectiveColor.ToString()) ? "color" : "none");
    if (!effectiveColor.IsBsonNull) appearance["BackgroundColor"] = effectiveColor;
    SetDefault(appearance, "TextColor", BsonNull.Value);
    SetDefault(appearance, "TextAlign", "inherit");
    SetDefault(appearance, "Opacity", 1d);
    SetDefault(appearance, "BorderColor", BsonNull.Value);
    SetDefault(appearance, "BorderWidth", 0);
    SetDefault(appearance, "BorderStyle", "solid");
    SetDefault(appearance, "BorderRadius", oldSchema == 0 ? layout.GetValue("BorderRadius", "none") : "none");
    SetDefault(appearance, "Shadow", "none");
    SetDefault(appearance, "Shape", "rectangle");
    SetDefault(appearance, "AspectRatio", "auto");
    SetDefault(appearance, "RotationDeg", 0d);
    SetDefault(appearance, "MediaFit", "cover");
    SetDefault(appearance, "MediaPosition", "center");
    SetDefault(appearance, "Padding", oldSchema == 0 ? layout.GetValue("Padding", "none") : "none");
    SetDefault(appearance, "Margin", oldSchema == 0 ? layout.GetValue("Margin", "none") : "none");
    SetDefault(appearance, "Decorative", false);
    SetDefault(appearance, "InheritFromContainer", false);
    return appearance;
}

static void SetDefault(BsonDocument document, string name, BsonValue value)
{
    if (!document.Contains(name) || document[name].IsBsonNull)
        document[name] = value;
}

static bool CanonicalizeAsset(BsonDocument row)
{
    var type = ReadDiscriminator(row);
    if (type is not ("image" or "video" or "file" or "card")) return false;
    var changed = false;
    var asset = row.TryGetValue("Asset", out var value) && value.IsBsonDocument
        ? value.AsBsonDocument
        : new BsonDocument();
    if (!row.Contains("Asset")) changed = true;
    var assetSchemaVersion = ReadEnumValue(asset.GetValue("SchemaVersion", 0));
    if (assetSchemaVersion < 1)
    {
        asset["SchemaVersion"] = 1;
        changed = true;
    }
    SetDefault(asset, "ResourceSource", "DirectUpload");
    var urlField = type == "video" ? "EmbedUrl" : type == "file" ? "FileUrl" : "ImageUrl";
    if (row.TryGetValue(urlField, out var url) && !url.IsBsonNull)
    {
        SetDefault(asset, "Url", url);
        row.Remove(urlField);
        changed = true;
    }
    if (type == "file" && row.TryGetValue("FileType", out var contentType) && !contentType.IsBsonNull)
    {
        SetDefault(asset, "ContentType", contentType);
        row.Remove("FileType");
        changed = true;
    }
    row["Asset"] = asset;
    return changed;
}

static bool CanonicalizeContainer(BsonDocument row)
{
    if (ReadDiscriminator(row) != "container") return false;
    var changed = false;
    var layout = GetOrCreateDocument(row, "ContainerLayout", new BsonDocument(), ref changed);
    var mappings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["LayoutMode"] = "Mode",
        ["Columns"] = "Columns",
        ["Gap"] = "Gap",
        ["OrbitRadius"] = "OrbitRadius",
        ["OrbitStartAngle"] = "OrbitStartAngle",
        ["SemicircleRadius"] = "SemicircleRadius",
        ["SemicircleStartAngle"] = "SemicircleStartAngle",
        ["SemicircleEndAngle"] = "SemicircleEndAngle"
    };
    foreach (var mapping in mappings)
    {
        if (!row.TryGetValue(mapping.Key, out var value)) continue;
        SetDefault(layout, mapping.Value, value);
        row.Remove(mapping.Key);
        changed = true;
    }
    var mode = layout.GetValue("Mode", "stack").ToString() ?? "stack";
    var preset = mode switch
    {
        "row" => "row-six",
        "grid" => "grid-six",
        "split" => "split-two",
        "orbit" => "orbit-eight",
        "semicircle" => "semicircle-six",
        "freeform" => "advanced-freeform-ten",
        _ => "stack-basic"
    };
    var currentPreset = row.GetValue("PresetKey", string.Empty).ToString();
    if (string.IsNullOrWhiteSpace(currentPreset) || currentPreset == "legacy-freeform")
    {
        row["PresetKey"] = preset;
        layout["SchemaVersion"] = 3;
        SetDefault(layout, "Purpose", mode is "stack" or "row" or "grid" ? "collection" : "composition");
        SetDefault(layout, "MobileMode", mode is "orbit" or "semicircle" ? "compact-preserve" : "stack");
        changed = true;
    }
    return changed;
}

static bool HasDocument(BsonDocument document, string name) =>
    document.TryGetValue(name, out var value) && value.IsBsonDocument;

static bool HasVersion(BsonDocument document, string name, int minimum) =>
    document.TryGetValue(name, out var value) && value.IsBsonDocument &&
    ReadEnumValue(value.AsBsonDocument.GetValue("SchemaVersion", 0)) >= minimum;

static string ReadDiscriminator(BsonDocument document)
{
    var value = document.GetValue("_t", string.Empty);
    if (value.IsString) return value.AsString;
    return value.IsBsonArray && value.AsBsonArray.Count > 0
        ? value.AsBsonArray[^1].ToString() ?? string.Empty
        : string.Empty;
}

static string ReadContainerValue(
    BsonDocument document,
    BsonDocument layout,
    string canonicalName,
    string legacyName,
    string fallback)
{
    var value = layout.GetValue(canonicalName, BsonNull.Value);
    if (!value.IsBsonNull) return value.ToString() ?? fallback;
    value = document.GetValue(legacyName, BsonNull.Value);
    return value.IsBsonNull ? fallback : value.ToString() ?? fallback;
}

static async Task RunFormSeedCleanupAsync(IMongoDatabase database, bool apply)
{
    var sourcePath = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        "Tool", "Tool For Demo Import", "demo-seed", "collections", "form_definitions.json"));
    var publishPath = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        "Tool", "Tool For Demo Import", "publish", "demo-seed", "collections", "form_definitions.json"));
    if (!File.Exists(sourcePath))
        throw new FileNotFoundException("The canonical demo Form Definition seed was not found.", sourcePath);

    var seedValue = BsonSerializer.Deserialize<BsonValue>(await File.ReadAllTextAsync(sourcePath));
    if (!seedValue.IsBsonArray)
        throw new InvalidOperationException("The Form Definition seed must be a JSON array.");
    var seedRows = seedValue.AsBsonArray.Select(value => value.AsBsonDocument).ToList();
    var seedIds = seedRows.Select(ReadId).ToHashSet(StringComparer.Ordinal);
    var liveRows = await database.GetCollection<BsonDocument>("form_definitions")
        .Find(Builders<BsonDocument>.Filter.In("_id", seedIds.Select(ObjectId.Parse)))
        .Sort(Builders<BsonDocument>.Sort.Ascending("Key"))
        .ToListAsync();
    var validLiveRows = liveRows.Where(row =>
        row.TryGetValue("Design", out var designValue) &&
        designValue.IsBsonDocument &&
        ReadEnumValue(designValue.AsBsonDocument.GetValue("SchemaVersion", 0)) >= 2 &&
        designValue.AsBsonDocument.TryGetValue("V2", out var v2) &&
        v2.IsBsonDocument).ToList();

    Console.WriteLine($"Seed definitions          : {seedRows.Count}");
    Console.WriteLine($"Matching live definitions : {liveRows.Count}");
    Console.WriteLine($"Valid schema-v2 sources   : {validLiveRows.Count}");
    if (validLiveRows.Count != seedRows.Count)
        throw new InvalidOperationException("Every seeded Form Definition must have a validated live schema-v2 source.");

    Console.WriteLine("Form seed reconciliation : PASSED");
    if (!apply)
    {
        Console.WriteLine("No seed files were changed.");
        return;
    }

    foreach (var row in validLiveRows)
    {
        row.Remove("DisplayMode");
        row.Remove("Layout");
    }
    var json = new BsonArray(validLiveRows).ToJson(new JsonWriterSettings
    {
        Indent = true,
        OutputMode = JsonOutputMode.CanonicalExtendedJson
    });
    await File.WriteAllTextAsync(sourcePath, json);
    await File.WriteAllTextAsync(publishPath, json);
    Console.WriteLine("Canonical schema-v2 Form Definition seeds were written.");
}

static async Task RunFormCleanupAsync(IMongoDatabase database, bool apply)
{
    var definitions = database.GetCollection<BsonDocument>("form_definitions");
    var submissions = database.GetCollection<BsonDocument>("form_submissions");
    var rows = await definitions.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var invalid = rows.Where(row =>
    {
        if (!row.TryGetValue("Design", out var designValue) || !designValue.IsBsonDocument) return true;
        var design = designValue.AsBsonDocument;
        return ReadEnumValue(design.GetValue("SchemaVersion", 0)) < 2 ||
               !design.TryGetValue("V2", out var v2) || !v2.IsBsonDocument;
    }).Select(ReadId).ToList();
    var legacyFields = rows.Count(row => row.Contains("DisplayMode") || row.Contains("Layout"));
    var missingThemeMarker = rows.Count(row =>
        !row.TryGetValue("Design", out var designValue) ||
        !designValue.IsBsonDocument ||
        !designValue.AsBsonDocument.Contains("UseThemeDefaults"));
    var legacyFormBlocks = 0L;
    foreach (var collectionName in new[] { "blocks_draft", "blocks_published" })
    {
        var blocks = database.GetCollection<BsonDocument>(collectionName);
        var formBlock = Builders<BsonDocument>.Filter.Eq("_t", "form") |
                        Builders<BsonDocument>.Filter.AnyEq("_t", "form");
        var legacyBlock = formBlock & Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("DesignSchemaVersion", false),
            Builders<BsonDocument>.Filter.Lt("DesignSchemaVersion", 2),
            Builders<BsonDocument>.Filter.Exists("Fields", true),
            Builders<BsonDocument>.Filter.Exists("SubmitButtonLabel", true));
        legacyFormBlocks += await blocks.CountDocumentsAsync(legacyBlock);
    }
    var submissionCount = await submissions.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
    var migrationRecordCount = await database.GetCollection<BsonDocument>("form_design_migration_records")
        .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
    var migrationLeaseCount = await database.GetCollection<BsonDocument>("form_design_migration_leases")
        .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    Console.WriteLine($"Form definitions          : {rows.Count}");
    Console.WriteLine($"Schema-v2 definitions     : {rows.Count - invalid.Count}");
    Console.WriteLine($"Invalid definitions       : {invalid.Count}");
    Console.WriteLine($"Legacy top-level fields   : {legacyFields}");
    Console.WriteLine($"Missing theme markers     : {missingThemeMarker}");
    Console.WriteLine($"Outdated Form Blocks      : {legacyFormBlocks}");
    Console.WriteLine($"Form submissions          : {submissionCount}");
    Console.WriteLine($"Migration records         : {migrationRecordCount}");
    Console.WriteLine($"Migration leases          : {migrationLeaseCount}");
    if (invalid.Count > 0 || missingThemeMarker > 0 || legacyFormBlocks > 0)
        throw new InvalidOperationException(
            $"Form cleanup is blocked. Invalid definitions: {string.Join(", ", invalid)}; " +
            $"missing theme markers: {missingThemeMarker}; outdated Form Blocks: {legacyFormBlocks}.");

    Console.WriteLine("Form reconciliation      : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    await definitions.UpdateManyAsync(
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("DisplayMode", true),
            Builders<BsonDocument>.Filter.Exists("Layout", true)),
        Builders<BsonDocument>.Update.Unset("DisplayMode").Unset("Layout"));
    await DropCollectionIfPresentAsync(database, "form_design_migration_records");
    await DropCollectionIfPresentAsync(database, "form_design_migration_leases");
    Console.WriteLine("Legacy Form fields and completed migration scaffolding were removed.");
}

static async Task RunContentCleanupAsync(IMongoDatabase database, bool apply)
{
    var users = database.GetCollection<BsonDocument>("admin_users");
    var roles = database.GetCollection<BsonDocument>("admin_roles");
    var draft = database.GetCollection<BsonDocument>("content_draft");
    var published = database.GetCollection<BsonDocument>("content_published");

    var userRows = await users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var roleRows = await roles.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var protectedRoleIds = roleRows
        .Where(role => role.GetValue("IsProtected", false).ToBoolean())
        .Select(ReadId)
        .ToHashSet(StringComparer.Ordinal);
    var protectedAdministrators = userRows
        .Where(user => protectedRoleIds.Contains(ReadReference(user.GetValue("RoleId", BsonNull.Value))))
        .ToList();
    if (protectedAdministrators.Count != 1)
        throw new InvalidOperationException("Content cleanup requires exactly one protected administrator account.");
    var protectedAdministrator = protectedAdministrators[0];
    var legacyDemoAuthorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "6a05676a4b5370e52b805d45"
    };
    var usersById = userRows.ToDictionary(ReadId, StringComparer.Ordinal);
    var usersByEmail = userRows
        .Where(user => user.TryGetValue("Email", out var email) && email.IsString && !string.IsNullOrWhiteSpace(email.AsString))
        .GroupBy(user => user["Email"].AsString.Trim(), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    var collections = new[] { draft, published };
    var legacyStatusCount = 0;
    var repairableAuthors = 0;
    var invalidAuthors = new List<string>();
    var missingAuthorNames = 0;
    var plans = new List<(IMongoCollection<BsonDocument> Collection, BsonValue Id, UpdateDefinition<BsonDocument> Update)>();

    foreach (var collection in collections)
    {
        var rows = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        foreach (var row in rows)
        {
            var updates = new List<UpdateDefinition<BsonDocument>>();
            var status = ReadEnumValue(row.GetValue("Status", 0));
            if (status is 2 or 4)
            {
                legacyStatusCount++;
                if (status == 2)
                {
                    updates.Add(Builders<BsonDocument>.Update.Set("Status", 0));
                    updates.Add(Builders<BsonDocument>.Update.Set("ReviewStatus", 1));
                }
                else
                {
                    updates.Add(Builders<BsonDocument>.Update.Set("Status", 5));
                    updates.Add(Builders<BsonDocument>.Update.Set("Visible", false));
                }
            }

            var authorId = row.GetValue("AuthorId", string.Empty).ToString() ?? string.Empty;
            BsonDocument? author = null;
            if (!string.IsNullOrWhiteSpace(authorId) && usersById.TryGetValue(authorId, out var canonicalAuthor))
            {
                author = canonicalAuthor;
            }
            else if (!string.IsNullOrWhiteSpace(authorId) && usersByEmail.TryGetValue(authorId, out var emailAuthor))
            {
                author = emailAuthor;
                repairableAuthors++;
                updates.Add(Builders<BsonDocument>.Update.Set("AuthorId", ReadId(emailAuthor)));
            }
            else if (legacyDemoAuthorIds.Contains(authorId))
            {
                author = protectedAdministrator;
                repairableAuthors++;
                updates.Add(Builders<BsonDocument>.Update.Set("AuthorId", ReadId(protectedAdministrator)));
            }
            else
            {
                invalidAuthors.Add($"{collection.CollectionNamespace.CollectionName}:{ReadId(row)}:{authorId}");
            }

            var authorName = row.GetValue("AuthorName", string.Empty).ToString();
            if (string.IsNullOrWhiteSpace(authorName) && author is not null)
            {
                missingAuthorNames++;
                var fullName = author.GetValue("FullName", "Unknown author").ToString();
                updates.Add(Builders<BsonDocument>.Update.Set(
                    "AuthorName",
                    string.IsNullOrWhiteSpace(fullName) ? "Unknown author" : fullName.Trim()));
            }

            if (updates.Count > 0)
                plans.Add((collection, row["_id"], Builders<BsonDocument>.Update.Combine(updates)));
        }
    }

    Console.WriteLine($"Administrator identities : {userRows.Count}");
    Console.WriteLine($"Legacy content statuses  : {legacyStatusCount}");
    Console.WriteLine($"Email authors repairable : {repairableAuthors}");
    Console.WriteLine($"Missing author names     : {missingAuthorNames}");
    Console.WriteLine($"Unresolved authors       : {invalidAuthors.Count}");
    if (invalidAuthors.Count > 0)
    {
        Console.WriteLine("Canonical administrators:");
        foreach (var user in userRows)
            Console.WriteLine($"  {ReadId(user)} | {user.GetValue("FullName", string.Empty)} | {user.GetValue("Email", string.Empty)}");
        Console.WriteLine("Unresolved author groups:");
        foreach (var group in invalidAuthors
                     .Select(value => value[(value.LastIndexOf(':') + 1)..])
                     .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count()))
            Console.WriteLine($"  {group.Key} | {group.Count()} record(s)");
        throw new InvalidOperationException(
            "Content ownership reconciliation failed: " + string.Join(", ", invalidAuthors.Take(10)));
    }

    Console.WriteLine("Content reconciliation   : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    foreach (var plan in plans)
    {
        await plan.Collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", plan.Id),
            plan.Update);
    }

    Console.WriteLine($"Content records updated  : {plans.Count}");
}

static async Task RunRoleCleanupAsync(IMongoDatabase database, bool apply)
{
    var roles = database.GetCollection<BsonDocument>("admin_roles");
    var users = database.GetCollection<BsonDocument>("admin_users");
    var sessions = database.GetCollection<BsonDocument>("admin_sessions");
    var rememberedDevices = database.GetCollection<BsonDocument>("admin_remembered_devices");

    var roleRows = await roles.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var userRows = await users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var roleIds = roleRows.Select(ReadId).ToHashSet(StringComparer.Ordinal);
    var invalidUsers = userRows.Where(user =>
    {
        if (!user.TryGetValue("RoleId", out var roleId) || roleId.IsBsonNull) return true;
        var value = roleId.BsonType == BsonType.ObjectId ? roleId.AsObjectId.ToString() : roleId.ToString();
        return string.IsNullOrWhiteSpace(value) || !roleIds.Contains(value);
    }).Select(ReadId).ToList();
    var legacyUsers = userRows.Where(user =>
        user.Contains("role") || user.Contains("Role") || user.Contains("Permissions")).ToList();

    Console.WriteLine($"Canonical roles         : {roleRows.Count}");
    Console.WriteLine($"Administrator accounts  : {userRows.Count}");
    Console.WriteLine($"Invalid RoleId accounts  : {invalidUsers.Count}");
    Console.WriteLine($"Legacy-field accounts    : {legacyUsers.Count}");

    if (roleRows.Count == 0 || invalidUsers.Count > 0)
        throw new InvalidOperationException("Every administrator must reference a valid canonical role before cleanup.");

    Console.WriteLine("Role reconciliation     : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    if (legacyUsers.Count > 0)
    {
        var affectedIds = legacyUsers.Select(user => user["_id"]).ToArray();
        var affectedFilter = Builders<BsonDocument>.Filter.In("_id", affectedIds);
        await users.UpdateManyAsync(
            affectedFilter,
            Builders<BsonDocument>.Update
                .Unset("role")
                .Unset("Role")
                .Unset("Permissions")
                .Inc("TokenVersion", 1)
                .Set("UpdatedAt", DateTime.UtcNow));

        await sessions.UpdateManyAsync(
            Builders<BsonDocument>.Filter.In("AdminId", affectedIds.Select(value => value.ToString())) &
            Builders<BsonDocument>.Filter.Ne("IsRevoked", true),
            Builders<BsonDocument>.Update
                .Set("IsRevoked", true)
                .Set("RevokedAt", DateTime.UtcNow)
                .Set("RevokedById", "fallback-cleanup")
                .Set("RevokeReason", "RoleChanged"));
        await rememberedDevices.UpdateManyAsync(
            Builders<BsonDocument>.Filter.In("AdminId", affectedIds.Select(value => value.ToString())) &
            Builders<BsonDocument>.Filter.Ne("IsRevoked", true),
            Builders<BsonDocument>.Update
                .Set("IsRevoked", true)
                .Set("RevokedAt", DateTime.UtcNow)
                .Set("RevokedById", "fallback-cleanup")
                .Set("RevokeReason", "AdminRevoked"));
    }

    var remaining = await users.CountDocumentsAsync(
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("role", true),
            Builders<BsonDocument>.Filter.Exists("Role", true),
            Builders<BsonDocument>.Filter.Exists("Permissions", true)));
    if (remaining != 0)
        throw new InvalidOperationException($"Role cleanup verification failed for {remaining} account(s).");

    Console.WriteLine("Legacy role fields were removed and affected authentication credentials were revoked.");
}

static async Task RunLogCleanupAsync(IMongoDatabase database, bool apply)
{
    var audit = database.GetCollection<BsonDocument>("admin_audit_events");
    var login = database.GetCollection<BsonDocument>("admin_login_activity_events");
    var legacyCollectionsExist = await CollectionExistsAsync(database, "admin_audit_logs") ||
                                 await CollectionExistsAsync(database, "admin_login_activity") ||
                                 await CollectionExistsAsync(database, "admin_log_archive_checkpoints");
    if (!legacyCollectionsExist)
    {
        var staleLinks = await audit.CountDocumentsAsync(Builders<BsonDocument>.Filter.Exists("LegacySourceId", true)) +
                         await login.CountDocumentsAsync(Builders<BsonDocument>.Filter.Exists("LegacySourceId", true));
        Console.WriteLine("Legacy log collections    : 0");
        Console.WriteLine($"Stale migration links     : {staleLinks}");
        if (staleLinks > 0)
            throw new InvalidOperationException("Canonical log collections still contain migration-only source links.");
        Console.WriteLine("Log reconciliation      : PASSED");
        Console.WriteLine("No database changes were made.");
        return;
    }

    var legacyAudit = database.GetCollection<BsonDocument>("admin_audit_logs");
    var legacyLogin = database.GetCollection<BsonDocument>("admin_login_activity");
    var checkpoints = database.GetCollection<BsonDocument>("admin_log_archive_checkpoints");

    var legacyAuditRows = await legacyAudit.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var legacyLoginRows = await legacyLogin.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    var migratedAuditRows = await audit.Find(Builders<BsonDocument>.Filter.Exists("LegacySourceId", true)).ToListAsync();
    var migratedLoginRows = await login.Find(Builders<BsonDocument>.Filter.Exists("LegacySourceId", true)).ToListAsync();
    var checkpointRows = await checkpoints.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

    var excludedAuditActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "login-success",
        "login-denied",
        "logout"
    };
    var expectedAuditIds = legacyAuditRows
        .Where(row => !excludedAuditActions.Contains(row.GetValue("Action", string.Empty).AsString))
        .Select(ReadId)
        .ToHashSet(StringComparer.Ordinal);
    var migratedAuditIds = migratedAuditRows
        .Select(row => row.GetValue("LegacySourceId", string.Empty).AsString)
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.Ordinal);
    var legacyLoginIds = legacyLoginRows.Select(ReadId).ToHashSet(StringComparer.Ordinal);
    var migratedLoginIds = migratedLoginRows
        .Select(row => row.GetValue("LegacySourceId", string.Empty).AsString)
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.Ordinal);
    var completedSources = checkpointRows
        .Where(row => row.GetValue("Completed", false).ToBoolean())
        .Select(row => row.GetValue("_id", string.Empty).ToString())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var missingAudit = expectedAuditIds.Except(migratedAuditIds).ToList();
    var missingLogin = legacyLoginIds.Except(migratedLoginIds).ToList();
    var unexpectedAudit = migratedAuditIds.Except(expectedAuditIds).ToList();
    var unexpectedLogin = migratedLoginIds.Except(legacyLoginIds).ToList();

    Console.WriteLine($"Legacy audit rows       : {legacyAuditRows.Count}");
    Console.WriteLine($"Expected audit migrated : {expectedAuditIds.Count}");
    Console.WriteLine($"Audit migration links   : {migratedAuditIds.Count}");
    Console.WriteLine($"Legacy login rows       : {legacyLoginRows.Count}");
    Console.WriteLine($"Login migration links   : {migratedLoginIds.Count}");
    Console.WriteLine($"Completed checkpoints   : {completedSources.Count}");

    if (missingAudit.Count > 0 || missingLogin.Count > 0 || unexpectedAudit.Count > 0 || unexpectedLogin.Count > 0)
    {
        throw new InvalidOperationException(
            $"Log reconciliation failed. Missing audit: {missingAudit.Count}; missing login: {missingLogin.Count}; " +
            $"unexpected audit: {unexpectedAudit.Count}; unexpected login: {unexpectedLogin.Count}.");
    }

    if (!completedSources.Contains("admin_audit_logs") ||
        !completedSources.Contains("admin_login_activity"))
    {
        throw new InvalidOperationException("Both legacy log migration checkpoints must be complete.");
    }

    Console.WriteLine("Log reconciliation      : PASSED");
    if (!apply)
    {
        Console.WriteLine("No database changes were made.");
        return;
    }

    await audit.UpdateManyAsync(
        Builders<BsonDocument>.Filter.Exists("LegacySourceId", true),
        Builders<BsonDocument>.Update.Unset("LegacySourceId"));
    await login.UpdateManyAsync(
        Builders<BsonDocument>.Filter.Exists("LegacySourceId", true),
        Builders<BsonDocument>.Update.Unset("LegacySourceId"));
    await DropIndexIfPresentAsync(audit, "ux_admin_audit_legacy");
    await DropIndexIfPresentAsync(login, "ux_admin_login_legacy");
    await database.DropCollectionAsync("admin_audit_logs");
    await database.DropCollectionAsync("admin_login_activity");
    await database.DropCollectionAsync("admin_log_archive_checkpoints");

    Console.WriteLine("Legacy log collections and migration fields were removed.");
}

static async Task DropIndexIfPresentAsync(IMongoCollection<BsonDocument> collection, string name)
{
    using var cursor = await collection.Indexes.ListAsync();
    var exists = (await cursor.ToListAsync()).Any(index =>
        string.Equals(index.GetValue("name", string.Empty).AsString, name, StringComparison.Ordinal));
    if (exists) await collection.Indexes.DropOneAsync(name);
}

static async Task DropCollectionIfPresentAsync(IMongoDatabase database, string name)
{
    using var cursor = await database.ListCollectionNamesAsync(new ListCollectionNamesOptions
    {
        Filter = new BsonDocument("name", name)
    });
    if (await cursor.AnyAsync())
        await database.DropCollectionAsync(name);
}

static async Task<bool> CollectionExistsAsync(IMongoDatabase database, string name)
{
    using var cursor = await database.ListCollectionNamesAsync(new ListCollectionNamesOptions
    {
        Filter = new BsonDocument("name", name)
    });
    return await cursor.AnyAsync();
}

static string ReadId(BsonDocument document)
{
    var value = document.GetValue("_id", BsonNull.Value);
    return value.BsonType == BsonType.ObjectId ? value.AsObjectId.ToString() : value.ToString() ?? string.Empty;
}

static int ReadEnumValue(BsonValue value)
{
    if (value.IsInt32) return value.AsInt32;
    if (value.IsInt64) return checked((int)value.AsInt64);
    if (value.IsString)
    {
        return value.AsString.Trim().ToLowerInvariant() switch
        {
            "draft" => 0,
            "submitted" => 1,
            "rejected" => 2,
            "published" => 3,
            "archived" => 4,
            "deleted" => 5,
            _ => -1
        };
    }

    return -1;
}

static string ReadReference(BsonValue value) => value.BsonType switch
{
    BsonType.ObjectId => value.AsObjectId.ToString(),
    BsonType.String => value.AsString,
    _ => string.Empty
};

static async Task<MongoConfiguration> LoadConfigurationAsync(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException("MongoDB configuration file was not found.", path);

    await using var stream = File.OpenRead(path);
    using var document = await JsonDocument.ParseAsync(stream);
    var mongo = document.RootElement.GetProperty("MongoDb");
    var connectionString = mongo.GetProperty("ConnectionString").GetString();
    var databaseName = mongo.GetProperty("DatabaseName").GetString();
    if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("MongoDb connection settings are incomplete.");
    if (databaseName is "admin" or "config" or "local")
        throw new InvalidOperationException("Refusing to operate on a reserved MongoDB database.");
    return new MongoConfiguration(connectionString, databaseName);
}

internal sealed record MongoConfiguration(string ConnectionString, string DatabaseName);

internal sealed record CleanupOptions(string Phase, string ConfigurationPath, bool Apply)
{
    public static CleanupOptions Parse(string[] args)
    {
        var phase = ReadValue(args, "--phase") ?? throw new InvalidOperationException("--phase is required.");
        var config = ReadValue(args, "--config") ?? Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AdminSite-API", "appsettings.json"));
        var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
        return new CleanupOptions(phase.Trim().ToLowerInvariant(), Path.GetFullPath(config), apply);
    }

    private static string? ReadValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
