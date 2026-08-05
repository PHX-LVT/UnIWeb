namespace Contracts.Admin;

public static class ResourceUploadContextCodes
{
    public const string LibraryManual = "library.manual";

    public const string BrandLogo = "brand.logo";
    public const string BrandFavicon = "brand.favicon";
    public const string BrandFooter = "brand.footer";

    public const string BackgroundSection = "background.section";
    public const string BackgroundBanner = "background.banner";

    public const string ContentManagementHero = "content.management.hero";
    public const string ContentManagementThumbnail = "content.management.thumbnail";
    public const string ContentManagementBody = "content.management.body";
    public const string ContentManagementGallery = "content.management.gallery";
    public const string ContentManagementLegacy = "content.management.legacy";
    public const string ContentSectionHeroMedia = "content.section.hero-media";
    public const string ContentSectionListItem = "content.section.list-item";
    public const string ContentSectionCarousel = "content.section.carousel";
    public const string ContentSectionHighlight = "content.section.highlight";
    public const string ContentSectionShowcase = "content.section.showcase";
    public const string ContentSectionGallery = "content.section.gallery";
    public const string ContentSectionLegacy = "content.section.legacy";
    public const string ContentBlockImage = "content.block.image";
    public const string ContentBlockCard = "content.block.card";
    public const string ContentBlockLegacy = "content.block.legacy";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        LibraryManual,
        BrandLogo,
        BrandFavicon,
        BrandFooter,
        BackgroundSection,
        BackgroundBanner,
        ContentManagementHero,
        ContentManagementThumbnail,
        ContentManagementBody,
        ContentManagementGallery,
        ContentManagementLegacy,
        ContentSectionHeroMedia,
        ContentSectionListItem,
        ContentSectionCarousel,
        ContentSectionHighlight,
        ContentSectionShowcase,
        ContentSectionGallery,
        ContentSectionLegacy,
        ContentBlockImage,
        ContentBlockCard,
        ContentBlockLegacy
    };

    public static readonly IReadOnlySet<string> ClientInitiable = new HashSet<string>(
        All.Where(context =>
            !string.Equals(context, ContentManagementLegacy, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context, ContentSectionLegacy, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context, ContentBlockLegacy, StringComparison.OrdinalIgnoreCase)),
        StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? LibraryManual
            : value.Trim().ToLowerInvariant();
        return All.Contains(normalized) ? normalized : null;
    }

    public static string? NormalizeClient(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = Normalize(value);
        return normalized is not null && ClientInitiable.Contains(normalized) ? normalized : null;
    }

    public static bool IsAutomatic(string? value) =>
        Normalize(value) is { } normalized &&
        !string.Equals(normalized, LibraryManual, StringComparison.OrdinalIgnoreCase);
}

public sealed class ResourceUploadCapabilitiesDto
{
    public bool DirectUploadEnabled { get; set; }
    public long MaxImageBytes { get; set; }
    public long MaxFileBytes { get; set; }
    public long MaxVideoBytes { get; set; }
    public List<string> AllowedImageFormats { get; set; } = [];
    public List<string> AllowedFileFormats { get; set; } = [];
    public List<string> AllowedVideoFormats { get; set; } = [];
    public long MultipartThresholdBytes { get; set; }
    public long MultipartPartSizeBytes { get; set; }
    public int MaxConcurrentUploads { get; set; }
    public int PendingLifetimeMinutes { get; set; }
}

public sealed class ResourceUploadInitiateRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadContext { get; set; } = string.Empty;
    public string? AlbumId { get; set; }
    public string? ReplaceResourceId { get; set; }
}

public sealed class ResourceUploadInitiateResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Mode { get; set; } = "single";
    public string? UploadUrl { get; set; }
    public Dictionary<string, string> RequiredHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime UploadUrlExpiresAtUtc { get; set; }
    public long PartSizeBytes { get; set; }
    public int PartCount { get; set; }
}

public sealed class ResourceUploadPartUrlsRequest
{
    public List<int> PartNumbers { get; set; } = [];
}

public sealed class ResourceUploadPartUrlDto
{
    public int PartNumber { get; set; }
    public string UploadUrl { get; set; } = string.Empty;
}

public sealed class ResourceUploadPartUrlsResponse
{
    public List<ResourceUploadPartUrlDto> Parts { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class ResourceUploadCompletedPartDto
{
    public int PartNumber { get; set; }
    public string ETag { get; set; } = string.Empty;
}

public sealed class ResourceUploadCompleteRequest
{
    public List<ResourceUploadCompletedPartDto> Parts { get; set; } = [];
}

public sealed class ResourceUploadSessionDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string UploadContext { get; set; } = ResourceUploadContextCodes.LibraryManual;
    public long SizeBytes { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResourceId { get; set; }
    public string? ReplaceResourceId { get; set; }
    public ResourceUploadResourceDto? Resource { get; set; }
}

public sealed class ResourceUploadResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public int AssetVersion { get; set; }
    public int StorageSchemaVersion { get; set; }
    public string Kind { get; set; } = "file";
    public string? Purpose { get; set; }
    public string? OriginContext { get; set; }
    public Dictionary<string, string> Name { get; set; } = [];
    public Dictionary<string, string> Description { get; set; } = [];
    public string Url { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Source { get; set; } = "managed-upload";
    public string? OriginalSourceUrl { get; set; }
    public string? LicenseName { get; set; }
    public string? Attribution { get; set; }
    public string? DeletionState { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? AlbumId { get; set; }
    public bool Active { get; set; }
    public int UsageCount { get; set; }
    public bool IsInUse { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public string? UpdatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
