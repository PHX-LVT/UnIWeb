namespace Contracts.Admin;

public sealed class StorageInventoryDto
{
    public int TotalObjectCount { get; set; }
    public int CanonicalObjectCount { get; set; }
    public int LegacyObjectCount { get; set; }
    public int TemporaryObjectCount { get; set; }
    public int UntrackedObjectCount { get; set; }
    public bool Truncated { get; set; }
    public List<StorageInventoryObjectDto> UntrackedObjects { get; set; } = [];
}

public sealed class StorageInventoryObjectDto
{
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
}

public sealed class StorageMigrationPlanRequest
{
    public string MigrationId { get; set; } = "storage-schema-v2";
    public bool DryRun { get; set; } = true;
}

public sealed class StorageMigrationPlanDto
{
    public string MigrationId { get; set; } = string.Empty;
    public bool DryRun { get; set; }
    public int CandidateCount { get; set; }
    public int AlreadyCanonicalCount { get; set; }
    public List<StorageMigrationCandidateDto> Candidates { get; set; } = [];
}

public sealed class StorageMigrationCandidateDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string DestinationKey { get; set; } = string.Empty;
    public string Status { get; set; } = "planned";
    public string? Error { get; set; }
}

public sealed class StorageMigrationExecuteRequest
{
    public string MigrationId { get; set; } = "storage-schema-v2";
    public int Limit { get; set; } = 25;
}

public sealed class StorageMigrationExecuteDto
{
    public string MigrationId { get; set; } = string.Empty;
    public int AttemptedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public List<StorageMigrationCandidateDto> Results { get; set; } = [];
}
