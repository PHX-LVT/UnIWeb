using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.AssetService;

public sealed class ManagedImageBackfillService
{
    private readonly MongoDbContext _context;
    private readonly ManagedResourceAlbumService _albums;
    private readonly ManagedResourceService _resources;
    private readonly ILogger<ManagedImageBackfillService> _logger;

    public ManagedImageBackfillService(
        MongoDbContext context,
        ManagedResourceAlbumService albums,
        ManagedResourceService resources,
        ILogger<ManagedImageBackfillService> logger)
    {
        _context = context;
        _albums = albums;
        _resources = resources;
        _logger = logger;
    }

    public async Task<ManagedImageBackfillResultDto> RunAsync(
        ManagedImageBackfillRequest request,
        string actorId,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 2_000);
        var imageFilter = Builders<StoredAsset>.Filter.Or(
            Builders<StoredAsset>.Filter.Regex(
                asset => asset.ContentType,
                new BsonRegularExpression("^image/", "i")),
            Builders<StoredAsset>.Filter.Regex(
                asset => asset.StorageKey,
                new BsonRegularExpression(@"\.(jpe?g|png|webp|gif|svg)$", "i")));
        var unlinkedFilter = Builders<StoredAsset>.Filter.Or(
            Builders<StoredAsset>.Filter.Exists(asset => asset.ResourceId, false),
            Builders<StoredAsset>.Filter.Eq(asset => asset.ResourceId, null));
        var supportedOwnerFilter = Builders<StoredAsset>.Filter.Or(
            Builders<StoredAsset>.Filter.And(
                Builders<StoredAsset>.Filter.Eq(asset => asset.OwnerDomain, "global"),
                Builders<StoredAsset>.Filter.Or(
                    Builders<StoredAsset>.Filter.In(asset => asset.OwnerType, ["branding", "footer"]),
                    Builders<StoredAsset>.Filter.In(asset => asset.Role, ["branding", "footer"]))),
            Builders<StoredAsset>.Filter.Eq(asset => asset.OwnerDomain, "content"),
            Builders<StoredAsset>.Filter.And(
                Builders<StoredAsset>.Filter.Eq(asset => asset.OwnerDomain, "page-builder"),
                Builders<StoredAsset>.Filter.In(asset => asset.OwnerType, ["section", "block"])),
            Builders<StoredAsset>.Filter.And(
                Builders<StoredAsset>.Filter.Eq(asset => asset.OwnerDomain, "resource-library"),
                Builders<StoredAsset>.Filter.Eq(asset => asset.OwnerType, "managed-resource")));
        var filter = Builders<StoredAsset>.Filter.Eq(asset => asset.LifecycleStatus, "ready") &
                     unlinkedFilter &
                     imageFilter &
                     supportedOwnerFilter &
                     Builders<StoredAsset>.Filter.Regex(
                         asset => asset.StorageKey,
                         new BsonRegularExpression(".+")) &
                     Builders<StoredAsset>.Filter.Regex(
                         asset => asset.PublicUrl,
                         new BsonRegularExpression(".+"));
        if (!string.IsNullOrWhiteSpace(request.AfterAssetId))
            filter &= Builders<StoredAsset>.Filter.Gt(asset => asset.Id, request.AfterAssetId.Trim());
        var assets = await _context.StoredAssets.Find(filter)
            .SortBy(asset => asset.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        var result = new ManagedImageBackfillResultDto
        {
            DryRun = request.DryRun,
            ScannedCount = assets.Count,
            NextAfterAssetId = assets.Count == limit ? assets[^1].Id : null
        };
        var rootAlbums = new Dictionary<string, ResourceAlbum>(StringComparer.Ordinal);

        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = ResolveContext(asset);
            if (context is null ||
                string.IsNullOrWhiteSpace(asset.StorageKey) ||
                string.IsNullOrWhiteSpace(asset.PublicUrl))
            {
                result.ExcludedCount++;
                result.Items.Add(Item(asset, context, null, "excluded"));
                continue;
            }

            result.EligibleCount++;
            var existing = await FindExistingResourceAsync(asset, cancellationToken);
            if (existing is not null)
            {
                if (!request.DryRun &&
                    !await TryLinkStoredAssetAsync(asset, existing.Id, cancellationToken))
                {
                    result.FailedCount++;
                    result.Items.Add(Item(asset, context, existing.Id, "failed"));
                    continue;
                }

                result.AlreadyManagedCount++;
                result.Items.Add(Item(asset, context, existing.Id, "already-managed"));
                continue;
            }

            if (request.DryRun)
            {
                result.WouldCreateCount++;
                result.Items.Add(Item(asset, context, null, "would-create"));
                continue;
            }

            try
            {
                var rootSystemKey = ResourceUploadContextPolicy.RootSystemKeyFor(context)
                    ?? throw new InvalidOperationException("The legacy image context has no root album.");
                if (!rootAlbums.TryGetValue(rootSystemKey, out var album))
                {
                    album = await _albums.ResolveSystemRootAsync(context, actorId, cancellationToken);
                    rootAlbums[rootSystemKey] = album;
                }
                var resourceId = ObjectId.GenerateNewId().ToString();
                var fileName = SourceFileName(asset);
                var create = _resources.BuildUploadCreateDto(
                    asset.PublicUrl,
                    asset.StorageKey,
                    "image",
                    fileName,
                    ImageContentType(asset),
                    asset.SizeBytes,
                    album.Id);
                create.Name["en"] = FriendlyName(fileName);

                var (resource, errors) = await _resources.CreateUploadedAsync(
                    create,
                    actorId,
                    resourceId,
                    asset.Id,
                    Math.Max(1, asset.AssetVersion),
                    Math.Max(1, asset.SchemaVersion),
                    context);
                if (resource is null)
                    throw new InvalidOperationException(string.Join(' ', errors));

                if (!await LinkStoredAssetAsync(asset, resource.Id, cancellationToken))
                {
                    await _context.ManagedResources.DeleteOneAsync(
                        item => item.Id == resource.Id && item.AssetId == asset.Id,
                        cancellationToken);
                    throw new InvalidOperationException("The source Stored Asset changed before it could be linked.");
                }
                result.CreatedCount++;
                result.Items.Add(Item(asset, context, resource.Id, "created"));
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                var concurrent = await FindExistingResourceAsync(asset, cancellationToken);
                if (concurrent is null)
                {
                    result.FailedCount++;
                    result.Items.Add(Item(asset, context, null, "failed"));
                    _logger.LogError(exception, "Managed image backfill hit an unresolved duplicate for Asset {AssetId}.", asset.Id);
                    continue;
                }

                if (!await TryLinkStoredAssetAsync(asset, concurrent.Id, cancellationToken))
                {
                    result.FailedCount++;
                    result.Items.Add(Item(asset, context, concurrent.Id, "failed"));
                    continue;
                }
                result.AlreadyManagedCount++;
                result.Items.Add(Item(asset, context, concurrent.Id, "already-managed"));
            }
            catch (Exception exception)
            {
                result.FailedCount++;
                result.Items.Add(Item(asset, context, null, "failed"));
                _logger.LogError(exception, "Managed image backfill failed for Asset {AssetId}.", asset.Id);
            }
        }

        return result;
    }

    private async Task<ManagedResource?> FindExistingResourceAsync(
        StoredAsset asset,
        CancellationToken cancellationToken)
    {
        var filters = new List<FilterDefinition<ManagedResource>>
        {
            Builders<ManagedResource>.Filter.Eq(resource => resource.AssetId, asset.Id),
            Builders<ManagedResource>.Filter.Eq(resource => resource.StorageKey, asset.StorageKey)
        };
        return await _context.ManagedResources
            .Find(Builders<ManagedResource>.Filter.Or(filters))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> LinkStoredAssetAsync(
        StoredAsset asset,
        string resourceId,
        CancellationToken cancellationToken)
    {
        var result = await _context.StoredAssets.UpdateOneAsync(
            stored => stored.Id == asset.Id &&
                      (stored.ResourceId == null || stored.ResourceId == resourceId),
            Builders<StoredAsset>.Update
                .Set(stored => stored.ResourceId, resourceId)
                .Set(stored => stored.OwnerDomain, "resource-library")
                .Set(stored => stored.OwnerType, "managed-resource")
                .Set(stored => stored.OwnerId, resourceId)
                .Set(stored => stored.Role, "image")
                .Set(stored => stored.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    private async Task<bool> TryLinkStoredAssetAsync(
        StoredAsset asset,
        string resourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LinkStoredAssetAsync(asset, resourceId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Managed image backfill could not link Stored Asset {AssetId} to Resource {ResourceId}.",
                asset.Id,
                resourceId);
            return false;
        }
    }

    private static string? ResolveContext(StoredAsset asset)
    {
        if (string.Equals(asset.OwnerDomain, "icons", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(asset.OwnerDomain, "resource-library", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(asset.OwnerType, "resource-album", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (string.Equals(asset.OwnerDomain, "resource-library", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(asset.OwnerType, "managed-resource", StringComparison.OrdinalIgnoreCase))
        {
            return ResourceUploadContextCodes.ContentManagementLegacy;
        }

        return LegacyManagedImageContextPolicy.Resolve(asset.OwnerDomain, asset.OwnerType, asset.Role);
    }

    private static ManagedImageBackfillItemDto Item(
        StoredAsset asset,
        string? context,
        string? resourceId,
        string status) => new()
    {
        AssetId = asset.Id,
        ResourceId = resourceId,
        FileName = SourceFileName(asset),
        UploadContext = context ?? string.Empty,
        RootSystemKey = context is null
            ? string.Empty
            : ResourceUploadContextPolicy.RootSystemKeyFor(context) ?? string.Empty,
        Status = status
    };

    private static string SourceFileName(StoredAsset asset) =>
        Path.GetFileName(string.IsNullOrWhiteSpace(asset.OriginalFileName)
            ? asset.StoredFileName
            : asset.OriginalFileName) is { Length: > 0 } name
                ? name
                : $"{asset.Id}.img";

    private static string FriendlyName(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName) is { Length: > 0 } name
            ? name
            : "Imported image";

    private static string ImageContentType(StoredAsset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.ContentType) &&
            asset.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return asset.ContentType;
        return Path.GetExtension(SourceFileName(asset)).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}
