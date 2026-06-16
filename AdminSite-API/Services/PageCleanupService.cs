using FullProject.Data;
using FullProject.Models;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class PageCleanupService
    {
        private readonly MongoDbContext _context;

        public PageCleanupService(MongoDbContext context)
        {
            _context = context;
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

            // Fetch all sections to get their StableIds for block cleanup
            var sections = await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId)
                .ToListAsync();

            // Delete all blocks (draft + published) per section
            foreach (var section in sections)
            {
                await _context.BlocksDraft.DeleteManyAsync(
                    b => b.SectionStableId == section.StableId);
                await _context.BlocksPublished.DeleteManyAsync(
                    b => b.SectionStableId == section.StableId);
            }

            // Delete all sections (draft + published) for this page
            await _context.SectionsDraft.DeleteManyAsync(
                s => s.PageStableId == page.StableId);
            await _context.SectionsPublished.DeleteManyAsync(
                s => s.PageStableId == page.StableId);

            // Delete page itself (draft + published)
            await _context.PagesDraft.DeleteOneAsync(p => p.Id == pageId);
            await _context.PagesPublished.DeleteOneAsync(
                p => p.StableId == page.StableId);
        }
    }
}