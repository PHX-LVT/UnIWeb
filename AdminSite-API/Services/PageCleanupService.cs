using FullProject.Data;
using FullProject.Models;
using GlobalManager.Services.AssetService;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class PageCleanupService
    {
        private readonly MongoDbContext _context;
        private readonly AssetCleanupService _assetCleanup;

        public PageCleanupService(MongoDbContext context, AssetCleanupService assetCleanup)
        {
            _context = context;
            _assetCleanup = assetCleanup;
        }

        public async Task DeletePageAndDependenciesAsync(string pageId)
        {
            // Fetch page to get its StableId
            var page = await _context.PagesDraft
                .Find(p => p.Id == pageId)
                .FirstOrDefaultAsync();
            if (page is null) return;

            // Recursively delete children first
            var children = await _context.PagesDraft
                .Find(p => p.ParentPageId == pageId)
                .ToListAsync();
            foreach (var child in children)
                await DeletePageAndDependenciesAsync(child.Id);

            var publishedPage = await _context.PagesPublished
                .Find(p => p.StableId == page.StableId)
                .FirstOrDefaultAsync();

            var draftSections = await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId)
                .ToListAsync();

            var publishedSections = await _context.SectionsPublished
                .Find(s => s.PageStableId == page.StableId)
                .ToListAsync();

            var draftBlocks = await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId)
                .ToListAsync();

            var publishedBlocks = await _context.BlocksPublished
                .Find(b => b.PageStableId == page.StableId)
                .ToListAsync();

            var removedPages = new[] { page, publishedPage };
            var removedSections = draftSections.Concat(publishedSections).ToList();
            var removedBlocks = draftBlocks.Concat(publishedBlocks).ToList();

            // Delete all blocks (draft + published) for this page
            await _context.BlocksDraft.DeleteManyAsync(
                b => b.PageStableId == page.StableId);
            await _context.BlocksPublished.DeleteManyAsync(
                b => b.PageStableId == page.StableId);

            // Delete all sections (draft + published) for this page
            await _context.SectionsDraft.DeleteManyAsync(
                s => s.PageStableId == page.StableId);
            await _context.SectionsPublished.DeleteManyAsync(
                s => s.PageStableId == page.StableId);

            // Delete page itself (draft + published)
            await _context.PagesDraft.DeleteOneAsync(p => p.Id == pageId);
            await _context.PagesPublished.DeleteOneAsync(
                p => p.StableId == page.StableId);

            await _assetCleanup.DeleteUnusedPageGraphAssetsAsync(removedPages, removedSections, removedBlocks);
        }
    }
}
