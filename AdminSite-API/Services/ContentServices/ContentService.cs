using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ContentService
    {
        private readonly MongoDbContext _context;
        private readonly ContentTypeService _types;
        private readonly ContentValidationService _validation;
        private readonly ContentWorkflowService _workflow;
        private readonly ContentRevisionService _revisions;
        private readonly ContentAssetMetadataService _assets;

        public ContentService(
            MongoDbContext context,
            ContentTypeService types,
            ContentValidationService validation,
            ContentWorkflowService workflow,
            ContentRevisionService revisions,
            ContentAssetMetadataService assets)
        {
            _context = context;
            _types = types;
            _validation = validation;
            _workflow = workflow;
            _revisions = revisions;
            _assets = assets;
        }

        public Task<List<ContentType>> GetTypesAsync() =>
            _types.GetTypesAsync();

        public Task<ContentType?> GetTypeAsync(string id) =>
            _types.GetTypeAsync(id);

        public Task<(ContentType? Type, List<string> Errors)> CreateTypeAsync(ContentTypeCreateDto dto) =>
            _types.CreateTypeAsync(dto);

        public Task<ContentType?> UpdateTypeAsync(string id, ContentTypeUpdateDto dto) =>
            _types.UpdateTypeAsync(id, dto);

        public Task<bool> DeleteTypeAsync(string id) =>
            _types.DeleteTypeAsync(id);

        public async Task<List<ContentItem>> GetAllAsync(string? typeKey = null, ContentStatus? status = null)
        {
            var filter = Builders<ContentItem>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(typeKey))
                filter &= Builders<ContentItem>.Filter.Eq(c => c.ContentTypeKey, ContentAssetMetadataService.NormalizeKey(typeKey));
            if (status is not null)
                filter &= Builders<ContentItem>.Filter.Eq(c => c.Status, status.Value);

            return await _context.ContentDraft.Find(filter)
                .SortByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetDraftLibraryItemsAsync(IEnumerable<string>? typeKeys, int limit, string? sortMode = null)
        {
            if (!HasLibrarySource(typeKeys))
                return new();

            var filter = BuildLibraryFilter(typeKeys, draft: true);
            return await ApplyLibrarySort(_context.ContentDraft.Find(filter), sortMode)
                .Limit(Math.Clamp(limit, 1, 200))
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetPublishedLibraryItemsAsync(IEnumerable<string>? typeKeys, int limit, string? sortMode = null)
        {
            if (!HasLibrarySource(typeKeys))
                return new();

            var filter = BuildLibraryFilter(typeKeys, draft: false);
            return await ApplyLibrarySort(_context.ContentPublished.Find(filter), sortMode)
                .Limit(Math.Clamp(limit, 1, 200))
                .ToListAsync();
        }

        public async Task<ContentItem?> GetPublishedBySlugAsync(string typeKey, string slug)
        {
            var normalizedType = ContentAssetMetadataService.NormalizeRouteType(typeKey);
            var normalizedSlug = ContentAssetMetadataService.NormalizeSlug(slug, string.Empty);
            var candidateTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedType };
            if (normalizedType.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalizedType.Length > 1)
                candidateTypes.Add(normalizedType[..^1]);

            var filter = Builders<ContentItem>.Filter.Eq(c => c.Visible, true) &
                         Builders<ContentItem>.Filter.In(c => c.ContentTypeKey, candidateTypes) &
                         Builders<ContentItem>.Filter.Eq(c => c.Slug, normalizedSlug);

            return await _context.ContentPublished.Find(filter)
                .FirstOrDefaultAsync();
        }

        public async Task<ContentItem?> GetByIdAsync(string id) =>
            await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync();

        public async Task<(ContentItem? Item, List<string> Errors)> CreateAsync(ContentCreateDto dto, string actorId)
        {
            var errors = await _validation.ValidateCreateAsync(dto);
            if (errors.Count > 0) return (null, errors);

            var typeKey = ContentAssetMetadataService.NormalizeKey(dto.ContentTypeKey);
            var enTitle = dto.Title.GetValueOrDefault("en", string.Empty).Trim();
            var slug = await UniqueSlugAsync(typeKey, ContentAssetMetadataService.NormalizeSlug(dto.Slug, enTitle));
            var bodyItems = _assets.NormalizeBodyItems(dto.BodyItems, dto.BodyHtml);
            var galleryItems = _assets.NormalizeGalleryItems(dto.GalleryItems);

            var item = new ContentItem
            {
                StableId = Guid.NewGuid().ToString("N"),
                ContentTypeKey = typeKey,
                Slug = slug,
                Title = ContentAssetMetadataService.NormalizeLang(dto.Title),
                Summary = ContentAssetMetadataService.NormalizeLang(dto.Summary, false),
                BodyItems = bodyItems,
                BodyHtml = _assets.BuildBodyHtmlMirror(bodyItems, dto.BodyHtml),
                GalleryItems = galleryItems,
                HeroImageUrl = ContentAssetMetadataService.CleanUrl(dto.HeroImageUrl),
                HeroImageResourceId = ContentAssetMetadataService.CleanResourceId(dto.HeroImageResourceId),
                HeroImageResourceSource = ContentAssetMetadataService.NormalizeResourceSource(dto.HeroImageResourceSource, dto.HeroImageResourceId),
                HeroImageStorageKey = ContentAssetMetadataService.CleanStorageKey(dto.HeroImageStorageKey),
                HeroImageAlt = dto.HeroImageAlt?.Trim(),
                ThumbnailUrl = ContentAssetMetadataService.CleanUrl(dto.ThumbnailUrl),
                ThumbnailResourceId = ContentAssetMetadataService.CleanResourceId(dto.ThumbnailResourceId),
                ThumbnailResourceSource = ContentAssetMetadataService.NormalizeResourceSource(dto.ThumbnailResourceSource, dto.ThumbnailResourceId),
                ThumbnailStorageKey = ContentAssetMetadataService.CleanStorageKey(dto.ThumbnailStorageKey),
                VideoUrl = ContentAssetMetadataService.CleanUrl(dto.VideoUrl),
                VideoResourceId = ContentAssetMetadataService.CleanResourceId(dto.VideoResourceId),
                VideoResourceSource = ContentAssetMetadataService.NormalizeResourceSource(dto.VideoResourceSource, dto.VideoResourceId),
                VideoStorageKey = ContentAssetMetadataService.CleanStorageKey(dto.VideoStorageKey),
                ExternalUrl = ContentAssetMetadataService.CleanUrl(dto.ExternalUrl),
                TemplateKey = ContentAssetMetadataService.CleanTemplateKey(dto.TemplateKey),
                Tags = ContentAssetMetadataService.NormalizeTags(dto.Tags),
                Attachments = _assets.NormalizeAttachments(dto.Attachments),
                Visible = dto.Visible,
                AuthorId = actorId,
                UpdatedById = actorId,
                Status = ContentStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ContentDraft.InsertOneAsync(item);
            await _revisions.LogAsync(item.StableId, "created", actorId);
            return (item, []);
        }

        public async Task<(ContentItem? Item, List<string> Errors)> UpdateAsync(string id, ContentUpdateDto dto, string actorId)
        {
            var existing = await GetByIdAsync(id);
            if (existing is null) return (null, ["Content item not found."]);

            var updates = new List<UpdateDefinition<ContentItem>>
            {
                Builders<ContentItem>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow),
                Builders<ContentItem>.Update.Set(c => c.UpdatedById, actorId)
            };

            var typeKey = existing.ContentTypeKey;
            if (!string.IsNullOrWhiteSpace(dto.ContentTypeKey))
            {
                typeKey = ContentAssetMetadataService.NormalizeKey(dto.ContentTypeKey);
                if (!await _types.TypeExistsAsync(typeKey)) return (null, ["Content type does not exist."]);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ContentTypeKey, typeKey));
            }

            var validation = await _validation.ValidateUpdateAsync(existing, dto, typeKey);
            if (validation.Count > 0) return (null, validation);

            await _revisions.SaveAsync(existing, actorId, "updated");

            if (dto.Title is not null)
            {
                var normalizedTitle = ContentAssetMetadataService.NormalizeLang(dto.Title);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.Title, normalizedTitle));

                if (!string.IsNullOrWhiteSpace(dto.Slug))
                    updates.Add(Builders<ContentItem>.Update.Set(c => c.Slug, await UniqueSlugAsync(typeKey, ContentAssetMetadataService.NormalizeSlug(dto.Slug, normalizedTitle.GetValueOrDefault("en", string.Empty)), existing.Id)));
            }
            else if (!string.IsNullOrWhiteSpace(dto.Slug))
            {
                updates.Add(Builders<ContentItem>.Update.Set(c => c.Slug, await UniqueSlugAsync(typeKey, ContentAssetMetadataService.NormalizeSlug(dto.Slug, existing.Title.GetValueOrDefault("en", string.Empty)), existing.Id)));
            }

            if (dto.Summary is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Summary, ContentAssetMetadataService.NormalizeLang(dto.Summary, false)));
            if (dto.BodyItems is not null)
            {
                var normalizedBodyItems = _assets.NormalizeBodyItems(dto.BodyItems, dto.BodyHtml ?? existing.BodyHtml);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.BodyItems, normalizedBodyItems));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.BodyHtml, _assets.BuildBodyHtmlMirror(normalizedBodyItems, dto.BodyHtml ?? existing.BodyHtml)));
            }
            else if (dto.BodyHtml is not null)
            {
                var bodyHtml = _assets.SanitizeLang(dto.BodyHtml);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.BodyHtml, bodyHtml));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.BodyItems, _assets.NormalizeBodyItems(null, bodyHtml)));
            }
            if (dto.GalleryItems is not null)
                updates.Add(Builders<ContentItem>.Update.Set(c => c.GalleryItems, _assets.NormalizeGalleryItems(dto.GalleryItems)));
            if (dto.HeroImageUrl is not null)
            {
                updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageUrl, ContentAssetMetadataService.CleanUrl(dto.HeroImageUrl)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageResourceId, ContentAssetMetadataService.CleanResourceId(dto.HeroImageResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageResourceSource, ContentAssetMetadataService.NormalizeResourceSource(dto.HeroImageResourceSource, dto.HeroImageResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageStorageKey, ContentAssetMetadataService.CleanStorageKey(dto.HeroImageStorageKey)));
            }
            if (dto.HeroImageAlt is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageAlt, dto.HeroImageAlt.Trim()));
            if (dto.ThumbnailUrl is not null)
            {
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ThumbnailUrl, ContentAssetMetadataService.CleanUrl(dto.ThumbnailUrl)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ThumbnailResourceId, ContentAssetMetadataService.CleanResourceId(dto.ThumbnailResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ThumbnailResourceSource, ContentAssetMetadataService.NormalizeResourceSource(dto.ThumbnailResourceSource, dto.ThumbnailResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ThumbnailStorageKey, ContentAssetMetadataService.CleanStorageKey(dto.ThumbnailStorageKey)));
            }
            if (dto.VideoUrl is not null)
            {
                updates.Add(Builders<ContentItem>.Update.Set(c => c.VideoUrl, ContentAssetMetadataService.CleanUrl(dto.VideoUrl)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.VideoResourceId, ContentAssetMetadataService.CleanResourceId(dto.VideoResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.VideoResourceSource, ContentAssetMetadataService.NormalizeResourceSource(dto.VideoResourceSource, dto.VideoResourceId)));
                updates.Add(Builders<ContentItem>.Update.Set(c => c.VideoStorageKey, ContentAssetMetadataService.CleanStorageKey(dto.VideoStorageKey)));
            }
            if (dto.ExternalUrl is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.ExternalUrl, ContentAssetMetadataService.CleanUrl(dto.ExternalUrl)));
            if (dto.TemplateKey is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.TemplateKey, ContentAssetMetadataService.CleanTemplateKey(dto.TemplateKey)));
            if (dto.Tags is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Tags, ContentAssetMetadataService.NormalizeTags(dto.Tags)));
            if (dto.Attachments is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Attachments, _assets.NormalizeAttachments(dto.Attachments)));
            if (dto.Visible is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Visible, dto.Visible.Value));

            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id, Builders<ContentItem>.Update.Combine(updates));
            await _revisions.LogAsync(existing.StableId, "updated", actorId);
            return (await GetByIdAsync(id), []);
        }

        public Task<(ContentItem? Item, List<string> Errors)> SetStatusAsync(string id, ContentStatusUpdateDto dto, string actorId) =>
            _workflow.SetStatusAsync(id, dto, actorId);

        public Task<(ContentItem? Item, List<string> Errors)> PublishAsync(string id, string actorId) =>
            _workflow.PublishAsync(id, actorId);

        public Task<bool> DeleteAsync(string id, string actorId) =>
            _workflow.DeleteAsync(id, actorId);

        public Task<(ContentItem? Item, List<string> Errors)> RestoreAsync(string id, string actorId) =>
            _workflow.RestoreAsync(id, actorId);

        public Task<int> PermanentDeleteAsync(IEnumerable<string> ids) =>
            _workflow.PermanentDeleteAsync(ids);

        public Task<List<RevisionResponseDto>> GetRevisionsAsync(string id) =>
            _revisions.GetRevisionsAsync(id);

        public Task<(ContentItem? Item, List<string> Errors)> RestoreRevisionAsync(string id, string revisionId, string actorId) =>
            _revisions.RestoreRevisionAsync(id, revisionId, actorId);

        public Task<List<ContentAuditLog>> GetLogsAsync(string stableId) =>
            _revisions.GetLogsAsync(stableId);

        private static FilterDefinition<ContentItem> BuildLibraryFilter(IEnumerable<string>? typeKeys, bool draft)
        {
            var filter = Builders<ContentItem>.Filter.Eq(c => c.Visible, true);
            if (draft)
                filter &= Builders<ContentItem>.Filter.Eq(c => c.Status, ContentStatus.Published);

            var keys = (typeKeys ?? Enumerable.Empty<string>())
                .Select(ContentAssetMetadataService.NormalizeKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (keys.Count > 0)
                filter &= Builders<ContentItem>.Filter.In(c => c.ContentTypeKey, keys);

            return filter;
        }

        private static bool HasLibrarySource(IEnumerable<string>? typeKeys) =>
            (typeKeys ?? Enumerable.Empty<string>())
                .Select(ContentAssetMetadataService.NormalizeKey)
                .Any(k => !string.IsNullOrWhiteSpace(k));

        private static IFindFluent<ContentItem, ContentItem> ApplyLibrarySort(IFindFluent<ContentItem, ContentItem> query, string? sortMode) =>
            sortMode switch
            {
                "oldest" => query.SortBy(c => c.PublishedAt).ThenBy(c => c.CreatedAt),
                "title" => query.Sort(Builders<ContentItem>.Sort.Ascending("Title.en")),
                _ => query.SortByDescending(c => c.PublishedAt).ThenByDescending(c => c.UpdatedAt)
            };

        private async Task<string> UniqueSlugAsync(string typeKey, string slug, string? existingId = null)
        {
            var baseSlug = string.IsNullOrWhiteSpace(slug) ? "content" : slug;
            var candidate = baseSlug;
            var index = 2;

            while (await _context.ContentDraft.Find(c =>
                       c.ContentTypeKey == typeKey &&
                       c.Slug == candidate &&
                       c.Id != existingId).AnyAsync())
            {
                candidate = $"{baseSlug}-{index++}";
            }

            return candidate;
        }
    }
}
