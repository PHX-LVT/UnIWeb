using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services.AssetService;
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

        public async Task<List<ManagedResource>> GetAllAsync(string? kind = null, string? search = null, bool includeInactive = false, string? albumId = null, string? purpose = null)
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
            if (!string.IsNullOrWhiteSpace(purpose))
                filter &= Builders<ManagedResource>.Filter.Eq(r => r.Purpose, purpose.Trim().ToLowerInvariant());
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

        public async Task<(ManagedResource? Resource, List<string> Errors)> CreateUploadedAsync(
            ManagedResourceCreateDto dto,
            string actorId,
            string resourceId,
            string assetId,
            int assetVersion,
            int storageSchemaVersion,
            string? originContext = null)
        {
            if (!ObjectId.TryParse(resourceId, out _) || !ObjectId.TryParse(assetId, out _))
                return (null, ["The reserved resource identity is invalid."]);

            var (resource, errors) = _validation.BuildResource(dto, actorId);
            if (resource is not null)
            {
                resource.Id = resourceId;
                resource.AssetId = assetId;
                resource.AssetVersion = Math.Max(1, assetVersion);
                resource.StorageSchemaVersion = Math.Max(1, storageSchemaVersion);
                resource.OriginContext = string.IsNullOrWhiteSpace(originContext)
                    ? null
                    : originContext.Trim().ToLowerInvariant();
                await _validation.AddAlbumAssignmentErrorsAsync(resource, errors);
            }
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
            string actorId,
            string? assetId = null,
            int assetVersion = 0,
            int storageSchemaVersion = 0,
            bool deferOldAssetCleanup = false)
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

            if (!string.IsNullOrWhiteSpace(assetId)) resource.AssetId = assetId;
            if (assetVersion > 0) resource.AssetVersion = assetVersion;
            if (storageSchemaVersion > 0) resource.StorageSchemaVersion = storageSchemaVersion;

            await _context.ManagedResources.ReplaceOneAsync(r => r.Id == id, resource);
            var updatedDocuments = await PropagateUploadReplacementAsync(resource, oldUrl);
            if (!deferOldAssetCleanup)
                await _assetCleanup.DeleteIfUnusedAsync(oldUrl, resource.Url);
            return (resource, updatedDocuments, errors);
        }

        public async Task<(ManagedResource? Resource, string? PreviousStorageKey, int UpdatedDocuments, List<string> Errors)> MigrateStorageAsync(
            string id,
            string url,
            string storageKey,
            string assetId,
            int assetVersion,
            int storageSchemaVersion,
            string actorId)
        {
            var resource = await GetByIdAsync(id);
            if (resource is null) return (null, null, 0, ["Resource not found."]);
            if (!ObjectId.TryParse(assetId, out _)) return (null, null, 0, ["Asset identity is invalid."]);

            var oldUrl = resource.Url;
            var oldStorageKey = resource.StorageKey;
            resource.Url = url;
            resource.StorageKey = storageKey;
            resource.AssetId = assetId;
            resource.AssetVersion = Math.Max(1, assetVersion);
            resource.StorageSchemaVersion = Math.Max(1, storageSchemaVersion);
            resource.UpdatedById = actorId;
            resource.UpdatedAt = DateTime.UtcNow;
            await _context.ManagedResources.ReplaceOneAsync(item => item.Id == resource.Id, resource);
            var updatedDocuments = string.Equals(oldUrl, url, StringComparison.OrdinalIgnoreCase)
                ? 0
                : await PropagateUploadReplacementAsync(resource, oldUrl);
            return (resource, oldStorageKey, updatedDocuments, []);
        }

        public Task<Dictionary<string, int>> GetUsageCountsAsync(IEnumerable<ManagedResource> resources) =>
            _usage.GetUsageCountsAsync(resources);

        public Task<int> ReconcileUploadReplacementAsync(ManagedResource resource, string? previousUrl)
        {
            if (string.IsNullOrWhiteSpace(previousUrl) ||
                string.Equals(previousUrl, resource.Url, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(0);
            }

            return PropagateUploadReplacementAsync(resource, previousUrl);
        }

        public async Task<(int UpdatedCount, List<string> Errors)> AssignToAlbumAsync(string albumId, IEnumerable<string>? resourceIds, string actorId)
            => await MoveToAlbumAsync(albumId, resourceIds, actorId);

        public async Task<(int UpdatedCount, List<string> Errors)> MoveToAlbumAsync(string? albumId, IEnumerable<string>? resourceIds, string actorId)
        {
            var errors = new List<string>();
            ResourceAlbum? album = null;
            if (!string.IsNullOrWhiteSpace(albumId))
            {
                if (!ObjectId.TryParse(albumId.Trim(), out _)) return (0, ["Album not found."]);
                album = await _albums.GetByIdAsync(albumId.Trim());
                if (album is null) return (0, ["Album not found."]);
            }

            var ids = (resourceIds ?? [])
                .Select(id => id?.Trim() ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0)
                return (0, ["Choose at least one resource."]);
            if (ids.Count > 100)
                return (0, ["Move no more than 100 resources at a time."]);

            if (ids.Any(id => !ObjectId.TryParse(id, out _)))
                return (0, ["One or more resources were not found."]);

            var resources = await _context.ManagedResources.Find(r => ids.Contains(r.Id)).ToListAsync();
            var foundIds = resources.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ids.Any(id => !foundIds.Contains(id)))
                errors.Add("One or more resources were not found.");

            foreach (var resource in resources.Where(_ => album is not null))
            {
                var expectedScope = ManagedResourceAlbumService.ScopeForKind(resource.Kind);
                if (!string.Equals(album!.Scope, expectedScope, StringComparison.OrdinalIgnoreCase))
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
                    .Set(r => r.AlbumId, album?.Id)
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

            var isCustomIcon = string.Equals(resource.Purpose, "custom-icon", StringComparison.OrdinalIgnoreCase);
            if (isCustomIcon)
            {
                resource.DeletionState = "pending";
                resource.UpdatedAt = DateTime.UtcNow;
                await _context.ManagedResources.ReplaceOneAsync(r => r.Id == resource.Id, resource);

                var storageDeleted = await _assetCleanup.DeleteIfUnusedAsync(resource.Url, null, resource.Id);
                if (!storageDeleted)
                {
                    resource.DeletionState = "failed";
                    resource.UpdatedAt = DateTime.UtcNow;
                    await _context.ManagedResources.ReplaceOneAsync(r => r.Id == resource.Id, resource);
                    return (false, usage, ["Custom Icon storage deletion failed. The catalogue record was retained so deletion can be retried safely."]);
                }
            }

            var result = await _context.ManagedResources.DeleteOneAsync(r => r.Id == resource.Id);
            if (result.DeletedCount <= 0)
                return (false, usage, ["Resource not found."]);

            if (!isCustomIcon)
                await _assetCleanup.DeleteUnusedAsync([resource.Url, resource.ThumbnailUrl]);
            return (true, usage, []);
        }

        public async Task<(ResourceAlbumDeleteResultDto Result, ResourceAlbum? Album, List<string> Errors)> DeleteAlbumWithResourcesAsync(string id)
        {
            var result = new ResourceAlbumDeleteResultDto { AlbumId = id };
            var album = await _albums.GetByIdAsync(id);
            if (album is null) return (result, null, ["Album not found."]);
            if (album.IsSystemRoot)
                return (result, album, ["System root albums cannot be deleted."]);

            var resources = await _context.ManagedResources
                .Find(resource => resource.AlbumId == id)
                .ToListAsync();
            result.ResourceCount = resources.Count;

            var usageCounts = await _usage.GetUsageCountsAsync(resources);
            var blocked = resources
                .Where(resource => usageCounts.GetValueOrDefault(resource.Id) > 0)
                .ToList();
            result.BlockedResourceCount = blocked.Count;
            if (blocked.Count > 0)
            {
                var names = blocked
                    .Take(5)
                    .Select(resource => resource.Name.GetValueOrDefault("en") ?? resource.FileName ?? resource.Id)
                    .Where(name => !string.IsNullOrWhiteSpace(name));
                var examples = string.Join(", ", names);
                var detail = string.IsNullOrWhiteSpace(examples) ? string.Empty : $" Used resources include: {examples}.";
                return (result, album,
                [
                    $"Album cannot be deleted because {blocked.Count} contained resource(s) are used elsewhere in the project.{detail}"
                ]);
            }

            var errors = new List<string>();
            foreach (var resource in resources)
            {
                var (deleted, usage, resourceErrors) = await DeleteAsync(resource.Id);
                if (deleted)
                {
                    result.DeletedResourceCount++;
                    continue;
                }

                if ((usage?.UsageCount ?? 0) > 0)
                    result.BlockedResourceCount++;
                errors.AddRange(resourceErrors.Select(error => $"{resource.Name.GetValueOrDefault("en") ?? resource.FileName ?? resource.Id}: {error}"));
            }

            if (errors.Count > 0)
                return (result, album, errors);

            var (albumDeleted, albumErrors) = await _albums.DeleteRecordAsync(id);
            result.Deleted = albumDeleted;
            return (result, album, albumErrors);
        }

        public ManagedResourceCreateDto BuildUploadCreateDto(string url, string storageKey, string kind, string fileName, string contentType, long sizeBytes, string? albumId = null, string? purpose = null) =>
            _validation.BuildUploadCreateDto(url, storageKey, kind, fileName, contentType, sizeBytes, albumId, purpose);

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
            updated += await PropagateSectionPresetReplacementAsync(resource, oldUrl);
            updated += await PropagateBrandingReplacementAsync(resource, oldUrl);
            updated += await PropagateSocialReplacementAsync(resource, oldUrl);
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

            var sections = await collection.Find(SectionReplacementFilter(resource, oldUrl)).ToListAsync();
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

            var blocks = await collection.Find(BlockReplacementFilter(resource, oldUrl)).ToListAsync();
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

        private async Task<int> PropagateSectionPresetReplacementAsync(ManagedResource resource, string oldUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return 0;

            var presets = await _context.SectionPresets.Find(SectionPresetReplacementFilter(oldUrl)).ToListAsync();
            var updated = 0;
            foreach (var preset in presets)
            {
                var changed = false;
                if (ManagedResourceReferenceHelper.SameUrl(preset.ThumbnailUrl, oldUrl))
                {
                    preset.ThumbnailUrl = ThumbnailReplacementUrl(resource);
                    changed = true;
                }
                if (preset.Section is not null && ReplaceSectionReferences(preset.Section, resource, oldUrl))
                {
                    changed = true;
                }

                foreach (var block in preset.Blocks)
                    changed |= ReplaceBlockReferences(block, resource, oldUrl);

                if (!changed) continue;
                preset.UpdatedAt = DateTime.UtcNow;
                await _context.SectionPresets.ReplaceOneAsync(item => item.Id == preset.Id, preset);
                updated++;
            }

            return updated;
        }

        private async Task<int> PropagateSocialReplacementAsync(ManagedResource resource, string oldUrl)
        {
            var groups = await _context.SocialButtons.Find(Builders<SocialButtonGroup>.Filter.Or(
                Builders<SocialButtonGroup>.Filter.Eq("Buttons.IconVisual.ResourceId", resource.Id),
                Builders<SocialButtonGroup>.Filter.Eq("Buttons.IconVisual.Url", oldUrl))).ToListAsync();
            var updated = 0;
            foreach (var group in groups)
            {
                var changed = false;
                foreach (var button in group.Buttons)
                    changed |= ReplaceIconReference(button.IconVisual, resource, oldUrl);
                if (!changed) continue;
                await _context.SocialButtons.ReplaceOneAsync(item => item.Id == group.Id, group);
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
                    foreach (var item in list.Items)
                        changed |= ReplaceIconReference(item.IconVisual, resource, oldUrl);
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
                    foreach (var item in testimonial.Items)
                        changed |= ReplaceIconReference(item.IconVisual, resource, oldUrl);
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
            var changed = false;
            switch (block)
            {
                case ImageBlock image when ManagedResourceReferenceHelper.SameUrl(image.Asset.Url, oldUrl):
                    image.Asset.Url = resource.Url;
                    changed = true;
                    break;
                case FileBlock file when ManagedResourceReferenceHelper.SameUrl(file.Asset.Url, oldUrl):
                    file.Asset.Url = resource.Url;
                    file.Filename = resource.FileName;
                    file.Asset.ContentType = resource.ContentType;
                    changed = true;
                    break;
                case VideoBlock video when ManagedResourceReferenceHelper.SameUrl(video.Asset.Url, oldUrl):
                    video.Asset.Url = resource.Url;
                    changed = true;
                    break;
                case CardBlock card when ManagedResourceReferenceHelper.SameUrl(card.Asset.Url, oldUrl):
                    card.Asset.Url = resource.Url;
                    changed = true;
                    break;
            }

            changed |= ReplaceIconReference(BlockIcon(block), resource, oldUrl);
            if (block is BulletListBlock bullet)
                foreach (var item in bullet.Items)
                    changed |= ReplaceIconReference(item.IconVisual, resource, oldUrl);
            return changed;
        }

        private static FilterDefinition<ContentItem> ContentReplacementFilter(ManagedResource resource, string oldUrl) =>
            ManagedResourceReferenceHelper.ContentFilter(resource, oldUrl);

        private static FilterDefinition<Section> SectionReplacementFilter(ManagedResource resource, string oldUrl) =>
            Builders<Section>.Filter.Or(
                ManagedResourceReferenceHelper.SectionUrlFilter(oldUrl),
                Builders<Section>.Filter.Eq("Items.IconVisual.ResourceId", resource.Id),
                Builders<Section>.Filter.Eq("Items.IconVisual.Url", oldUrl));

        private static FilterDefinition<Block> BlockReplacementFilter(ManagedResource resource, string oldUrl) =>
            Builders<Block>.Filter.Or(
                ManagedResourceReferenceHelper.BlockUrlFilter([oldUrl]),
                Builders<Block>.Filter.Eq("IconVisual.ResourceId", resource.Id),
                Builders<Block>.Filter.Eq("IconVisual.Url", oldUrl),
                Builders<Block>.Filter.Eq("Items.IconVisual.ResourceId", resource.Id),
                Builders<Block>.Filter.Eq("Items.IconVisual.Url", oldUrl));

        private static FilterDefinition<SectionPreset> SectionPresetReplacementFilter(string oldUrl) =>
            Builders<SectionPreset>.Filter.Or(
                Builders<SectionPreset>.Filter.Eq(preset => preset.ThumbnailUrl, oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.Style.BackgroundImageUrl", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.Style.BackgroundVideoUrl", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.ImageUrl", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.Items.ImageUrl", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.Items.IconVisual.Url", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Section.ItemOverrides.CardImageUrl", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Blocks.Asset.Url", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Blocks.IconVisual.Url", oldUrl),
                Builders<SectionPreset>.Filter.Eq("Blocks.Items.IconVisual.Url", oldUrl));

        private static IconReference? BlockIcon(Block block) => block switch
        {
            CardBlock value => value.IconVisual,
            ButtonBlock value => value.IconVisual,
            MetricBlock value => value.IconVisual,
            StepBlock value => value.IconVisual,
            IconBlock value => value.IconVisual,
            _ => null
        };

        private static bool ReplaceIconReference(IconReference? icon, ManagedResource resource, string oldUrl)
        {
            if (icon is null) return false;
            var matches = string.Equals(icon.ResourceId, resource.Id, StringComparison.Ordinal) ||
                          ManagedResourceReferenceHelper.SameUrl(icon.Url, oldUrl);
            if (!matches) return false;
            icon.ResourceId = resource.Id;
            icon.ResourceSource = ManagedResourceSource;
            icon.Url = resource.Url;
            icon.StorageKey = resource.StorageKey;
            icon.FileName = resource.FileName;
            icon.ContentType = resource.ContentType;
            icon.SizeBytes = resource.SizeBytes;
            return true;
        }

        private static string ThumbnailReplacementUrl(ManagedResource resource) =>
            string.Equals(resource.Kind, "image", StringComparison.OrdinalIgnoreCase)
                ? resource.Url
                : resource.ThumbnailUrl ?? resource.Url;
    }
}
