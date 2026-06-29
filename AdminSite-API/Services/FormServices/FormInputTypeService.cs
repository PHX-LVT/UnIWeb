using Contracts.Forms;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.FormServices;

public sealed class FormInputTypeService
{
    private readonly IMongoCollection<FormInputTypeDefinition> _types;

    public FormInputTypeService(IMongoDatabase database)
    {
        _types = database.GetCollection<FormInputTypeDefinition>("form_input_types");
    }

    public async Task EnsureDefaultsAsync()
    {
        var existing = await _types.Find(_ => true).ToListAsync();
        var existingTypes = existing
            .Select(type => FormInputTypeCatalog.NormalizeType(type.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var inserts = FormInputTypeCatalog.BuiltInTypes
            .Select((capability, index) => (capability, index))
            .Where(item => !existingTypes.Contains(item.capability.Type))
            .Select(item => CreateDefault(item.capability, item.index, now))
            .ToList();

        if (inserts.Count > 0)
            await _types.InsertManyAsync(inserts);
    }

    public async Task<List<FormInputTypeResponse>> GetAllAsync()
    {
        await EnsureDefaultsAsync();
        var types = await _types
            .Find(_ => true)
            .SortBy(type => type.Order)
            .ThenBy(type => type.Type)
            .ToListAsync();
        return types.Select(Map).ToList();
    }

    public async Task<IReadOnlyDictionary<string, FormInputTypeCapability>> GetCapabilityLookupAsync()
    {
        await EnsureDefaultsAsync();
        var types = await _types.Find(_ => true).ToListAsync();
        return types
            .Select(ToCapability)
            .ToDictionary(
                capability => capability.Type,
                capability => capability,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>> GetActiveTypeSetAsync()
    {
        await EnsureDefaultsAsync();
        var activeTypes = await _types
            .Find(type => type.Active)
            .Project(type => type.Type)
            .ToListAsync();
        return activeTypes
            .Select(FormInputTypeCatalog.NormalizeType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<FormInputTypeResponse?> UpdateAsync(string type, FormInputTypeUpdateRequest request)
    {
        await EnsureDefaultsAsync();
        var normalizedType = FormInputTypeCatalog.NormalizeType(type);
        var existing = await _types.Find(item => item.Type == normalizedType).FirstOrDefaultAsync();
        if (existing is null)
            return null;

        var now = DateTime.UtcNow;
        var rendererCapability = FormInputTypeCatalog.Get(existing.Type);
        existing.Name = CleanTextMap(request.Name);
        existing.Active = request.Active;
        existing.SupportsMaxCharacters = rendererCapability.SupportsMaxCharacters && request.SupportsMaxCharacters;
        existing.SupportsOptions = rendererCapability.SupportsOptions && request.SupportsOptions;
        existing.SupportsInputBoxSize = rendererCapability.SupportsInputBoxSize && request.SupportsInputBoxSize;
        existing.DefaultMaxCharacters = existing.SupportsMaxCharacters
            ? Math.Clamp(request.DefaultMaxCharacters <= 0
                ? FormInputTypeCatalog.Get(existing.Type).DefaultMaxCharacters
                : request.DefaultMaxCharacters, 1, FormInputTypeCatalog.MaxCharactersLimit)
            : 0;
        existing.DefaultInputBoxSize = existing.SupportsInputBoxSize
            ? Math.Clamp(request.DefaultInputBoxSize <= 0
                ? FormInputTypeCatalog.Get(existing.Type).DefaultInputBoxSize
                : request.DefaultInputBoxSize, FormInputTypeCatalog.MinInputBoxSize, FormInputTypeCatalog.MaxInputBoxSize)
            : 1;
        existing.UpdatedAt = now;

        await _types.ReplaceOneAsync(item => item.Id == existing.Id, existing);
        return Map(existing);
    }

    public static FormInputTypeCapability Capability(
        string? type,
        IReadOnlyDictionary<string, FormInputTypeCapability>? configuredTypes)
    {
        var normalizedType = FormInputTypeCatalog.NormalizeType(type);
        return configuredTypes is not null && configuredTypes.TryGetValue(normalizedType, out var capability)
            ? capability
            : FormInputTypeCatalog.Get(normalizedType);
    }

    public static bool IsSupported(
        string? type,
        IReadOnlyDictionary<string, FormInputTypeCapability>? configuredTypes)
    {
        var normalizedType = FormInputTypeCatalog.NormalizeType(type);
        return configuredTypes is not null
            ? configuredTypes.ContainsKey(normalizedType)
            : FormInputTypeCatalog.IsSupported(normalizedType);
    }

    private static FormInputTypeDefinition CreateDefault(FormInputTypeCapability capability, int index, DateTime now) => new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        Type = capability.Type,
        LabelKey = capability.LabelKey,
        Name = new() { ["en"] = DefaultDisplayName(capability) },
        Active = true,
        SupportsMaxCharacters = capability.SupportsMaxCharacters,
        SupportsOptions = capability.SupportsOptions,
        SupportsInputBoxSize = capability.SupportsInputBoxSize,
        UsesMultilineInput = capability.UsesMultilineInput,
        DefaultMaxCharacters = capability.DefaultMaxCharacters,
        DefaultInputBoxSize = capability.DefaultInputBoxSize,
        Order = index,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static FormInputTypeCapability ToCapability(FormInputTypeDefinition type)
    {
        var fallback = FormInputTypeCatalog.Get(type.Type);
        var normalizedType = FormInputTypeCatalog.NormalizeType(type.Type);
        return new FormInputTypeCapability(
            normalizedType,
            string.IsNullOrWhiteSpace(type.LabelKey) ? fallback.LabelKey : type.LabelKey,
            type.SupportsMaxCharacters,
            type.SupportsOptions,
            type.SupportsInputBoxSize,
            type.UsesMultilineInput,
            type.SupportsMaxCharacters
                ? Math.Clamp(type.DefaultMaxCharacters <= 0 ? fallback.DefaultMaxCharacters : type.DefaultMaxCharacters, 1, FormInputTypeCatalog.MaxCharactersLimit)
                : 0,
            type.SupportsInputBoxSize
                ? Math.Clamp(type.DefaultInputBoxSize <= 0 ? fallback.DefaultInputBoxSize : type.DefaultInputBoxSize, FormInputTypeCatalog.MinInputBoxSize, FormInputTypeCatalog.MaxInputBoxSize)
                : 1);
    }

    private static FormInputTypeResponse Map(FormInputTypeDefinition type)
    {
        var capability = ToCapability(type);
        return new FormInputTypeResponse
        {
            Id = type.Id,
            Type = capability.Type,
            LabelKey = capability.LabelKey,
            Name = new(type.Name),
            Active = type.Active,
            SupportsMaxCharacters = capability.SupportsMaxCharacters,
            SupportsOptions = capability.SupportsOptions,
            SupportsInputBoxSize = capability.SupportsInputBoxSize,
            UsesMultilineInput = capability.UsesMultilineInput,
            DefaultMaxCharacters = capability.DefaultMaxCharacters,
            DefaultInputBoxSize = capability.DefaultInputBoxSize,
            Order = type.Order,
            CreatedAt = type.CreatedAt,
            UpdatedAt = type.UpdatedAt
        };
    }

    private static Dictionary<string, string> CleanTextMap(Dictionary<string, string>? values) =>
        values is null
            ? new()
            : values
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key.Trim().ToLowerInvariant(),
                    pair => pair.Value?.Trim() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

    private static string DefaultDisplayName(FormInputTypeCapability capability) =>
        capability.Type switch
        {
            "text" => "Short Text",
            "email" => "Email",
            "tel" => "Phone",
            "textarea" => "Long Text",
            "select" => "Dropdown",
            "checkbox" => "Checkbox",
            "date" => "Date",
            "number" => "Number",
            "url" => "URL",
            _ => capability.Type
        };
}
