using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.AssetService;

public sealed class StorageMigrationService
{
    private readonly MongoDbContext _context;
    private readonly R2StorageService _storage;
    private readonly AssetStorageKeyPolicy _keys;
    private readonly StoredAssetService _storedAssets;
    private readonly ManagedResourceService _resources;
    private readonly R2StorageSettings _settings;
    private readonly ILogger<StorageMigrationService> _logger;

    public StorageMigrationService(
        MongoDbContext context,
        R2StorageService storage,
        AssetStorageKeyPolicy keys,
        StoredAssetService storedAssets,
        ManagedResourceService resources,
        IOptions<R2StorageSettings> settings,
        ILogger<StorageMigrationService> logger)
    {
        _context = context;
        _storage = storage;
        _keys = keys;
        _storedAssets = storedAssets;
        _resources = resources;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<StorageInventoryDto> InventoryAsync(CancellationToken cancellationToken)
    {
        var prefix = _settings.KeyPrefix.Trim('/') + "/";
        var listed = await _storage.ListObjectsAsync(prefix, cancellationToken: cancellationToken);
        var resourceKeys = await _context.ManagedResources
            .Find(item => item.StorageKey != null && item.StorageKey != string.Empty)
            .Project(item => item.StorageKey!)
            .ToListAsync(cancellationToken);
        var registeredKeys = await _context.StoredAssets
            .Find(FilterDefinition<StoredAsset>.Empty)
            .Project(item => item.StorageKey)
            .ToListAsync(cancellationToken);
        var sessions = await _context.ResourceUploadSessions
            .Find(item => item.DeleteAfterUtc == null || item.DeleteAfterUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        var tracked = resourceKeys.Concat(registeredKeys)
            .Concat(sessions.Select(item => item.PendingStorageKey))
            .Concat(sessions.Select(item => item.FinalStorageKey ?? string.Empty))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);

        var canonicalRoots = CanonicalRoots();
        var temporaryRoot = JoinRoot(_settings.TemporaryFolder);
        var result = new StorageInventoryDto
        {
            TotalObjectCount = listed.Objects.Count,
            Truncated = listed.Truncated
        };
        foreach (var item in listed.Objects)
        {
            var canonical = canonicalRoots.Any(root => item.Key.StartsWith(root, StringComparison.Ordinal));
            var temporary = item.Key.StartsWith(temporaryRoot, StringComparison.Ordinal);
            if (canonical) result.CanonicalObjectCount++;
            else result.LegacyObjectCount++;
            if (temporary) result.TemporaryObjectCount++;
            if (!tracked.Contains(item.Key))
            {
                result.UntrackedObjectCount++;
                if (result.UntrackedObjects.Count < 500)
                {
                    result.UntrackedObjects.Add(new StorageInventoryObjectDto
                    {
                        StorageKey = item.Key,
                        SizeBytes = item.SizeBytes,
                        LastModifiedUtc = item.LastModifiedUtc
                    });
                }
            }
        }
        return result;
    }

    public async Task<StorageMigrationPlanDto> PlanAsync(
        StorageMigrationPlanRequest request,
        CancellationToken cancellationToken)
    {
        var migrationId = CleanMigrationId(request.MigrationId);
        var resources = await _context.ManagedResources
            .Find(item => item.StorageKey != null && item.StorageKey != string.Empty)
            .ToListAsync(cancellationToken);
        var resourceRoot = JoinRoot(_settings.ResourceLibraryFolder);
        var result = new StorageMigrationPlanDto { MigrationId = migrationId, DryRun = request.DryRun };
        foreach (var resource in resources)
        {
            if (resource.StorageKey!.StartsWith(resourceRoot, StringComparison.Ordinal))
            {
                result.AlreadyCanonicalCount++;
                continue;
            }

            var version = Math.Max(1, resource.AssetVersion);
            var destination = _keys.CreateResourceKey(
                resource.Id,
                version,
                resource.Kind,
                resource.Name.GetValueOrDefault("en") ?? resource.FileName,
                resource.FileName);
            var candidate = new StorageMigrationCandidateDto
            {
                ResourceId = resource.Id,
                SourceKey = resource.StorageKey,
                DestinationKey = destination
            };
            result.Candidates.Add(candidate);

            if (!request.DryRun)
            {
                var now = DateTime.UtcNow;
                var filter = Builders<StorageMigrationRecord>.Filter.Eq(item => item.MigrationId, migrationId) &
                             Builders<StorageMigrationRecord>.Filter.Eq(item => item.SourceKey, resource.StorageKey);
                var update = Builders<StorageMigrationRecord>.Update
                    .SetOnInsert(item => item.Id, ObjectId.GenerateNewId().ToString())
                    .SetOnInsert(item => item.CreatedAt, now)
                    .SetOnInsert(item => item.Status, "planned")
                    .Set(item => item.OwnerDomain, "resource-library")
                    .Set(item => item.OwnerType, "managed-resource")
                    .Set(item => item.OwnerId, resource.Id)
                    .Set(item => item.DestinationKey, destination)
                    .Set(item => item.DryRun, false)
                    .Set(item => item.UpdatedAt, now);
                await _context.StorageMigrations.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
            }
        }
        result.CandidateCount = result.Candidates.Count;
        return result;
    }

    public async Task<StorageMigrationExecuteDto> ExecuteAsync(
        StorageMigrationExecuteRequest request,
        string actorId,
        CancellationToken cancellationToken)
    {
        var migrationId = CleanMigrationId(request.MigrationId);
        var limit = Math.Clamp(request.Limit, 1, 100);
        var abandonedBefore = DateTime.UtcNow.AddMinutes(-15);
        var records = await _context.StorageMigrations
            .Find(item => item.MigrationId == migrationId &&
                          (item.Status == "planned" || item.Status == "failed" ||
                           (item.Status == "copying" && item.UpdatedAt < abandonedBefore)))
            .SortBy(item => item.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var result = new StorageMigrationExecuteDto { MigrationId = migrationId, AttemptedCount = records.Count };
        foreach (var record in records)
        {
            var itemResult = new StorageMigrationCandidateDto
            {
                ResourceId = record.OwnerId,
                SourceKey = record.SourceKey,
                DestinationKey = record.DestinationKey
            };
            try
            {
                record.Status = "copying";
                record.AttemptCount++;
                record.UpdatedAt = DateTime.UtcNow;
                record.LastError = null;
                await SaveAsync(record, cancellationToken);

                var resource = await _resources.GetByIdAsync(record.OwnerId)
                    ?? throw new InvalidOperationException("Resource not found.");
                if (!string.Equals(resource.StorageKey, record.DestinationKey, StringComparison.Ordinal))
                {
                    var source = await _storage.GetMetadataAsync(record.SourceKey, cancellationToken)
                        ?? throw new InvalidOperationException("Source object was not found.");
                    var destination = await _storage.GetMetadataAsync(record.DestinationKey, cancellationToken);
                    if (destination is null)
                    {
                        await _storage.CopyAsync(record.SourceKey, record.DestinationKey, cancellationToken);
                        destination = await _storage.GetMetadataAsync(record.DestinationKey, cancellationToken);
                    }
                    if (destination is null ||
                        destination.SizeBytes != source.SizeBytes ||
                        (!string.IsNullOrWhiteSpace(source.ContentType) &&
                         !string.Equals(destination.ContentType, source.ContentType, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Destination verification failed.");

                    var assetId = ObjectId.TryParse(resource.AssetId, out _)
                        ? resource.AssetId!
                        : ObjectId.GenerateNewId().ToString();
                    var version = Math.Max(1, resource.AssetVersion);
                    var (updated, previousKey, _, errors) = await _resources.MigrateStorageAsync(
                        resource.Id,
                        _storage.PublicUrl(record.DestinationKey),
                        record.DestinationKey,
                        assetId,
                        version,
                        _keys.SchemaVersion,
                        actorId);
                    if (updated is null) throw new InvalidOperationException(string.Join(' ', errors));

                    await _storedAssets.RecordReadyAsync(
                        assetId,
                        _keys.SchemaVersion,
                        new AssetStorageOwner("resource-library", "managed-resource", updated.Id, updated.Kind),
                        record.DestinationKey,
                        updated.Url,
                        updated.FileName,
                        updated.ContentType,
                        updated.SizeBytes,
                        actorId,
                        version,
                        updated.Id,
                        destination.ETag,
                        previousKey,
                        cancellationToken);
                }

                record.Status = "completed";
                record.CompletedAt = DateTime.UtcNow;
                record.SourceDeleteAfterUtc = record.CompletedAt.Value.AddDays(Math.Max(1, _settings.LegacyObjectRetentionDays));
                record.UpdatedAt = record.CompletedAt.Value;
                await SaveAsync(record, cancellationToken);
                itemResult.Status = "completed";
                result.CompletedCount++;
            }
            catch (Exception exception)
            {
                record.Status = "failed";
                record.LastError = "Storage migration failed: the object could not be transferred.";
                record.UpdatedAt = DateTime.UtcNow;
                await SaveAsync(record, CancellationToken.None);
                itemResult.Status = "failed";
                itemResult.Error = record.LastError;
                _logger.LogError(
                    exception,
                    "Storage migration failed for {OwnerType} {OwnerId} and record {RecordId}.",
                    record.OwnerType,
                    record.OwnerId,
                    record.Id);
                result.FailedCount++;
            }
            result.Results.Add(itemResult);
        }
        return result;
    }

    private Task SaveAsync(StorageMigrationRecord record, CancellationToken cancellationToken) =>
        _context.StorageMigrations.ReplaceOneAsync(item => item.Id == record.Id, record, cancellationToken: cancellationToken);

    private List<string> CanonicalRoots() =>
    [
        JoinRoot(_settings.TemporaryFolder),
        JoinRoot(_settings.ResourceLibraryFolder),
        JoinRoot(_settings.ContentFolder),
        JoinRoot(_settings.PageBuilderFolder),
        JoinRoot(_settings.GlobalFolder),
        JoinRoot(_settings.CustomIconFolder)
    ];

    private string JoinRoot(string folder) =>
        $"{_settings.KeyPrefix.Trim('/')}/{folder.Trim('/')}/";

    private static string CleanMigrationId(string? value)
    {
        var clean = new string((value ?? string.Empty).Trim().ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "storage-schema-v2" : clean[..Math.Min(80, clean.Length)];
    }
}
