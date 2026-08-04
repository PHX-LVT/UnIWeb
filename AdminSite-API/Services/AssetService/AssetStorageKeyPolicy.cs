using FullProject.Settings;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace FullProject.Services.AssetService;

public sealed class AssetStorageKeyPolicy
{
    private readonly R2StorageSettings _settings;

    public AssetStorageKeyPolicy(IOptions<R2StorageSettings> settings)
    {
        _settings = settings.Value;
    }

    public int SchemaVersion => Math.Max(1, _settings.StorageSchemaVersion);

    public string CreateTemporaryKey(string sessionId, string fileName) =>
        Join(_settings.KeyPrefix, _settings.TemporaryFolder, Segment(sessionId), $"{Guid.NewGuid():N}{Extension(fileName)}");

    public string CreateResourceKey(
        string resourceId,
        int version,
        string kind,
        string friendlyName,
        string originalFileName)
    {
        var scope = string.Equals(kind, "file", StringComparison.OrdinalIgnoreCase) ? "files" : "media";
        var readable = Slug(string.IsNullOrWhiteSpace(friendlyName)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : friendlyName, 80);
        return Join(
            _settings.KeyPrefix,
            _settings.ResourceLibraryFolder,
            scope,
            $"{Segment(resourceId)}-v{Math.Max(1, version)}-{readable}{Extension(originalFileName)}");
    }

    public string CreateOwnedKey(
        string ownerDomain,
        string ownerType,
        string ownerId,
        string role,
        string assetId,
        int version,
        string fileName)
    {
        var root = ownerDomain.Trim().ToLowerInvariant() switch
        {
            "content" => _settings.ContentFolder,
            "page-builder" => _settings.PageBuilderFolder,
            "global" => _settings.GlobalFolder,
            "icons" => _settings.CustomIconFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(ownerDomain), "Unknown asset owner domain.")
        };
        var readable = Slug(Path.GetFileNameWithoutExtension(fileName), 64);
        return Join(
            _settings.KeyPrefix,
            root,
            Segment(ownerType),
            Segment(ownerId),
            Segment(role),
            $"{Segment(assetId)}-v{Math.Max(1, version)}-{readable}{Extension(fileName)}");
    }

    public AssetStorageOwner MapLegacyFolder(string? folder)
    {
        var value = (folder ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "branding" => new("global", "branding", "site", "branding"),
            "footer" => new("global", "footer", "site", "footer"),
            "content-hero" => new("content", "content-item", "unassigned", "hero"),
            "content-thumbnails" => new("content", "content-item", "unassigned", "thumbnail"),
            "content-body" => new("content", "content-item", "unassigned", "body"),
            "content-files" => new("content", "content-item", "unassigned", "attachment"),
            "uploads" => new("content", "upload", "unassigned", "asset"),
            "managed-resources" => new("content", "legacy-resource", "unassigned", "asset"),
            "sections" => new("page-builder", "section", "unassigned", "asset"),
            "blocks" => new("page-builder", "block", "unassigned", "asset"),
            "gallery" => new("page-builder", "section", "unassigned", "gallery"),
            "hero" => new("page-builder", "section", "unassigned", "hero"),
            "carousel" => new("page-builder", "section", "unassigned", "carousel"),
            "showcase" => new("page-builder", "section", "unassigned", "showcase"),
            "list-items" => new("page-builder", "section", "unassigned", "list-item"),
            "highlights" => new("page-builder", "section", "unassigned", "highlight"),
            "section-backgrounds" => new("page-builder", "section", "unassigned", "background"),
            "image-blocks" => new("page-builder", "block", "unassigned", "image"),
            "video-blocks" => new("page-builder", "block", "unassigned", "video"),
            "file-blocks" => new("page-builder", "block", "unassigned", "file"),
            "card-blocks" => new("page-builder", "block", "unassigned", "card"),
            "custom-icons" => new("icons", "custom-icon", "catalogue", "icon"),
            _ => throw new ArgumentOutOfRangeException(nameof(folder), $"Unsupported asset folder '{folder}'.")
        };
    }

    public string CreateMappedLegacyUploadKey(string folder, string assetId, int version, string fileName)
    {
        var owner = MapLegacyFolder(folder);
        return CreateOwnedKey(owner.Domain, owner.Type, owner.Id, owner.Role, assetId, version, fileName);
    }

    private static string Join(params string[] values)
    {
        var parts = values.SelectMany(value => (value ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        return string.Join('/', parts.Select((value, index) =>
                index == parts.Count - 1 && !string.IsNullOrWhiteSpace(Path.GetExtension(value))
                    ? $"{Slug(Path.GetFileNameWithoutExtension(value), 150)}{Extension(value)}"
                    : Segment(value))
            .Where(value => value.Length > 0));
    }

    private static string Segment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unassigned";
        return Slug(value, 90);
    }

    private static string Slug(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "asset";
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingDash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                pendingDash = false;
            }
            else
            {
                pendingDash = builder.Length > 0;
            }
            if (builder.Length >= maximumLength) break;
        }
        return builder.ToString().Trim('-') is { Length: > 0 } result ? result : "asset";
    }

    private static string Extension(string? fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty)).ToLowerInvariant();
        if (extension.Length is < 2 or > 12 || extension.Skip(1).Any(character => !char.IsLetterOrDigit(character)))
            return ".bin";
        return extension;
    }
}

public sealed record AssetStorageOwner(string Domain, string Type, string Id, string Role);
