namespace Contracts.Admin;

public sealed class ManagedImageBackfillRequest
{
    public bool DryRun { get; set; } = true;
    public int Limit { get; set; } = 500;
    public string? AfterAssetId { get; set; }
}

public sealed class ManagedImageBackfillResultDto
{
    public bool DryRun { get; set; }
    public int ScannedCount { get; set; }
    public int EligibleCount { get; set; }
    public int WouldCreateCount { get; set; }
    public int CreatedCount { get; set; }
    public int AlreadyManagedCount { get; set; }
    public int ExcludedCount { get; set; }
    public int FailedCount { get; set; }
    public string? NextAfterAssetId { get; set; }
    public List<ManagedImageBackfillItemDto> Items { get; set; } = [];
}

public sealed class ManagedImageBackfillItemDto
{
    public string AssetId { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string UploadContext { get; set; } = string.Empty;
    public string RootSystemKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
