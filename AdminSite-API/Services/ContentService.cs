using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using Ganss.Xss;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace FullProject.Services
{
    public class ContentService
    {
        private static readonly string[] RequiredLanguages = ["en", "vi", "cn"];

        private readonly MongoDbContext _context;
        private readonly HtmlSanitizer _sanitizer = new();

        public ContentService(MongoDbContext context)
        {
            _context = context;
            ConfigureSanitizer();
        }

        public async Task<List<ContentType>> GetTypesAsync()
        {
            await EnsureDefaultTypesAsync();
            return await _context.ContentTypes.Find(_ => true)
                .SortBy(t => t.Order)
                .ThenBy(t => t.Key)
                .ToListAsync();
        }

        public async Task<ContentType?> GetTypeAsync(string id) =>
            await _context.ContentTypes.Find(t => t.Id == id).FirstOrDefaultAsync();

        public async Task<(ContentType? Type, List<string> Errors)> CreateTypeAsync(ContentTypeCreateDto dto)
        {
            var errors = ValidateType(dto);
            if (errors.Count > 0) return (null, errors);

            var key = NormalizeContentTypeKey(dto.Key, dto.Name.GetValueOrDefault("en"));
            var exists = await _context.ContentTypes.Find(t => t.Key == key).AnyAsync();
            if (exists) return (null, ["Content type key already exists."]);

            var order = (int)await _context.ContentTypes.CountDocumentsAsync(_ => true);
            var type = new ContentType
            {
                Key = key,
                Name = NormalizeLang(dto.Name),
                Description = NormalizeLang(dto.Description, false),
                RequiresBody = dto.RequiresBody,
                RequiresHeroImage = dto.RequiresHeroImage,
                RequiresFile = dto.RequiresFile,
                RequiresVideoUrl = dto.RequiresVideoUrl,
                AllowsAttachments = dto.AllowsAttachments,
                ClickBehavior = NormalizeClickBehavior(dto.ClickBehavior),
                Visible = dto.Visible,
                Order = order,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ContentTypes.InsertOneAsync(type);
            return (type, []);
        }

        public async Task<ContentType?> UpdateTypeAsync(string id, ContentTypeUpdateDto dto)
        {
            var type = await GetTypeAsync(id);
            if (type is null) return null;

            var updates = new List<UpdateDefinition<ContentType>>
            {
                Builders<ContentType>.Update.Set(t => t.UpdatedAt, DateTime.UtcNow)
            };

            if (dto.Name is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Name, NormalizeLang(dto.Name)));
            if (dto.Description is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Description, NormalizeLang(dto.Description, false)));
            if (dto.RequiresBody is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresBody, dto.RequiresBody.Value));
            if (dto.RequiresHeroImage is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresHeroImage, dto.RequiresHeroImage.Value));
            if (dto.RequiresFile is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresFile, dto.RequiresFile.Value));
            if (dto.RequiresVideoUrl is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.RequiresVideoUrl, dto.RequiresVideoUrl.Value));
            if (dto.AllowsAttachments is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.AllowsAttachments, dto.AllowsAttachments.Value));
            if (dto.ClickBehavior is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.ClickBehavior, NormalizeClickBehavior(dto.ClickBehavior)));
            if (dto.Visible is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Visible, dto.Visible.Value));
            if (dto.Order is not null) updates.Add(Builders<ContentType>.Update.Set(t => t.Order, Math.Max(0, dto.Order.Value)));

            await _context.ContentTypes.UpdateOneAsync(t => t.Id == id, Builders<ContentType>.Update.Combine(updates));
            return await GetTypeAsync(id);
        }

        public async Task<bool> DeleteTypeAsync(string id)
        {
            var type = await GetTypeAsync(id);
            if (type is null) return false;

            var inUse = await _context.ContentDraft.Find(c => c.ContentTypeKey == type.Key).AnyAsync() ||
                        await _context.ContentPublished.Find(c => c.ContentTypeKey == type.Key).AnyAsync();
            if (inUse) return false;

            var result = await _context.ContentTypes.DeleteOneAsync(t => t.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<List<ContentItem>> GetAllAsync(string? typeKey = null, ContentStatus? status = null)
        {
            var filter = Builders<ContentItem>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(typeKey))
                filter &= Builders<ContentItem>.Filter.Eq(c => c.ContentTypeKey, NormalizeKey(typeKey));
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
            var normalizedType = NormalizeRouteType(typeKey);
            var normalizedSlug = NormalizeSlug(slug, string.Empty);

            return await _context.ContentPublished.Find(c =>
                    c.Visible &&
                    c.ContentTypeKey == normalizedType &&
                    c.Slug == normalizedSlug)
                .FirstOrDefaultAsync();
        }

        public async Task<ContentItem?> GetByIdAsync(string id) =>
            await _context.ContentDraft.Find(c => c.Id == id).FirstOrDefaultAsync();

        public async Task<(ContentItem? Item, List<string> Errors)> CreateAsync(ContentCreateDto dto, string actorId)
        {
            var errors = await ValidateContentAsync(dto);
            if (errors.Count > 0) return (null, errors);

            var typeKey = NormalizeKey(dto.ContentTypeKey);
            var enTitle = dto.Title.GetValueOrDefault("en", string.Empty).Trim();
            var slug = await UniqueSlugAsync(typeKey, NormalizeSlug(dto.Slug, enTitle));

            var item = new ContentItem
            {
                StableId = Guid.NewGuid().ToString("N"),
                ContentTypeKey = typeKey,
                Slug = slug,
                Title = NormalizeLang(dto.Title),
                Summary = NormalizeLang(dto.Summary, false),
                BodyHtml = SanitizeLang(dto.BodyHtml),
                HeroImageUrl = CleanUrl(dto.HeroImageUrl),
                HeroImageAlt = dto.HeroImageAlt?.Trim(),
                ThumbnailUrl = CleanUrl(dto.ThumbnailUrl),
                VideoUrl = CleanUrl(dto.VideoUrl),
                ExternalUrl = CleanUrl(dto.ExternalUrl),
                TemplateKey = CleanTemplateKey(dto.TemplateKey),
                Tags = NormalizeTags(dto.Tags),
                Attachments = NormalizeAttachments(dto.Attachments),
                Visible = dto.Visible,
                AuthorId = actorId,
                UpdatedById = actorId,
                Status = ContentStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ContentDraft.InsertOneAsync(item);
            await LogAsync(item.StableId, "created", actorId);
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
                typeKey = NormalizeKey(dto.ContentTypeKey);
                if (!await TypeExistsAsync(typeKey)) return (null, ["Content type does not exist."]);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.ContentTypeKey, typeKey));
            }

            if (dto.Title is not null)
            {
                var normalizedTitle = NormalizeLang(dto.Title);
                updates.Add(Builders<ContentItem>.Update.Set(c => c.Title, normalizedTitle));

                if (!string.IsNullOrWhiteSpace(dto.Slug))
                    updates.Add(Builders<ContentItem>.Update.Set(c => c.Slug, await UniqueSlugAsync(typeKey, NormalizeSlug(dto.Slug, normalizedTitle.GetValueOrDefault("en", string.Empty)), existing.Id)));
            }
            else if (!string.IsNullOrWhiteSpace(dto.Slug))
            {
                updates.Add(Builders<ContentItem>.Update.Set(c => c.Slug, await UniqueSlugAsync(typeKey, NormalizeSlug(dto.Slug, existing.Title.GetValueOrDefault("en", string.Empty)), existing.Id)));
            }

            if (dto.Summary is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Summary, NormalizeLang(dto.Summary, false)));
            if (dto.BodyHtml is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.BodyHtml, SanitizeLang(dto.BodyHtml)));
            if (dto.HeroImageUrl is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageUrl, CleanUrl(dto.HeroImageUrl)));
            if (dto.HeroImageAlt is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.HeroImageAlt, dto.HeroImageAlt.Trim()));
            if (dto.ThumbnailUrl is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.ThumbnailUrl, CleanUrl(dto.ThumbnailUrl)));
            if (dto.VideoUrl is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.VideoUrl, CleanUrl(dto.VideoUrl)));
            if (dto.ExternalUrl is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.ExternalUrl, CleanUrl(dto.ExternalUrl)));
            if (dto.TemplateKey is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.TemplateKey, CleanTemplateKey(dto.TemplateKey)));
            if (dto.Tags is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Tags, NormalizeTags(dto.Tags)));
            if (dto.Attachments is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Attachments, NormalizeAttachments(dto.Attachments)));
            if (dto.Visible is not null) updates.Add(Builders<ContentItem>.Update.Set(c => c.Visible, dto.Visible.Value));

            await _context.ContentDraft.UpdateOneAsync(c => c.Id == id, Builders<ContentItem>.Update.Combine(updates));
            await LogAsync(existing.StableId, "updated", actorId);
            return (await GetByIdAsync(id), []);
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
            await LogAsync(item.StableId, dto.Status.ToString().ToLowerInvariant(), actorId, dto.Message);
            return (await GetByIdAsync(id), []);
        }

        public async Task<(ContentItem? Item, List<string> Errors)> PublishAsync(string id, string actorId)
        {
            var item = await GetByIdAsync(id);
            if (item is null) return (null, ["Content item not found."]);

            var validation = await ValidatePublishAsync(item);
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

            var published = CloneForPublished(updated, actorId, now);
            await _context.ContentPublished.DeleteManyAsync(c => c.StableId == item.StableId);
            await _context.ContentPublished.InsertOneAsync(published);
            await LogAsync(item.StableId, "published", actorId);
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
            await LogAsync(item.StableId, "deleted", actorId);
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

            await LogAsync(item.StableId, "restored", actorId);
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
            await _context.ContentAuditLogs.DeleteManyAsync(l => stableIds.Contains(l.ContentStableId));
            return items.Count;
        }

        public async Task<List<ContentAuditLog>> GetLogsAsync(string stableId) =>
            await _context.ContentAuditLogs.Find(l => l.ContentStableId == stableId)
                .SortByDescending(l => l.CreatedAt)
                .ToListAsync();

        private async Task<List<string>> ValidateContentAsync(ContentCreateDto dto)
        {
            var errors = new List<string>();
            if (!await TypeExistsAsync(dto.ContentTypeKey)) errors.Add("Content type does not exist.");
            if (string.IsNullOrWhiteSpace(dto.Title.GetValueOrDefault("en"))) errors.Add("English title is required.");
            if (dto.BodyHtml.Values.Any(v => v.Length > 100_000)) errors.Add("Body HTML is too large.");
            if (dto.Attachments.Count > 20) errors.Add("A content item can have at most 20 attachments.");
            return errors;
        }

        private async Task<List<string>> ValidatePublishAsync(ContentItem item)
        {
            var errors = new List<string>();
            var type = await _context.ContentTypes.Find(t => t.Key == item.ContentTypeKey).FirstOrDefaultAsync();
            if (type is null) errors.Add("Content type does not exist.");
            if (string.IsNullOrWhiteSpace(item.Title.GetValueOrDefault("en"))) errors.Add("English title is required.");
            if (type?.RequiresBody == true && string.IsNullOrWhiteSpace(item.BodyHtml.GetValueOrDefault("en"))) errors.Add("Body is required.");
            if (type?.RequiresHeroImage == true && string.IsNullOrWhiteSpace(item.HeroImageUrl)) errors.Add("Hero image is required.");
            if (type?.RequiresFile == true && item.Attachments.Count == 0) errors.Add("File attachment is required.");
            if (type?.RequiresVideoUrl == true && string.IsNullOrWhiteSpace(item.VideoUrl)) errors.Add("Video URL is required.");
            if (type?.AllowsAttachments == false && item.Attachments.Count > 0) errors.Add("This content type does not allow attachments.");
            return errors;
        }

        private static List<string> ValidateType(ContentTypeCreateDto dto)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(dto.Name.GetValueOrDefault("en"))) errors.Add("English name is required.");
            if (!string.IsNullOrWhiteSpace(dto.Key) && !Regex.IsMatch(dto.Key, "^[a-z0-9-]+$", RegexOptions.IgnoreCase))
                errors.Add("Key can only contain letters, numbers, and hyphens.");
            return errors;
        }

        private async Task<bool> TypeExistsAsync(string typeKey)
        {
            await EnsureDefaultTypesAsync();
            return await _context.ContentTypes.Find(t => t.Key == NormalizeKey(typeKey)).AnyAsync();
        }

        private static FilterDefinition<ContentItem> BuildLibraryFilter(IEnumerable<string>? typeKeys, bool draft)
        {
            var filter = Builders<ContentItem>.Filter.Eq(c => c.Visible, true);
            if (draft)
                filter &= Builders<ContentItem>.Filter.Eq(c => c.Status, ContentStatus.Published);

            var keys = (typeKeys ?? Enumerable.Empty<string>())
                .Select(NormalizeKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (keys.Count > 0)
                filter &= Builders<ContentItem>.Filter.In(c => c.ContentTypeKey, keys);

            return filter;
        }

        private static bool HasLibrarySource(IEnumerable<string>? typeKeys) =>
            (typeKeys ?? Enumerable.Empty<string>())
                .Select(NormalizeKey)
                .Any(k => !string.IsNullOrWhiteSpace(k));

        private static IFindFluent<ContentItem, ContentItem> ApplyLibrarySort(IFindFluent<ContentItem, ContentItem> query, string? sortMode) =>
            sortMode switch
            {
                "oldest" => query.SortBy(c => c.PublishedAt).ThenBy(c => c.CreatedAt),
                "title" => query.Sort(Builders<ContentItem>.Sort.Ascending("Title.en")),
                _ => query.SortByDescending(c => c.PublishedAt).ThenByDescending(c => c.UpdatedAt)
            };

        private async Task EnsureDefaultTypesAsync()
        {
            if (await _context.ContentTypes.Find(_ => true).AnyAsync())
                return;

            var defaults = new[]
            {
                new ContentType { Key = "article", Name = Lang("Article"), Description = Lang("Editorial content and market intelligence."), Order = 0, RequiresBody = true, ClickBehavior = "detail" },
                new ContentType { Key = "case-study", Name = Lang("Case Study"), Description = Lang("Outcome-driven customer or operational stories."), Order = 1, RequiresBody = true, ClickBehavior = "detail" },
                new ContentType { Key = "whitepaper", Name = Lang("Whitepaper / Report"), Description = Lang("Downloadable PDF files and reports."), Order = 2, RequiresBody = false, RequiresFile = true, AllowsAttachments = true, ClickBehavior = "download" },
                new ContentType { Key = "video", Name = Lang("Video / Webinar"), Description = Lang("Video and webinar links."), Order = 3, RequiresBody = false, RequiresVideoUrl = true, AllowsAttachments = false, ClickBehavior = "video" },
                new ContentType { Key = "tool", Name = Lang("Tool"), Description = Lang("Templates, calculators, and downloadable tools."), Order = 4, RequiresBody = false, AllowsAttachments = true, ClickBehavior = "download" }
            };

            await _context.ContentTypes.InsertManyAsync(defaults);
        }

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

        private async Task LogAsync(string stableId, string action, string actorId, string? message = null)
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

        private ContentItem CloneForPublished(ContentItem item, string actorId, DateTime publishedAt) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            StableId = item.StableId,
            ContentTypeKey = item.ContentTypeKey,
            Slug = item.Slug,
            Title = new(item.Title),
            Summary = new(item.Summary),
            BodyHtml = new(item.BodyHtml),
            HeroImageUrl = item.HeroImageUrl,
            HeroImageAlt = item.HeroImageAlt,
            ThumbnailUrl = item.ThumbnailUrl,
            VideoUrl = item.VideoUrl,
            ExternalUrl = item.ExternalUrl,
            TemplateKey = item.TemplateKey,
            Tags = item.Tags.ToList(),
            Attachments = item.Attachments.Select(a => new ContentAttachment
            {
                Id = a.Id,
                FileName = a.FileName,
                Url = a.Url,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes
            }).ToList(),
            Status = ContentStatus.Published,
            Visible = item.Visible,
            AuthorId = item.AuthorId,
            UpdatedById = actorId,
            PublishedById = actorId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = publishedAt,
            SubmittedAt = item.SubmittedAt,
            PublishedAt = publishedAt
        };

        private void ConfigureSanitizer()
        {
            _sanitizer.AllowedSchemes.Add("data");
            _sanitizer.AllowedAttributes.Add("class");
            _sanitizer.AllowedAttributes.Add("target");
            _sanitizer.AllowedAttributes.Add("rel");
        }

        private Dictionary<string, string> SanitizeLang(Dictionary<string, string> values) =>
            NormalizeLang(values, false).ToDictionary(kv => kv.Key, kv => _sanitizer.Sanitize(kv.Value));

        private static Dictionary<string, string> NormalizeLang(Dictionary<string, string> values, bool requireFallback = true)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var en = values.GetValueOrDefault("en", string.Empty).Trim();
            foreach (var lang in RequiredLanguages)
            {
                var value = values.GetValueOrDefault(lang, string.Empty).Trim();
                result[lang] = requireFallback && string.IsNullOrWhiteSpace(value) ? en : value;
            }
            return result;
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags) =>
            tags.Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

        private static List<ContentAttachment> NormalizeAttachments(IEnumerable<ContentAttachmentDto> attachments) =>
            attachments
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .Take(20)
                .Select(a => new ContentAttachment
                {
                    Id = string.IsNullOrWhiteSpace(a.Id) ? Guid.NewGuid().ToString("N") : a.Id,
                    FileName = a.FileName.Trim(),
                    Url = CleanUrl(a.Url) ?? string.Empty,
                    ContentType = a.ContentType.Trim(),
                    SizeBytes = Math.Max(0, a.SizeBytes)
                })
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .ToList();

        private static string NormalizeKey(string value) =>
            NormalizeSlug(value, "content-type");

        private static string NormalizeContentTypeKey(string? key, string? name) =>
            NormalizeSlug(string.IsNullOrWhiteSpace(key) ? name : key, "content-type");

        private static string NormalizeRouteType(string value)
        {
            var normalized = NormalizeKey(value);
            return normalized switch
            {
                "articles" => "article",
                "case-studies" => "case-study",
                "whitepapers" => "whitepaper",
                "reports" => "whitepaper",
                "videos" => "video",
                "webinars" => "video",
                "tools" => "tool",
                _ => normalized
            };
        }

        private static string NormalizeSlug(string? value, string fallback)
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
            var slug = Regex.Replace(source.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "content" : slug;
        }

        private static string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("/", StringComparison.Ordinal)
                ? trimmed
                : null;
        }

        private static string NormalizeClickBehavior(string? value)
        {
            var normalized = (value ?? "detail").Trim().ToLowerInvariant();
            return normalized is "detail" or "download" or "video" or "external" ? normalized : "detail";
        }

        private static string? CleanTemplateKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return NormalizeSlug(value, "default");
        }

        private static Dictionary<string, string> Lang(string value) => new()
        {
            ["en"] = value,
            ["vi"] = value,
            ["cn"] = value
        };
    }
}
