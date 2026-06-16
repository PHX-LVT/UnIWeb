using FullProject.Data;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class R2AssetService
    {
        private readonly MongoDbContext _context;
        private readonly R2StorageService _storage;

        public R2AssetService(MongoDbContext context, R2StorageService storage)
        {
            _context = context;
            _storage = storage;
        }

        public async Task DeleteIfUnusedAsync(string? oldUrl, string? replacementUrl)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return;
            if (string.Equals(oldUrl, replacementUrl, StringComparison.OrdinalIgnoreCase)) return;
            if (await IsReferencedAsync(oldUrl)) return;

            await _storage.DeleteAsync(oldUrl);
        }

        private async Task<bool> IsReferencedAsync(string url)
        {
            if (await _context.Branding.Find(b => b.LogoUrl == url).AnyAsync()) return true;
            if (await PageReferencesAsync(_context.PagesDraft, url)) return true;
            if (await PageReferencesAsync(_context.PagesPublished, url)) return true;
            if (await SectionReferencesAsync(_context.SectionsDraft, url)) return true;
            if (await SectionReferencesAsync(_context.SectionsPublished, url)) return true;
            if (await BlockReferencesAsync(_context.BlocksDraft, url)) return true;
            if (await BlockReferencesAsync(_context.BlocksPublished, url)) return true;

            return false;
        }

        private static async Task<bool> PageReferencesAsync(IMongoCollection<Page> pages, string url) =>
            await pages.Find(p => p.Card != null && p.Card.CardImageUrl == url).AnyAsync();

        private static async Task<bool> SectionReferencesAsync(IMongoCollection<Section> sections, string url)
        {
            var all = await sections.Find(_ => true).ToListAsync();
            return all.Any(section => SectionReferences(section, url));
        }

        private static async Task<bool> BlockReferencesAsync(IMongoCollection<Block> blocks, string url)
        {
            var all = await blocks.Find(_ => true).ToListAsync();
            return all.Any(block => block switch
            {
                ImageBlock image => string.Equals(image.ImageUrl, url, StringComparison.OrdinalIgnoreCase),
                FileBlock file => string.Equals(file.FileUrl, url, StringComparison.OrdinalIgnoreCase),
                _ => false
            });
        }

        private static bool SectionReferences(Section section, string url)
        {
            if (string.Equals(section.Style?.BackgroundImageUrl, url, StringComparison.OrdinalIgnoreCase)) return true;

            return section switch
            {
                HeroSection hero => string.Equals(hero.ImageUrl, url, StringComparison.OrdinalIgnoreCase),
                GallerySection gallery => gallery.Images.Any(i => string.Equals(i.ImageUrl, url, StringComparison.OrdinalIgnoreCase)),
                ListSection list => list.Items.Any(i => string.Equals(i.ImageUrl, url, StringComparison.OrdinalIgnoreCase)),
                CarouselSection carousel => carousel.Items.Any(i => string.Equals(i.ImageUrl, url, StringComparison.OrdinalIgnoreCase)),
                ShowcaseSection showcase => showcase.ItemOverrides.Any(i => string.Equals(i.CardImageUrl, url, StringComparison.OrdinalIgnoreCase)),
                _ => false
            };
        }
    }
}
