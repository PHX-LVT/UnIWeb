namespace Contracts.Admin;

public sealed class ResourceBulkMoveRequest
{
    public List<string> ResourceIds { get; set; } = [];
    public string? AlbumId { get; set; }
}

public sealed class ResourceBulkMoveResult
{
    public string? AlbumId { get; set; }
    public int RequestedCount { get; set; }
    public int UpdatedCount { get; set; }
}

public sealed class ResourceBulkDeleteRequest
{
    public List<string> ResourceIds { get; set; } = [];
}

public sealed class ResourceBulkDeleteItemResult
{
    public string ResourceId { get; set; } = string.Empty;
    public bool Deleted { get; set; }
    public int UsageCount { get; set; }
    public string? Error { get; set; }
}

public sealed class ResourceBulkDeleteResult
{
    public int RequestedCount { get; set; }
    public int DeletedCount { get; set; }
    public int BlockedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ResourceBulkDeleteItemResult> Items { get; set; } = [];
}
