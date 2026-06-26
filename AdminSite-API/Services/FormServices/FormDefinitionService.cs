using System.Text.RegularExpressions;
using Contracts.Forms;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.FormServices;

public sealed class FormDefinitionService
{
    private static readonly Regex FormKeyRegex = new(
        "^[a-z][a-z0-9-]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<FormDefinition> _definitions;

    public FormDefinitionService(IMongoDatabase database)
    {
        _database = database;
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

    public async Task<List<FormDefinition>> GetAllAsync() =>
        await _definitions
            .Find(_ => true)
            .SortBy(definition => definition.Key)
            .ToListAsync();

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
        var existing = string.IsNullOrWhiteSpace(id)
            ? await _definitions.Find(definition => definition.Key == key).FirstOrDefaultAsync()
            : await GetByIdAsync(id);

        var definition = existing ?? new FormDefinition
        {
            Id = ObjectId.GenerateNewId().ToString(),
            CreatedAt = now
        };

        definition.Key = existing?.Key ?? key;
        definition.Name = CleanTextMap(request.Name);
        definition.Introduction = CleanTextMap(request.Introduction);
        definition.SubmitButtonLabel = CleanTextMap(request.SubmitButtonLabel);
        definition.DisplayMode = request.DisplayMode;
        definition.Layout = request.Layout;
        definition.Active = request.Active;
        definition.Fields = request.Fields
            .OrderBy(field => field.Order)
            .Select((field, index) => new FormDefinitionField
            {
                Key = CleanFieldKey(field.Key, index),
                Type = string.IsNullOrWhiteSpace(field.Type) ? "text" : field.Type.Trim().ToLowerInvariant(),
                Label = CleanTextMap(field.Label),
                Placeholder = CleanTextMap(field.Placeholder),
                Required = field.Required,
                MinLength = Math.Clamp(field.MinLength, 0, 2_000),
                MaxLength = Math.Clamp(field.MaxLength <= 0 ? 500 : field.MaxLength, 1, 2_000),
                Order = index,
                Options = field.Options
                    .OrderBy(option => option.Order)
                    .Select((option, optionIndex) => new FormDefinitionFieldOption
                    {
                        Value = option.Value.Trim(),
                        Label = CleanTextMap(option.Label),
                        Order = optionIndex
                    })
                    .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                    .ToList()
            })
            .ToList();
        definition.UpdatedAt = now;

        await _definitions.ReplaceOneAsync(
            item => item.Id == definition.Id,
            definition,
            new ReplaceOptions { IsUpsert = true });

        return definition;
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

        var ordered = items
            .OrderBy(item => item.Area == "FormBlock" ? 0 : item.Area == "Block" ? 1 : item.Area == "Section" ? 2 : 3)
            .ThenBy(item => item.Source)
            .ThenBy(item => item.Location)
            .ToList();

        return new FormDefinitionUsageResponse
        {
            FormDefinitionId = id,
            TotalCount = ordered.Count,
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

    private async Task<bool> HasReferenceAsync(string collectionName, FilterDefinition<BsonDocument> filter) =>
        await _database.GetCollection<BsonDocument>(collectionName).Find(filter).Limit(1).AnyAsync();
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
    }

    public static string? NormalizeKey(string? key)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(normalized) && FormKeyRegex.IsMatch(normalized)
            ? normalized
            : null;
    }

    public static FormDefinitionUpsertRequest ToRequest(FormDefinition definition) => new()
    {
        Key = definition.Key,
        Name = new(definition.Name),
        Introduction = new(definition.Introduction),
        SubmitButtonLabel = new(definition.SubmitButtonLabel),
        DisplayMode = definition.DisplayMode,
        Layout = definition.Layout,
        Active = definition.Active,
        Fields = definition.Fields
            .OrderBy(field => field.Order)
            .Select(field => new FormFieldDefinitionDto
            {
                Key = field.Key,
                Type = field.Type,
                Label = new(field.Label),
                Placeholder = new(field.Placeholder),
                Required = field.Required,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                Order = field.Order,
                Options = field.Options
                    .OrderBy(option => option.Order)
                    .Select(option => new FormFieldOptionDto
                    {
                        Value = option.Value,
                        Label = new(option.Label),
                        Order = option.Order
                    })
                    .ToList()
            })
            .ToList()
    };

    public static FormDefinitionResponse MapPublic(FormDefinition definition) => new()
    {
        Id = definition.Id,
        Key = definition.Key,
        Name = new(definition.Name),
        Introduction = new(definition.Introduction),
        SubmitButtonLabel = new(definition.SubmitButtonLabel),
        DisplayMode = definition.DisplayMode,
        Layout = definition.Layout,
        Active = definition.Active,
        Fields = definition.Fields
            .OrderBy(field => field.Order)
            .Select(field => new FormFieldDefinitionDto
            {
                Key = field.Key,
                Type = field.Type,
                Label = new(field.Label),
                Placeholder = new(field.Placeholder),
                Required = field.Required,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                Order = field.Order,
                Options = field.Options
                    .OrderBy(option => option.Order)
                    .Select(option => new FormFieldOptionDto
                    {
                        Value = option.Value,
                        Label = new(option.Label),
                        Order = option.Order
                    })
                    .ToList()
            })
            .ToList(),
        CreatedAt = definition.CreatedAt,
        UpdatedAt = definition.UpdatedAt
    };

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
            Name = new() { ["en"] = "Get a Quote", ["vi"] = "Nháº­n bÃ¡o giÃ¡" },
            Introduction = new()
            {
                ["en"] = "Fill out the form below and our sales team will contact you shortly.",
                ["vi"] = "Äiá»n thÃ´ng tin bÃªn dÆ°á»›i vÃ  Ä‘á»™i ngÅ© tÆ° váº¥n sáº½ liÃªn há»‡ vá»›i báº¡n sá»›m."
            },
            SubmitButtonLabel = new() { ["en"] = "Submit Request", ["vi"] = "Gá»­i yÃªu cáº§u" },
            DisplayMode = FormDisplayMode.Modal,
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
            Name = new() { ["en"] = "Talk to an Expert", ["vi"] = "Trao Ä‘á»•i vá»›i chuyÃªn gia" },
            Introduction = new()
            {
                ["en"] = "Our specialists are here to answer your questions and help you find the best solution.",
                ["vi"] = "ChuyÃªn gia cá»§a chÃºng tÃ´i sáº½ há»— trá»£ cÃ¢u há»i vÃ  Ä‘á» xuáº¥t giáº£i phÃ¡p phÃ¹ há»£p."
            },
            SubmitButtonLabel = new() { ["en"] = "Submit", ["vi"] = "Gá»­i" },
            DisplayMode = FormDisplayMode.Modal,
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
    }

    private static FormDefinitionField Field(
        string key,
        string type,
        string label,
        bool required,
        int min,
        int max,
        int order,
        List<FormDefinitionFieldOption>? options = null) => new()
        {
            Key = key,
            Type = type,
            Label = new() { ["en"] = label },
            Placeholder = new() { ["en"] = label },
            Required = required,
            MinLength = min,
            MaxLength = max,
            Order = order,
            Options = options ?? new()
        };

    private static FormDefinitionFieldOption Option(string value, string label, int order) => new()
    {
        Value = value,
        Label = new() { ["en"] = label },
        Order = order
    };

}
