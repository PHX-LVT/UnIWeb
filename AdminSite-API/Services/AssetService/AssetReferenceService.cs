using FullProject.Data;
using FullProject.Models;
using MongoDB.Driver;

namespace GlobalManager.Services.AssetService
{
    public class AssetReferenceService
    {
        private readonly MongoDbContext _context;

        public AssetReferenceService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsReferencedAsync(string? url, string? excludingManagedResourceId = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (await ManagedResourceReferencesAsync(url, excludingManagedResourceId)) return true;
            if (await _context.Branding.Find(b => b.LogoUrl == url).AnyAsync()) return true;
            if (await PageReferencesAsync(_context.PagesDraft, url)) return true;
            if (await PageReferencesAsync(_context.PagesPublished, url)) return true;
            if (await SectionReferencesAsync(_context.SectionsDraft, url)) return true;
            if (await SectionReferencesAsync(_context.SectionsPublished, url)) return true;
            if (await BlockReferencesAsync(_context.BlocksDraft, url)) return true;
            if (await BlockReferencesAsync(_context.BlocksPublished, url)) return true;
            if (await ContentReferencesAsync(_context.ContentDraft, url)) return true;
            if (await ContentReferencesAsync(_context.ContentPublished, url)) return true;

            return false;
        }

        private async Task<bool> ManagedResourceReferencesAsync(string url, string? excludingManagedResourceId)
        {
            var filter = Builders<ManagedResource>.Filter.Or(
                Builders<ManagedResource>.Filter.Eq(r => r.Url, url),
                Builders<ManagedResource>.Filter.Eq(r => r.ThumbnailUrl, url));

            if (!string.IsNullOrWhiteSpace(excludingManagedResourceId))
                filter &= Builders<ManagedResource>.Filter.Ne(r => r.Id, excludingManagedResourceId.Trim());

            return await _context.ManagedResources.Find(filter).AnyAsync();
        }

        private static async Task<bool> PageReferencesAsync(IMongoCollection<Page> pages, string url) =>
            await pages.Find(p => p.Card != null && p.Card.CardImageUrl == url).AnyAsync();

        private static async Task<bool> SectionReferencesAsync(IMongoCollection<Section> sections, string url)
        {
            var filter = Builders<Section>.Filter.Or(
                Builders<Section>.Filter.Eq("Style.BackgroundImageUrl", url),
                Builders<Section>.Filter.Eq("Style.BackgroundVideoUrl", url),
                Builders<Section>.Filter.Eq("ImageUrl", url),
                Builders<Section>.Filter.Eq("Items.ImageUrl", url),
                Builders<Section>.Filter.Eq("ItemOverrides.CardImageUrl", url));

            return await sections.Find(filter).AnyAsync();
        }

        private static async Task<bool> BlockReferencesAsync(IMongoCollection<Block> blocks, string url)
        {
            var filter = Builders<Block>.Filter.Or(
                Builders<Block>.Filter.Eq("ImageUrl", url),
                Builders<Block>.Filter.Eq("FileUrl", url),
                Builders<Block>.Filter.Eq("EmbedUrl", url));

            return await blocks.Find(filter).AnyAsync();
        }

        private static async Task<bool> ContentReferencesAsync(IMongoCollection<ContentItem> content, string url)
        {
            var filter = Builders<ContentItem>.Filter.Or(
                Builders<ContentItem>.Filter.Eq(c => c.HeroImageUrl, url),
                Builders<ContentItem>.Filter.Eq(c => c.ThumbnailUrl, url),
                Builders<ContentItem>.Filter.Eq(c => c.VideoUrl, url),
                Builders<ContentItem>.Filter.Eq("Attachments.Url", url),
                Builders<ContentItem>.Filter.Eq("BodyItems.Url", url),
                Builders<ContentItem>.Filter.Eq("GalleryItems.Url", url),
                Builders<ContentItem>.Filter.Eq("GalleryItems.ThumbnailUrl", url));

            return await content.Find(filter).AnyAsync();
        }
    }
}
