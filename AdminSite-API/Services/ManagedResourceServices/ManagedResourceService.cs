using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using GlobalManager.Services.AssetService;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ManagedResourceService
    {
        private const string ManagedResourceSource = "ManagedResource";

        private readonly MongoDbContext _context;
        private readonly ManagedResourceValidationService _validation;
        private readonly ManagedResourceUsageService _usage;
        private readonly ManagedResourceAlbumService _albums;
        private readonly AssetCleanupService _assetCleanup;

        public ManagedResourceService(
            MongoDbContext context,
            ManagedResourceValidationService validation,
            ManagedResourceUsageService usage,
            ManagedResourceAlbumService albums,
            AssetCleanupService assetCleanup)
        {
            _context = context;
            _validation = validation;
            _usage = usage;
            _albums = albums;
            _assetCleanup = assetCleanup;
        }

        public async Task<List<ManagedResource>> GetAllAsync(string? kind = null, string? search = null, bool includeInactive = false, string? albumId = null)
        {
            var filter = Builders<ManagedResource>.Filter.Empty;
            var normalizedKind = _validation.NormalizeKind(kind, allowEmpty: true);
            if (!string.IsNullOrWhiteSpace(normalizedKind))
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.Kind, normalizedKind);
            if (!string.IsNullOrWhiteSpace(albumId))
            {
                var cleanAlbumId = albumId.Trim();
                if (!ObjectId.TryParse(cleanAlbumId, out _)) return [];
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.AlbumId, cleanAlbumId);
            }
            if (!includeInactive)
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.Active, true);

            var resources = await _context.ManagedResources.Find(filter)
                .SortByDescending(r => r.UpdatedAt)
                .ToListAsync();

            if (string.IsNullOrWhiteSpace(search))
                return resources;

            var term = search.Trim();
            return resources.Where(r => _validation.MatchesSearch(r, term)).ToList();
        }

        public async Task<ManagedResource?> GetByIdAsync(string id) =>
            await _context.ManagedResources.Find(r => r.Id == id).FirstOrDefaultAsync();

        public async Task<(ManagedResource? Resource, List<string> Errors)> CreateAsync(ManagedResourceCreateDto dto, string actorId)
        {
            var (resource, errors) = _validation.BuildResource(dto, actorId);
            if (resource is not null)
                await _validation.AddAlbumAssignmentErrorsAsync(resource, errors);
            if (errors.Count > 0) return (null, errors);

            await _context.ManagedResources.InsertOneAsync(resource!);
            return (resource, errors);
        }

        public async Task<(ManagedResource? Resource, List<string> Errors)> UpdateAsync(string id, ManagedResourceUpdateDto dto, string actorId)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (null, ["Resource not found."]);

            var errors = _validation.ApplyUpdate(resource, dto, actorId);
            await _validation.AddAlbumAssignmentErrorsAsync(resource, errors);
            if (errors.Count > 0) return (null, errors);

            await _context.ManagedResources.ReplaceOneAsync(r => r.Id == id, resource);
            return (resource, errors);
        }

        public async Task<(ManagedResource? Resource, int UpdatedDocuments, List<string> Errors)> ReplaceUploadAsync(
            string id,
            string url,
            string storageKey,
            string kind,
            string fileName,
            string contentType,
            long sizeBytes,
            string actorId)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (null, 0, ["Resource not found."]);

            var normalizedKind = _validation.NormalizeKind(kind);
            if (normalizedKind is null)
                return (null, 0, ["Resource kind must be image, file, or video."]);
            if (!string.Equals(resource.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase))
                return (null, 0, [$"Replacement file must be a {resource.Kind} resource."]);

            var oldUrl = resource.Url;
            var errors = _validation.ApplyUploadReplacement(resource, url, storageKey, fileName, contentType, sizeBytes, actorId);
            if (errors.Count > 0) return (null, 0, errors);

            await _context.ManagedResources.ReplaceOneAsync(r => r.Id == id, resource);
            var updatedDocuments = await PropagateUploadReplacementAsync(resource, oldUrl);
            await _assetCleanup.DeleteIfUnusedAsync(oldUrl, resource.Url);
            return (resource, updatedDocuments, errors);
        }

        public Task<Dictionary<string, int>> GetUsageCountsAsync(IEnumerable<ManagedResource> resources) =>
            _usage.GetUsageCountsAsync(resources);

        public async Task<(int UpdatedCount, List<string> Errors)> AssignToAlbumAsync(string albumId, IEnumerable<string>? resourceIds, string actorId)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(albumId) || !ObjectId.TryParse(albumId.Trim(), out _))
                return (0, ["Album not found."]);

            var album = await _albums.GetByIdAsync(albumId.Trim());
            if (album is null)
                return (0, ["Album not found."]);

            var ids = (resourceIds ?? [])
                .Select(id => id?.Trim() ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0)
                return (0, ["Choose at least one resource."]);

            if (ids.Any(id => !ObjectId.TryParse(id, out _)))
                return (0, ["One or more resources were not found."]);

            var resources = await _context.ManagedResources.Find(r => ids.Contains(r.Id)).ToListAsync();
            var foundIds = resources.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ids.Any(id => !foundIds.Contains(id)))
                errors.Add("One or more resources were not found.");

            foreach (var resource in resources)
            {
                var expectedScope = ManagedResourceAlbumService.ScopeForKind(resource.Kind);
                if (!string.Equals(album.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
                {
                    var resourceName = resource.Name.GetValueOrDefault("en") ?? resource.FileName ?? resource.Id;
                    errors.Add($"{resourceName} does not match this album type.");
                }
            }

            if (errors.Count > 0)
                return (0, errors);

            var now = DateTime.UtcNow;
            var result = await _context.ManagedResources.UpdateManyAsync(
                r => ids.Contains(r.Id),
                Builders<ManagedResource>.Update
                    .Set(r => r.AlbumId, album.Id)
                    .Set(r => r.UpdatedById, actorId)
                    .Set(r => r.UpdatedAt, now));

            return ((int)result.ModifiedCount, []);
        }

        public async Task<ManagedResourceUsageDto> GetUsageAsync(string id)
        {
            var resource = await GetByIdAsync(id);
            return resource is null
                ? new ManagedResourceUsageDto { ResourceId = id }
                : await _usage.GetUsageAsync(resource);
        }

        public async Task<(bool Deleted, ManagedResourceUsageDto? Usage, List<string> Errors)> DeleteAsync(string id)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (false, null, ["Resource not found."]);

            var usage = await _usage.GetUsageAsync(resource);
            if (usage.UsageCount > 0)
            {
                var plural = usage.UsageCount == 1 ? string.Empty : "s";
                return (false, usage, [$"Resource is used in {usage.UsageCount} place{plural}. Remove those references before deleting."]);
            }

            var result = await _context.ManagedResources.DeleteOneAsync(r => r.Id == resource.Id);
            if (result.DeletedCount <= 0)
                return (false, usage, ["Resource not found."]);

            await _assetCleanup.DeleteUnusedAsync([resource.Url, resource.ThumbnailUrl]);
            return (true, usage, []);
        }

        public ManagedResourceCreateDto BuildUploadCreateDto(string url, string storageKey, string kind, string fileName, string contentType, long sizeBytes, string? albumId = null) =>
            _validation.BuildUploadCreateDto(url, storageKey, kind, fileName, contentType, sizeBytes, albumId);

        public string? NormalizeKind(string? value, bool allowEmpty = false) =>
            _validation.NormalizeKind(value, allowEmpty);

        public static string InferKindFromUpload(string fileName, string? contentType) =>
            ManagedResourceValidationService.InferKindFromUpload(fileName, contentType);

        private async Task<int> PropagateUploadReplacementAsync(ManagedResource resource, string oldUrl)
        {
            var updated = 0;
            updated += await PropagateContentReplacementAsync(_context.ContentDraft, resource, oldUrl);
            updated += await PropagateContentReplacementAsync(_context.ContentPublished, resource, oldUrl);
            updated += await PropagatePageReplacementAsync(_context.PagesDraft, resource, oldUrl);
            updated += await PropagatePageReplacementAsync(_context.PagesPublished, resource, oldUrl);
            updated += await PropagateSectionReplacementAsync(_context.SectionsDraft, resource, oldUrl);
            updated += await PropagateSectionReplacementAsync(_context.SectionsPublished, resource, oldUrl);
            updated += await PropagateBlockReplacementAsync(_context.BlocksDraft, resource, oldUrl);
            updated += await PropagateBlockReplacementAsync(_context.BlocksPublished, resource, oldUrl);
            updated += await PropagateBrandingReplacementAsync(resource, oldUrl);
            return updated;
        }

        private async Task<int> PropagateContentReplacementAsync(
            IMongoCollection<ContentItem> collection,
            ManagedResource resource,
            string oldUrl)
        {
            var items = await collection.Find(ContentReplacementFilter(resource, oldUrl)).ToListAsync();
            var updated = 0;
            foreach (var item in items)
            {
                if (!ReplaceContentReferences(item, resource, oldUrl)) continue;

                item.UpdatedAt = DateTime.UtcNow;
                await collection.ReplaceOneAsync(i => i.Id == item.Id, item);
                updated++;
            }

            return updated;
        }

        private async Task<int> PropagatePageReplacementAsync(
            IMongoCollection<Page> collection,
            ManagedResource resource,
            string oldUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return 0;

            var pages = await collection
                .Find(p => p.Card != null && p.Card.CardImageUrl == oldUrl)
                .ToListAsync();
            var updated = 0;
            foreach (var page in pages)
            {
                if (page.Card is null || !ManagedResourceReferenceHelper.SameUrl(page.Card.CardImageUrl, oldUrl)) continue;

                page.Card.CardImageUrl = resource.Url;
                page.UpdatedAt = DateTime.UtcNow;
                await collection.ReplaceOneAsync(p => p.Id == page.Id, page);
                updated++;
            }

            return updated;
        }

        private async Task<int> PropagateSectionReplacementAsync(
            IMongoCollection<Section> collection,
            ManagedResource resource,
            string oldUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return 0;

            var sections = await collection.Find(SectionReplacementFilter(oldUrl)).ToListAsync();
            var updated = 0;
            foreach (var section in sections)
            {
                if (!ReplaceSectionReferences(section, resource, oldUrl)) continue;

                section.UpdatedAt = DateTime.UtcNow;
                await collection.ReplaceOneAsync(s => s.Id == section.Id, section);
                updated++;
            }

            return updated;
        }

        private async Task<int> PropagateBlockReplacementAsync(
            IMongoCollection<Block> collection,
            ManagedResource resource,
            string oldUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return 0;

            var blocks = await collection.Find(BlockReplacementFilter(oldUrl)).ToListAsync();
            var updated = 0;
            foreach (var block in blocks)
            {
                if (!ReplaceBlockReferences(block, resource, oldUrl)) continue;

                block.UpdatedAt = DateTime.UtcNow;
                await collection.ReplaceOneAsync(b => b.Id == block.Id, block);
                updated++;
            }

            return updated;
        }

        private async Task<int> PropagateBrandingReplacementAsync(ManagedResource resource, string oldUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return 0;

            var brands = await _context.Branding.Find(b => b.LogoUrl == oldUrl).ToListAsync();
            var updated = 0;
            foreach (var brand in brands)
            {
                if (!ManagedResourceReferenceHelper.SameUrl(brand.LogoUrl, oldUrl)) continue;

                brand.LogoUrl = resource.Url;
                await _context.Branding.ReplaceOneAsync(b => b.Id == brand.Id, brand);
                updated++;
            }

            return updated;
        }

        private static bool ReplaceContentReferences(ContentItem item, ManagedResource resource, string oldUrl)
        {
            var changed = false;

            if (ManagedResourceReferenceHelper.MatchesManagedReference(item.HeroImageResourceId, item.HeroImageUrl, resource, oldUrl))
            {
                item.HeroImageUrl = resource.Url;
                item.HeroImageResourceId = resource.Id;
                item.HeroImageResourceSource = ManagedResourceSource;
                item.HeroImageStorageKey = resource.StorageKey;
                changed = true;
            }

            if (ManagedResourceReferenceHelper.MatchesManagedReference(item.ThumbnailResourceId, item.ThumbnailUrl, resource, oldUrl))
            {
                item.ThumbnailUrl = ThumbnailReplacementUrl(resource);
                item.ThumbnailResourceId = resource.Id;
                item.ThumbnailResourceSource = ManagedResourceSource;
                item.ThumbnailStorageKey = resource.StorageKey;
                changed = true;
            }

            if (ManagedResourceReferenceHelper.MatchesManagedReference(item.VideoResourceId, item.VideoUrl, resource, oldUrl))
            {
                item.VideoUrl = resource.Url;
                item.VideoResourceId = resource.Id;
                item.VideoResourceSource = ManagedResourceSource;
                item.VideoStorageKey = resource.StorageKey;
                changed = true;
            }

            foreach (var attachment in item.Attachments.Where(a => ManagedResourceReferenceHelper.MatchesManagedReference(a.ResourceId, a.Url, resource, oldUrl)))
            {
                attachment.Url = resource.Url;
                attachment.ResourceId = resource.Id;
                attachment.ResourceSource = ManagedResourceSource;
                attachment.StorageKey = resource.StorageKey;
                attachment.FileName = resource.FileName;
                attachment.ContentType = resource.ContentType;
                attachment.SizeBytes = resource.SizeBytes;
                changed = true;
            }

            foreach (var bodyItem in item.BodyItems.Where(b => ManagedResourceReferenceHelper.MatchesManagedReference(b.ResourceId, b.Url, resource, oldUrl)))
            {
                bodyItem.Url = resource.Url;
                bodyItem.ResourceId = resource.Id;
                bodyItem.ResourceSource = ManagedResourceSource;
                bodyItem.StorageKey = resource.StorageKey;
                bodyItem.FileName = resource.FileName;
                bodyItem.ContentType = resource.ContentType;
                bodyItem.SizeBytes = resource.SizeBytes;
                changed = true;
            }

            foreach (var galleryItem in item.GalleryItems.Where(g => ManagedResourceReferenceHelper.MatchesManagedReference(g.ResourceId, g.Url, resource, oldUrl)))
            {
                galleryItem.Url = resource.Url;
                galleryItem.ResourceId = resource.Id;
                galleryItem.ResourceSource = ManagedResourceSource;
                galleryItem.StorageKey = resource.StorageKey;
                if (string.Equals(resource.Kind, "image", StringComparison.OrdinalIgnoreCase) ||
                    ManagedResourceReferenceHelper.SameUrl(galleryItem.ThumbnailUrl, oldUrl))
                {
                    galleryItem.ThumbnailUrl = ThumbnailReplacementUrl(resource);
                }
                changed = true;
            }

            return changed;
        }

        private static bool ReplaceSectionReferences(Section section, ManagedResource resource, string oldUrl)
        {
            var changed = false;
            if (ManagedResourceReferenceHelper.SameUrl(section.Style.BackgroundImageUrl, oldUrl))
            {
                section.Style.BackgroundImageUrl = resource.Url;
                changed = true;
            }
            if (ManagedResourceReferenceHelper.SameUrl(section.Style.BackgroundVideoUrl, oldUrl))
            {
                section.Style.BackgroundVideoUrl = resource.Url;
                changed = true;
            }

            switch (section)
            {
                case HeroSection hero when ManagedResourceReferenceHelper.SameUrl(hero.ImageUrl, oldUrl):
                    hero.ImageUrl = resource.Url;
                    changed = true;
                    break;
                case ListSection list:
                    foreach (var item in list.Items.Where(i => ManagedResourceReferenceHelper.SameUrl(i.ImageUrl, oldUrl)))
                    {
                        item.ImageUrl = resource.Url;
                        changed = true;
                    }
                    break;
                case CarouselSection carousel:
                    foreach (var item in carousel.Items.Where(i => ManagedResourceReferenceHelper.SameUrl(i.ImageUrl, oldUrl)))
                    {
                        item.ImageUrl = resource.Url;
                        changed = true;
                    }
                    break;
                case TestimonialSection testimonial:
                    foreach (var item in testimonial.Items.Where(i => ManagedResourceReferenceHelper.SameUrl(i.ImageUrl, oldUrl)))
                    {
                        item.ImageUrl = resource.Url;
                        changed = true;
                    }
                    break;
                case ShowcaseSection showcase:
                    foreach (var item in showcase.ItemOverrides.Where(i => ManagedResourceReferenceHelper.SameUrl(i.CardImageUrl, oldUrl)))
                    {
                        item.CardImageUrl = resource.Url;
                        changed = true;
                    }
                    break;
            }

            return changed;
        }

        private static bool ReplaceBlockReferences(Block block, ManagedResource resource, string oldUrl)
        {
            switch (block)
            {
                case ImageBlock image when ManagedResourceReferenceHelper.SameUrl(image.ImageUrl, oldUrl):
                    image.ImageUrl = resource.Url;
                    return true;
                case FileBlock file when ManagedResourceReferenceHelper.SameUrl(file.FileUrl, oldUrl):
                    file.FileUrl = resource.Url;
                    file.Filename = resource.FileName;
                    file.FileType = resource.ContentType;
                    return true;
                case VideoBlock video when ManagedResourceReferenceHelper.SameUrl(video.EmbedUrl, oldUrl):
                    video.EmbedUrl = resource.Url;
                    return true;
                case CardBlock card when ManagedResourceReferenceHelper.SameUrl(card.ImageUrl, oldUrl):
                    card.ImageUrl = resource.Url;
                    return true;
                default:
                    return false;
            }
        }

        private static FilterDefinition<ContentItem> ContentReplacementFilter(ManagedResource resource, string oldUrl) =>
            ManagedResourceReferenceHelper.ContentFilter(resource, oldUrl);

        private static FilterDefinition<Section> SectionReplacementFilter(string oldUrl) =>
            ManagedResourceReferenceHelper.SectionUrlFilter(oldUrl);

        private static FilterDefinition<Block> BlockReplacementFilter(string oldUrl) =>
            ManagedResourceReferenceHelper.BlockUrlFilter([oldUrl]);

        private static string ThumbnailReplacementUrl(ManagedResource resource) =>
            string.Equals(resource.Kind, "image", StringComparison.OrdinalIgnoreCase)
                ? resource.Url
                : resource.ThumbnailUrl ?? resource.Url;
    }
}
