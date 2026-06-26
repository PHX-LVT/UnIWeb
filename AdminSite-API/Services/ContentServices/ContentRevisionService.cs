using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services.AssetService;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ContentRevisionService
    {
        private readonly MongoDbContext _context;
        private readonly ContentMappingService _mapping;
        private readonly AssetCleanupService _assetCleanup;

        public ContentRevisionService(MongoDbContext context, ContentMappingService mapping, AssetCleanupService assetCleanup)
        {
            _context = context;
            _mapping = mapping;
            _assetCleanup = assetCleanup;
        }

        public async Task<List<RevisionResponseDto>> GetRevisionsAsync(string id)
        {
            var item = await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (item is null) return new();

            var revisions = await _context.ContentRevisions
                .Find(r => r.ContentStableId == item.StableId)
                .SortByDescending(r => r.CreatedAt)
                .Limit(10)
                .ToListAsync();

            return revisions.Select(_mapping.MapRevision).ToList();
        }

        public async Task<(ContentItem? Item, List<string> Errors)> RestoreRevisionAsync(string id, string revisionId, string actorId)
        {
            var current = await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (current is null) return (null, ["Content item not found."]);

            var revision = await _context.ContentRevisions
                .Find(r => r.Id == revisionId && r.ContentStableId == current.StableId)
                .FirstOrDefaultAsync();
            if (revision is null) return (null, ["Content revision not found."]);

            await SaveAsync(current, actorId, "before-restore");

            var restored = BsonSerializer.Deserialize<ContentItem>(revision.Snapshot);
            restored.Id = current.Id;
            restored.StableId = current.StableId;
            restored.CreatedAt = current.CreatedAt;
            restored.UpdatedAt = DateTime.UtcNow;
            restored.UpdatedById = actorId;

            await _context.ContentDraft.ReplaceOneAsync(c => c.Id == id, restored);
            await _assetCleanup.DeleteUnusedAsync(_assetCleanup.RemovedAssetUrls(
                _assetCleanup.ContentAssetUrls(current),
                _assetCleanup.ContentAssetUrls(restored)));
            await TrimAsync(current.StableId);
            await LogAsync(current.StableId, "revision-restored", actorId);
            return (await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync(), []);
        }

        public async Task SaveAsync(ContentItem item, string actorId, string reason)
        {
            await _context.ContentRevisions.InsertOneAsync(new ContentRevision
            {
                ContentId = item.Id,
                ContentStableId = item.StableId,
                SourceUpdatedAt = item.UpdatedAt,
                ActorId = actorId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
                Snapshot = item.ToBsonDocument()
            });

            await TrimAsync(item.StableId);
        }

        public async Task TrimAsync(string contentStableId)
        {
            var staleIds = await _context.ContentRevisions
                .Find(r => r.ContentStableId == contentStableId)
                .SortByDescending(r => r.CreatedAt)
                .Skip(2)
                .Project(r => r.Id)
                .ToListAsync();

            if (staleIds.Count > 0)
                await _context.ContentRevisions.DeleteManyAsync(r => staleIds.Contains(r.Id));
        }

        public async Task<List<ContentAuditLog>> GetLogsAsync(string stableId) =>
            await _context.ContentAuditLogs.Find(l => l.ContentStableId == stableId)
                .SortByDescending(l => l.CreatedAt)
                .ToListAsync();

        public async Task LogAsync(string stableId, string action, string actorId, string? message = null)
        {
            await _context.ContentAuditLogs.InsertOneAsync(new ContentAuditLog
            {
                ContentStableId = stableId,
                Action = action,
                ActorId = actorId,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task LogAsync(IClientSessionHandle session, string stableId, string action, string actorId, string? message = null)
        {
            await _context.ContentAuditLogs.InsertOneAsync(session, new ContentAuditLog
            {
                ContentStableId = stableId,
                Action = action,
                ActorId = actorId,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
