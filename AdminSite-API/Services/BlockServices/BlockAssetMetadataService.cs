using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using SharedComponents.Helpers;

namespace FullProject.Services.BlockServices;

public sealed class BlockAssetMetadataService
{
    private readonly MongoDbContext _context;

    public BlockAssetMetadataService(MongoDbContext context)
    {
        _context = context;
    }

    public Task<string?> CanonicalizeAsync(BlockCreateDto dto) => dto switch
    {
        ImageBlockCreateDto image => CanonicalizeImageAsync(image),
        VideoBlockCreateDto video => CanonicalizeVideoAsync(video),
        FileBlockCreateDto file => CanonicalizeFileAsync(file),
        CardBlockCreateDto card => CanonicalizeCardAsync(card),
        _ => Task.FromResult<string?>(null)
    };

    public Task<string?> CanonicalizeAsync(BlockUpdateDto dto) => dto switch
    {
        ImageBlockUpdateDto image => CanonicalizeImageAsync(image),
        VideoBlockUpdateDto video => CanonicalizeVideoAsync(video),
        FileBlockUpdateDto file => CanonicalizeFileAsync(file),
        CardBlockUpdateDto card => CanonicalizeCardAsync(card),
        _ => Task.FromResult<string?>(null)
    };

    public static BlockAssetReference ToModel(BlockAssetReferenceDto? value) => new()
    {
        SchemaVersion = 1,
        Url = CleanOptional(value?.Url),
        ResourceId = CleanOptional(value?.ResourceId),
        ResourceSource = NormalizeSource(value?.ResourceSource),
        StorageKey = CleanOptional(value?.StorageKey),
        FileName = CleanOptional(value?.FileName),
        ContentType = CleanOptional(value?.ContentType),
        SizeBytes = Math.Max(value?.SizeBytes ?? 0, 0)
    };

    public static BlockAssetReferenceDto ToAdmin(BlockAssetReference? value)
    {
        value ??= new BlockAssetReference();
        return new BlockAssetReferenceDto
        {
            SchemaVersion = 1,
            Url = value.Url,
            ResourceId = value.ResourceId,
            ResourceSource = NormalizeSource(value.ResourceSource),
            StorageKey = value.StorageKey,
            FileName = value.FileName,
            ContentType = value.ContentType,
            SizeBytes = value.SizeBytes
        };
    }

    public static Contracts.Public.PublicBlockAssetReferenceDto ToPublic(BlockAssetReference? value)
    {
        value ??= new BlockAssetReference();
        return new Contracts.Public.PublicBlockAssetReferenceDto
        {
            SchemaVersion = 1,
            Url = value.Url,
            ResourceId = value.ResourceId,
            ResourceSource = NormalizeSource(value.ResourceSource),
            StorageKey = value.StorageKey,
            FileName = value.FileName,
            ContentType = value.ContentType,
            SizeBytes = value.SizeBytes
        };
    }

    private async Task<string?> CanonicalizeImageAsync(ImageBlockCreateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.ImageUrl), "image", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.ImageUrl = result.Asset?.Url;
        return null;
    }

    private async Task<string?> CanonicalizeImageAsync(ImageBlockUpdateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.ImageUrl), "image", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.ImageUrl = result.Asset?.Url;
        return null;
    }

    private async Task<string?> CanonicalizeCardAsync(CardBlockCreateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.ImageUrl), "image", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.ImageUrl = result.Asset?.Url;
        return null;
    }

    private async Task<string?> CanonicalizeCardAsync(CardBlockUpdateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.ImageUrl), "image", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.ImageUrl = result.Asset?.Url;
        return null;
    }

    private async Task<string?> CanonicalizeFileAsync(FileBlockCreateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.FileUrl, dto.Filename, dto.FileType), "file", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.FileUrl = result.Asset?.Url;
        dto.FileType = result.Asset?.ContentType ?? dto.FileType;
        return null;
    }

    private async Task<string?> CanonicalizeFileAsync(FileBlockUpdateDto dto)
    {
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.FileUrl, dto.Filename, dto.FileType), "file", false);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.FileUrl = result.Asset?.Url;
        dto.FileType = result.Asset?.ContentType ?? dto.FileType;
        return null;
    }

    private async Task<string?> CanonicalizeVideoAsync(VideoBlockCreateDto dto)
    {
        var source = dto.SourceType == "upload" ? "DirectUpload" : "ExternalUrl";
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.EmbedUrl, source: source), "video", true);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.EmbedUrl = result.Asset?.Url ?? string.Empty;
        dto.SourceType = VideoUrlHelper.IsYouTubeVideoUrl(dto.EmbedUrl) ? "youtube" : "upload";
        return null;
    }

    private async Task<string?> CanonicalizeVideoAsync(VideoBlockUpdateDto dto)
    {
        var source = dto.SourceType == "upload" ? "DirectUpload" : "ExternalUrl";
        var result = await CanonicalizeAsync(dto.Asset ?? Legacy(dto.EmbedUrl, source: source), "video", true);
        if (result.Error is not null) return result.Error;
        dto.Asset = result.Asset;
        dto.EmbedUrl = result.Asset?.Url ?? string.Empty;
        dto.SourceType = VideoUrlHelper.IsYouTubeVideoUrl(dto.EmbedUrl) ? "youtube" : "upload";
        return null;
    }

    private async Task<(BlockAssetReferenceDto? Asset, string? Error)> CanonicalizeAsync(
        BlockAssetReferenceDto? input,
        string expectedKind,
        bool allowYouTube)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Url))
            return (new BlockAssetReferenceDto { SchemaVersion = 1, ResourceSource = "DirectUpload" }, null);

        var source = NormalizeSource(input.ResourceSource);
        if (source == "ManagedResource")
        {
            if (string.IsNullOrWhiteSpace(input.ResourceId) || !ObjectId.TryParse(input.ResourceId, out _))
                return (null, "Managed Block assets require a valid Resource Library item.");
            var resource = await _context.ManagedResources
                .Find(item => item.Id == input.ResourceId && item.Active)
                .FirstOrDefaultAsync();
            if (resource is null)
                return (null, "The selected Resource Library item is missing or inactive.");
            if (!string.Equals(NormalizeKind(resource.Kind), expectedKind, StringComparison.Ordinal))
                return (null, $"The selected Resource Library item must be a {expectedKind}.");
            return (new BlockAssetReferenceDto
            {
                SchemaVersion = 1,
                Url = resource.Url,
                ResourceId = resource.Id,
                ResourceSource = "ManagedResource",
                StorageKey = resource.StorageKey,
                FileName = resource.FileName,
                ContentType = resource.ContentType,
                SizeBytes = resource.SizeBytes
            }, null);
        }

        var url = input.Url.Trim();
        if (source == "ExternalUrl")
        {
            if (!allowYouTube || !ContentSecurityPolicy.IsAllowedVideoUrl(url))
                return (null, "External video Blocks accept only a complete YouTube video link.");
            return (new BlockAssetReferenceDto
            {
                SchemaVersion = 1,
                Url = VideoUrlHelper.ToEmbedUrl(url),
                ResourceSource = "ExternalUrl"
            }, null);
        }

        var errors = new List<string>();
        ContentSecurityPolicy.ValidateOptionalUrl(url, "Block asset URL", errors);
        if (errors.Count > 0) return (null, errors[0]);
        return (new BlockAssetReferenceDto
        {
            SchemaVersion = 1,
            Url = url,
            ResourceSource = "DirectUpload",
            StorageKey = CleanOptional(input.StorageKey),
            FileName = CleanOptional(input.FileName),
            ContentType = CleanOptional(input.ContentType),
            SizeBytes = Math.Max(input.SizeBytes ?? 0, 0)
        }, null);
    }

    private static BlockAssetReferenceDto Legacy(
        string? url,
        string? fileName = null,
        string? contentType = null,
        string source = "DirectUpload") => new()
    {
        Url = url,
        FileName = fileName,
        ContentType = contentType,
        ResourceSource = source
    };

    private static string NormalizeSource(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "managedresource" or "managed-resource" => "ManagedResource",
        "externalurl" or "external-url" => "ExternalUrl",
        _ => "DirectUpload"
    };

    private static string NormalizeKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "image" => "image",
        "video" => "video",
        _ => "file"
    };

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
