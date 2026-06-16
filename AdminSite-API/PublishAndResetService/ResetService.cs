using FullProject.Data;
using FullProject.Models;
using FullProject.Utils;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class ResetService
    {
        private readonly MongoDbContext _context;

        public ResetService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<ResetResult> ResetPageAsync(string pageId)
        {
            // Fetch draft page
            var draftPage = await _context.PagesDraft
                .Find(p => p.Id == pageId)
                .FirstOrDefaultAsync();
            if (draftPage is null)
                return ResetResult.Fail("Page not found.");

            // Reset only available on published pages
            if (draftPage.Status != PageStatus.Published)
                return ResetResult.Fail("Reset is only available for published pages.");

            // Fetch the published version to restore from
            var publishedPage = await _context.PagesPublished
                .Find(p => p.StableId == draftPage.StableId)
                .FirstOrDefaultAsync();
            if (publishedPage is null)
                return ResetResult.Fail("No published version found to reset to.");

            using var session = await _context.Client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Fetch published sections and blocks to restore
                var publishedSections = await _context.SectionsPublished
                    .Find(session, s => s.PageStableId == draftPage.StableId)
                    .ToListAsync();

                var publishedBlocks = await _context.BlocksPublished
                    .Find(session, b => b.PageStableId == draftPage.StableId)
                    .ToListAsync();

                // Wipe all draft sections and blocks for this page
                await _context.BlocksDraft.DeleteManyAsync(
                    session, b => b.PageStableId == draftPage.StableId);
                await _context.SectionsDraft.DeleteManyAsync(
                    session, s => s.PageStableId == draftPage.StableId);

                // Clone published sections → draft
                if (publishedSections.Any())
                {
                    var draftSections = publishedSections
                        .Select(s => CloneUtility.CloneSection(s))
                        .ToList();
                    await _context.SectionsDraft.InsertManyAsync(session, draftSections);
                }

                // Clone published blocks → draft
                if (publishedBlocks.Any())
                {
                    var draftBlocks = publishedBlocks
                        .Select(b => CloneUtility.CloneBlock(b))
                        .ToList();
                    await _context.BlocksDraft.InsertManyAsync(session, draftBlocks);
                }

                // Reset draft page fields to match published version
                await _context.PagesDraft.UpdateOneAsync(
                    session,
                    p => p.Id == pageId,
                    Builders<Page>.Update
                        .Set(p => p.Name, publishedPage.Name)
                        .Set(p => p.Slug, publishedPage.Slug)
                        .Set(p => p.FullSlug, publishedPage.FullSlug)
                        .Set(p => p.Access, publishedPage.Access)
                        .Set(p => p.Visible, publishedPage.Visible)
                        .Set(p => p.Order, publishedPage.Order)
                        .Set(p => p.Seo, publishedPage.Seo)
                        .Set(p => p.Card, publishedPage.Card)
                        .Set(p => p.Status, PageStatus.Published)
                        .Set(p => p.UpdatedAt, DateTime.UtcNow)
                        .Inc(p => p.Version, 1));

                await session.CommitTransactionAsync();
                return ResetResult.Ok();
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync();
                return ResetResult.Fail($"Reset failed: {ex.Message}");
            }
        }
    }

  
}
