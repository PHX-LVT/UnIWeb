using FullProject.Data; // Imported your new context namespace
using FullProject.DTOs;
using FullProject.Models;
using FullProject.SectionServices;
using MongoDB.Driver;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FullProject.Services
{
    public class PageService
    {
        private readonly MongoDbContext _context; // Changed from raw IMongoCollection
        private readonly SectionService _sectionService;
        private readonly PageCleanupService _cleanupService;
        private readonly R2AssetService _r2Assets;

        public PageService(MongoDbContext context, SectionService sectionService, PageCleanupService cleanupService, R2AssetService r2Assets)
        {
            _context = context;
            _sectionService = sectionService;
            _cleanupService = cleanupService;
            _r2Assets = r2Assets;
        }

        // -----------------------------------------------------------
        // ADMIN WORKSPACE BACKEND METHODS (DRAFT EXCLUSIVE)
        // -----------------------------------------------------------

        public async Task<List<Page>> GetAllAsync() =>
            await _context.PagesDraft.Find(_ => true).SortBy(p => p.Order).ToListAsync();

        public async Task<Page?> GetByIdAsync(string pageId) =>
            await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();

        public async Task<Page?> GetBySlugAsync(string slug) =>
            await _context.PagesDraft.Find(p => p.Slug == slug && p.ParentPageId == null)
                .FirstOrDefaultAsync();

        public async Task<Page> CreateAsync(Page page, string enName)
        {
            // Set default runtime StableId for the whole layout pipeline link tree
            if (string.IsNullOrWhiteSpace(page.StableId))
            {
                page.StableId = Guid.NewGuid().ToString();
            }

            var count = page.ParentPageId != null
                ? await _context.PagesDraft.CountDocumentsAsync(p => p.ParentPageId == page.ParentPageId)
                : await _context.PagesDraft.CountDocumentsAsync(p => p.ParentPageId == null);

            page.Slug = await GenerateUniqueSlugAsync(enName, page.ParentPageId);
            page.FullSlug = page.ParentSlug != null
                ? $"{page.ParentSlug}/{page.Slug}"
                : page.Slug;
            page.Order = (int)count;
            page.Version = 1;
            page.CreatedAt = DateTime.UtcNow;
            page.UpdatedAt = DateTime.UtcNow;

            await _context.PagesDraft.InsertOneAsync(page);
            return page;
        }

        public async Task<Page?> UpdateAsync(string pageId, PageUpdateDto dto)
        {
            var existing = await GetByIdAsync(pageId);
            if (existing is null) return null;

            var updates = new List<UpdateDefinition<Page>>
            {
                // Increment schema layout versioning whenever manual changes occur
                Builders<Page>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow),
                Builders<Page>.Update.Inc(p => p.Version, 1)
            };

            if (dto.Name != null) updates.Add(Builders<Page>.Update.Set(p => p.Name, dto.Name));
            if (dto.Access != null) updates.Add(Builders<Page>.Update.Set(p => p.Access, dto.Access.Value));
            if (dto.Visible != null) updates.Add(Builders<Page>.Update.Set(p => p.Visible, dto.Visible.Value));
            if (dto.Status != null) updates.Add(Builders<Page>.Update.Set(p => p.Status, dto.Status.Value));

            if (dto.Slug != null && dto.Slug != existing.Slug)
            {
                var slug = await GenerateUniqueSlugAsync(
                    dto.Slug, existing.ParentPageId, excludeId: pageId);
                var fullSlug = existing.ParentSlug != null
                    ? $"{existing.ParentSlug}/{slug}"
                    : slug;
                updates.Add(Builders<Page>.Update.Set(p => p.Slug, slug));
                updates.Add(Builders<Page>.Update.Set(p => p.FullSlug, fullSlug));

                await CascadeSlugUpdateAsync(pageId, slug);
            }

            if (dto.Seo != null)
            {
                if (dto.Seo.MetaTitle != null)
                    updates.Add(Builders<Page>.Update.Set(p => p.Seo.MetaTitle, dto.Seo.MetaTitle));
                if (dto.Seo.MetaDescription != null)
                    updates.Add(Builders<Page>.Update.Set(p => p.Seo.MetaDescription, dto.Seo.MetaDescription));
            }

            await _context.PagesDraft.UpdateOneAsync(p => p.Id == pageId,
                Builders<Page>.Update.Combine(updates));

            return await GetByIdAsync(pageId);
        }

        public async Task<bool> DeleteAsync(string pageId)
        {
            var exists = await GetByIdAsync(pageId);
            if (exists is null) return false;

            // PageCleanupService internally handles purging data objects from your draft stack
            await _cleanupService.DeletePageAndDependenciesAsync(pageId);
            return true;
        }

        public async Task<bool> SetVisibilityAsync(string pageId, bool visible)
        {
            var result = await _context.PagesDraft.UpdateOneAsync(
                p => p.Id == pageId,
                Builders<Page>.Update
                    .Set(p => p.Visible, visible)
                    .Inc(p => p.Version, 1)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SetAccessAsync(string pageId, bool access)
        {
            var result = await _context.PagesDraft.UpdateOneAsync(
                p => p.Id == pageId,
                Builders<Page>.Update
                    .Set(p => p.Access, access)
                    .Inc(p => p.Version, 1)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ReorderAsync(List<string> orderedIds)
        {
            var writes = orderedIds.Select((id, i) =>
                new UpdateOneModel<Page>(
                    Builders<Page>.Filter.Eq(p => p.Id, id),
                    Builders<Page>.Update.Set(p => p.Order, i).Inc(p => p.Version, 1))
            ).Cast<WriteModel<Page>>().ToList();

            if (writes.Count == 0) return true;
            await _context.PagesDraft.BulkWriteAsync(writes);
            return true;
        }

        public async Task<List<Page>> GetChildrenAsync(string parentPageId) =>
            await _context.PagesDraft.Find(p => p.ParentPageId == parentPageId)
                                .SortBy(p => p.Order)
                                .ToListAsync();

        public async Task<bool> UpdateCardAsync(string pageId, PageCardDto dto, bool isCustomized = true)
        {
            var existing = await GetByIdAsync(pageId);
            var oldCardImageUrl = existing?.Card?.CardImageUrl;
            var updates = new List<UpdateDefinition<Page>>();
            if (dto.CardTitle != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardTitle, dto.CardTitle));
            if (dto.CardContent != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardContent, dto.CardContent));
            if (dto.CardBackgroundType != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardBackgroundType, dto.CardBackgroundType));
            if (dto.CardBackgroundColor != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardBackgroundColor, dto.CardBackgroundColor));
            if (dto.CardImageUrl != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardImageUrl, dto.CardImageUrl));

            updates.Add(Builders<Page>.Update.Set(p => p.Card!.IsCustomized, isCustomized));
            updates.Add(Builders<Page>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow));
            updates.Add(Builders<Page>.Update.Inc(p => p.Version, 1));

            var result = await _context.PagesDraft.UpdateOneAsync(p => p.Id == pageId,
                Builders<Page>.Update.Combine(updates));

            if (result.ModifiedCount > 0 && dto.CardImageUrl != null)
                await _r2Assets.DeleteIfUnusedAsync(oldCardImageUrl, dto.CardImageUrl);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> ResetCardAsync(string pageId)
        {
            var result = await _context.PagesDraft.UpdateOneAsync(p => p.Id == pageId,
                Builders<Page>.Update.Set(p => p.Card!.IsCustomized, false).Inc(p => p.Version, 1));
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ReorderChildrenAsync(string parentPageId, List<string> orderedIds)
        {
            var writes = orderedIds.Select((id, i) =>
                new UpdateOneModel<Page>(
                    Builders<Page>.Filter.Where(p => p.ParentPageId == parentPageId && p.Id == id),
                    Builders<Page>.Update.Set(p => p.Order, i).Inc(p => p.Version, 1))
            ).Cast<WriteModel<Page>>().ToList();

            if (writes.Count == 0) return true;
            await _context.PagesDraft.BulkWriteAsync(writes);
            return true;
        }

        public async Task AutoSyncCardFromHeroAsync(string pageId, Dictionary<string, string>? heading, Dictionary<string, string>? subheading, string? imageUrl)
        {
            var page = await GetByIdAsync(pageId);
            if (page is null || page.Card is null || page.Card.IsCustomized)
                return;

            var updates = new List<UpdateDefinition<Page>>
            {
                Builders<Page>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow)
            };
            if (heading != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardTitle, heading));
            if (subheading != null) updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardContent, subheading));
            if (imageUrl != null)
            {
                updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardImageUrl, imageUrl));
                updates.Add(Builders<Page>.Update.Set(p => p.Card!.CardBackgroundType, "image"));
            }

            await _context.PagesDraft.UpdateOneAsync(p => p.Id == pageId, Builders<Page>.Update.Combine(updates));
        }

        // -----------------------------------------------------------
        // PUBLIC USER SITE RENDER METHODS (PUBLISHED EXCLUSIVE)
        // -----------------------------------------------------------

        public async Task<Page?> GetByFullSlugAsync(string fullSlug) =>
            await _context.PagesPublished.Find(p => p.FullSlug == fullSlug &&
                                                    p.Access == true &&
                                                    p.Status == PageStatus.Published)
                                .FirstOrDefaultAsync();

        public async Task<List<Page>> GetPublicRootPagesAsync() =>
            await _context.PagesPublished
                .Find(p => p.Access == true && p.Visible == true && p.Status == PageStatus.Published && p.ParentPageId == null)
                .SortBy(p => p.Order)
                .ToListAsync();

        public async Task<List<Page>> GetPublicChildrenAsync(string parentPageId) =>
            await _context.PagesPublished.Find(p => p.ParentPageId == parentPageId &&
                                                    p.Access == true &&
                                                    p.Status == PageStatus.Published)
                .SortBy(p => p.Order)
                .ToListAsync();

        // -----------------------------------------------------------
        // INNER STRATEGY UTILITIES
        // -----------------------------------------------------------

        private static string Slugify(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);

            var slug = sb.ToString().Normalize(NormalizationForm.FormC);
            slug = slug.ToLowerInvariant();
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');
            return slug;
        }

        private async Task<string> GenerateUniqueSlugAsync(string input, string? parentPageId = null, string? excludeId = null)
        {
            var baseSlug = Slugify(input);
            var slug = baseSlug;
            var counter = 2;

            while (true)
            {
                var filter = parentPageId != null
                    ? Builders<Page>.Filter.Where(p => p.Slug == slug && p.ParentPageId == parentPageId)
                    : Builders<Page>.Filter.Where(p => p.Slug == slug && p.ParentPageId == null);

                if (excludeId != null)
                    filter &= Builders<Page>.Filter.Ne(p => p.Id, excludeId);

                if (!await _context.PagesDraft.Find(filter).AnyAsync()) return slug;
                slug = $"{baseSlug}-{counter++}";
            }
        }

        private async Task CascadeSlugUpdateAsync(string parentPageId, string newParentSlug)
        {
            var children = await GetChildrenAsync(parentPageId);
            foreach (var child in children)
            {
                var newFullSlug = $"{newParentSlug}/{child.Slug}";
                await _context.PagesDraft.UpdateOneAsync(p => p.Id == child.Id,
                    Builders<Page>.Update
                        .Set(p => p.ParentSlug, newParentSlug)
                        .Set(p => p.FullSlug, newFullSlug));

                await CascadeSlugUpdateAsync(child.Id, newFullSlug);
            }
        }
    }
}



