using Contracts.Icons;
using Contracts.Public;
using FullProject.Data;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.IconServices;

public sealed class IconReferenceService
{
    public const string CustomIconPurpose = "custom-icon";
    private readonly MongoDbContext _context;

    public IconReferenceService(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<IconResolutionResult> ResolveAsync(
        IconReferenceDto? requested,
        string? legacyClass,
        IconContext context,
        IconReference? existing = null)
    {
        if (requested is not null &&
            string.Equals(requested.Source, IconSources.Custom, StringComparison.OrdinalIgnoreCase))
        {
            var resourceId = requested.ResourceId?.Trim();
            if (string.IsNullOrWhiteSpace(resourceId) || !ObjectId.TryParse(resourceId, out _))
                return IconResolutionResult.Invalid("Choose a valid Custom Icon resource.");

            var resource = await _context.ManagedResources
                .Find(item => item.Id == resourceId && item.Active)
                .FirstOrDefaultAsync();
            if (resource is null ||
                !string.Equals(resource.Kind, "image", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(resource.Purpose, CustomIconPurpose, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(resource.DeletionState))
            {
                return IconResolutionResult.Invalid("The selected Custom Icon is unavailable.");
            }

            return IconResolutionResult.Valid(new IconReference
            {
                SchemaVersion = 1,
                Source = IconSources.Custom,
                ResourceId = resource.Id,
                ResourceSource = "ManagedResource",
                Url = resource.Url,
                StorageKey = resource.StorageKey,
                FileName = resource.FileName,
                ContentType = resource.ContentType,
                SizeBytes = resource.SizeBytes,
                AltText = NormalizeLang(requested.AltText)
            }, string.Empty);
        }

        var candidate = requested?.ClassName ?? legacyClass;
        var normalized = IconCatalog.Normalize(candidate, context);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return IconResolutionResult.Valid(new IconReference
            {
                SchemaVersion = 1,
                Source = IconSources.BuiltIn,
                ClassName = normalized,
                AltText = NormalizeLang(requested?.AltText)
            }, normalized);
        }

        if (requested is not null && !string.IsNullOrWhiteSpace(candidate))
            return IconResolutionResult.Invalid("The selected built-in icon is not supported in this location.");

        if (existing is not null && string.IsNullOrWhiteSpace(candidate))
            return IconResolutionResult.Valid(null, string.Empty);

        // Keep an untouched legacy class readable until the editor replaces it.
        return IconResolutionResult.Valid(null, legacyClass?.Trim() ?? string.Empty);
    }

    public static IconReferenceDto? ToAdmin(IconReference? value)
    {
        if (value is null) return null;
        return new IconReferenceDto
        {
            SchemaVersion = value.SchemaVersion,
            Source = value.Source,
            ClassName = value.ClassName,
            ResourceId = value.ResourceId,
            ResourceSource = value.ResourceSource,
            Url = value.Url,
            StorageKey = value.StorageKey,
            FileName = value.FileName,
            ContentType = value.ContentType,
            SizeBytes = value.SizeBytes,
            AltText = NormalizeLang(value.AltText)
        };
    }

    public static IconReference? ToModel(IconReferenceDto? value)
    {
        if (value is null) return null;
        return new IconReference
        {
            SchemaVersion = value.SchemaVersion,
            Source = value.Source,
            ClassName = value.ClassName,
            ResourceId = value.ResourceId,
            ResourceSource = value.ResourceSource,
            Url = value.Url,
            StorageKey = value.StorageKey,
            FileName = value.FileName,
            ContentType = value.ContentType,
            SizeBytes = value.SizeBytes,
            AltText = NormalizeLang(value.AltText)
        };
    }

    public static PublicIconReferenceDto? ToPublic(IconReference? value, string? legacyClass = null)
    {
        if (value is null)
        {
            var normalizedLegacy = IconCatalog.Normalize(legacyClass);
            return string.IsNullOrWhiteSpace(normalizedLegacy)
                ? null
                : new PublicIconReferenceDto { Source = IconSources.BuiltIn, ClassName = normalizedLegacy };
        }

        return new PublicIconReferenceDto
        {
            Source = value.Source,
            ClassName = value.Source == IconSources.BuiltIn ? IconCatalog.Normalize(value.ClassName) : null,
            Url = value.Source == IconSources.Custom ? value.Url : null,
            AltText = NormalizeLang(value.AltText)
        };
    }

    public static IconReference Clone(IconReference value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        Source = value.Source,
        ClassName = value.ClassName,
        ResourceId = value.ResourceId,
        ResourceSource = value.ResourceSource,
        Url = value.Url,
        StorageKey = value.StorageKey,
        FileName = value.FileName,
        ContentType = value.ContentType,
        SizeBytes = value.SizeBytes,
        AltText = NormalizeLang(value.AltText)
    };

    private static Dictionary<string, string> NormalizeLang(Dictionary<string, string>? source) =>
        (source ?? new())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim().ToLowerInvariant(),
                pair => pair.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
}

public sealed record IconResolutionResult(IconReference? Value, string LegacyClass, string? Error)
{
    public bool Success => string.IsNullOrWhiteSpace(Error);
    public static IconResolutionResult Valid(IconReference? value, string legacyClass) => new(value, legacyClass, null);
    public static IconResolutionResult Invalid(string error) => new(null, string.Empty, error);
}
