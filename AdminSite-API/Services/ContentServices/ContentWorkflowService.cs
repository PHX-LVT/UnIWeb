using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services.AssetService;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ContentWorkflowService
    {
        private readonly MongoDbContext _context;
        private readonly ContentValidationService _validation;
        private readonly ContentRevisionService _revisions;
        private readonly ContentMappingService _mapping;
        private readonly AssetCleanupService _assetCleanup;

        public ContentWorkflowService(
            MongoDbContext context,
            ContentValidationService validation,
            ContentRevisionService revisions,
            ContentMappingService mapping,
            AssetCleanupService assetCleanup)
        {
            _context = context;
            _validation = validation;
            _revisions = revisions;
            _mapping = mapping;
            _assetCleanup = assetCleanup;
        }

        public async Task<(ContentItem? Item, List<string> Errors)> SetStatusAsync(string id, ContentStatusUpdateDto dto, string actorId)
        {
            var item = await GetByIdAsync(id);
            if (item is null) return (null, ["Content item not found."]);

            var updates = new List<UpdateDefinition<ContentItem>>
            {
                Builders<ContentItem>.Update.Set(c => c.Status, dto.Status),
                Builders<ContentItem>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow),
                Builders<ContentItem>.Update.Set(c => c.UpdatedById, actorId)
            };

            if (dto.Status == ContentStatus.Submitted)
                updates.Add(Builders<ContentItem>.Update.Set(c => c.SubmittedAt, DateTime.UtcNow));

            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id, Builders<ContentItem>.Update.Combine(updates));
            await _revisions.LogAsync(item.StableId, dto.Status.ToString().ToLowerInvariant(), actorId, dto.Message);
            return (await GetByIdAsync(id), []);
        }

        public async Task<(ContentItem? Item, List<string> Errors)> PublishAsync(string id, string actorId)
        {
            var item = await GetByIdAsync(id);
            if (item is null) return (null, ["Content item not found."]);

            var validation = await _validation.ValidatePublishAsync(item);
            if (validation.Count > 0) return (null, validation);

            var now = DateTime.UtcNow;
            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id,
                Builders<ContentItem>.Update
                    .Set(c => c.Status, ContentStatus.Published)
                    .Set(c => c.PublishedAt, now)
                    .Set(c => c.PublishedById, actorId)
                    .Set(c => c.UpdatedById, actorId)
                    .Set(c => c.UpdatedAt, now));

            var updated = await GetByIdAsync(id);
            if (updated is null) return (null, ["Content item not found after update."]);

            var replacedPublishedItems = await _context.ContentPublished
                .Find(c => c.StableId == item.StableId)
                .ToListAsync();

            var published = _mapping.CloneForPublished(updated, actorId, now);
            await _context.ContentPublished.DeleteManyAsync(c => c.StableId == item.StableId);
            await _context.ContentPublished.InsertOneAsync(published);
            await _assetCleanup.DeleteUnusedContentAssetsAsync(replacedPublishedItems);
            await _revisions.LogAsync(item.StableId, "published", actorId);
            return (updated, []);
        }

        public async Task<bool> DeleteAsync(string id, string actorId)
        {
            var item = await GetByIdAsync(id);
            if (item is null) return false;

            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id,
                Builders<ContentItem>.Update
                    .Set(c => c.Status, ContentStatus.Deleted)
                    .Set(c => c.Visible, false)
                    .Set(c => c.UpdatedById, actorId)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow));
            await _context.ContentPublished.DeleteManyAsync(c => c.StableId == item.StableId);
            await _revisions.LogAsync(item.StableId, "deleted", actorId);
            return true;
        }

        public async Task<(ContentItem? Item, List<string> Errors)> RestoreAsync(string id, string actorId)
        {
            var item = await GetByIdAsync(id);
            if (item is null) return (null, ["Content item not found."]);
            if (item.Status != ContentStatus.Deleted) return (null, ["Only deleted content can be restored."]);

            var now = DateTime.UtcNow;
            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id,
                Builders<ContentItem>.Update
                    .Set(c => c.Status, ContentStatus.Draft)
                    .Set(c => c.Visible, true)
                    .Set(c => c.UpdatedById, actorId)
                    .Set(c => c.UpdatedAt, now));

            await _revisions.LogAsync(item.StableId, "restored", actorId);
            return (await GetByIdAsync(id), []);
        }

        public async Task<int> PermanentDeleteAsync(IEnumerable<string> ids)
        {
            var normalizedIds = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedIds.Count == 0) return 0;

            var items = await _context.ContentDraft.Find(c =>
                    normalizedIds.Contains(c.Id) &&
                    c.Status == ContentStatus.Deleted)
                .ToListAsync();

            if (items.Count == 0) return 0;

            var stableIds = items.Select(i => i.StableId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            await _context.ContentDraft.DeleteManyAsync(c => normalizedIds.Contains(c.Id) && c.Status == ContentStatus.Deleted);
            await _context.ContentPublished.DeleteManyAsync(c => stableIds.Contains(c.StableId));
            await _context.ContentRevisions.DeleteManyAsync(r => stableIds.Contains(r.ContentStableId));
            await _context.ContentAuditLogs.DeleteManyAsync(l => stableIds.Contains(l.ContentStableId));
            await _assetCleanup.DeleteUnusedContentAssetsAsync(items);
            return items.Count;
        }

        private async Task<ContentItem?> GetByIdAsync(string id) =>
            await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync();

    }
}
