using FullProject.Data;
using FullProject.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.AssetService;

public sealed class StoredAssetService
{
    private readonly MongoDbContext _context;

    public StoredAssetService(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<StoredAsset> RecordReadyAsync(
        string assetId,
        int schemaVersion,
        AssetStorageOwner owner,
        string storageKey,
        string publicUrl,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string actorId,
        int version = 1,
        string? resourceId = null,
        string? etag = null,
        string? previousStorageKey = null,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(assetId, out _)) assetId = ObjectId.GenerateNewId().ToString();
        var now = DateTime.UtcNow;
        var asset = new StoredAsset
        {
            Id = assetId,
            SchemaVersion = schemaVersion,
            OwnerDomain = owner.Domain,
            OwnerType = owner.Type,
            OwnerId = owner.Id,
            Role = owner.Role,
            ResourceId = ObjectId.TryParse(resourceId, out _) ? resourceId : null,
            AssetVersion = Math.Max(1, version),
            StorageKey = storageKey,
            PublicUrl = publicUrl,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFileName = Path.GetFileName(storageKey),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            ETag = etag,
            LifecycleStatus = "ready",
            PreviousStorageKey = previousStorageKey,
            CreatedById = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _context.StoredAssets.ReplaceOneAsync(
            item => item.Id == asset.Id,
            asset,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
        return asset;
    }

    public async Task MarkSupersededAsync(string? storageKey, DateTime deleteAfterUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        await _context.StoredAssets.UpdateOneAsync(
            item => item.StorageKey == storageKey,
            Builders<StoredAsset>.Update
                .Set(item => item.LifecycleStatus, "superseded")
                .Set(item => item.DeleteAfterUtc, deleteAfterUtc)
                .Set(item => item.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
    }

    public async Task<int> CleanupSupersededAsync(R2StorageService storage, CancellationToken cancellationToken)
    {
        var candidates = await _context.StoredAssets
            .Find(item => (item.LifecycleStatus == "superseded" || item.LifecycleStatus == "deletion-failed") &&
                          item.DeleteAfterUtc != null && item.DeleteAfterUtc <= DateTime.UtcNow)
            .Limit(100)
            .ToListAsync(cancellationToken);
        var deleted = 0;
        foreach (var asset in candidates)
        {
            var stillCurrent = await _context.ManagedResources
                .Find(item => item.StorageKey == asset.StorageKey)
                .AnyAsync(cancellationToken);
            if (stillCurrent) continue;

            if (!await storage.DeleteKeyAsync(asset.StorageKey, cancellationToken))
            {
                asset.LifecycleStatus = "deletion-failed";
                asset.UpdatedAt = DateTime.UtcNow;
                await _context.StoredAssets.ReplaceOneAsync(item => item.Id == asset.Id, asset, cancellationToken: cancellationToken);
                continue;
            }

            asset.LifecycleStatus = "deleted";
            asset.DeleteAfterUtc = null;
            asset.UpdatedAt = DateTime.UtcNow;
            await _context.StoredAssets.ReplaceOneAsync(item => item.Id == asset.Id, asset, cancellationToken: cancellationToken);
            deleted++;
        }
        return deleted;
    }
}

public sealed class StoredAssetCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoredAssetCleanupService> _logger;

    public StoredAssetCleanupService(IServiceScopeFactory scopeFactory, ILogger<StoredAssetCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleaned = await scope.ServiceProvider.GetRequiredService<StoredAssetService>()
                    .CleanupSupersededAsync(scope.ServiceProvider.GetRequiredService<R2StorageService>(), stoppingToken);
                if (cleaned > 0) _logger.LogInformation("Deleted {Count} superseded stored assets after their retention period.", cleaned);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { _logger.LogError(exception, "Stored asset cleanup failed."); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
