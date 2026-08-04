namespace Contracts.Admin;

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
