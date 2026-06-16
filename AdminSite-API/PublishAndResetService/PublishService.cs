using FullProject.Data;
using FullProject.Models;
using FullProject.Utils;
using MongoDB.Driver;

namespace FullProject.Services
{
    public class PublishService
    {
        private readonly MongoDbContext _context;

        public PublishService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<PublishResult> PublishPageAsync(string pageId)
        {
            var page = await _context.PagesDraft
                .Find(p => p.Id == pageId)
                .FirstOrDefaultAsync();
            if (page is null)
                return PublishResult.Fail("Page not found.");

            var now = DateTime.UtcNow;

            using var session = await _context.Client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                // Fetch all draft sections and blocks for this page
                var sections = await _context.SectionsDraft
                    .Find(session, s => s.PageStableId == page.StableId)
                    .ToListAsync();

                var blocks = await _context.BlocksDraft
                    .Find(session, b => b.PageStableId == page.StableId)
                    .ToListAsync();

                // Wipe existing published data for this page
                await _context.BlocksPublished.DeleteManyAsync(
                    session, b => b.PageStableId == page.StableId);
                await _context.SectionsPublished.DeleteManyAsync(
                    session, s => s.PageStableId == page.StableId);
                await _context.PagesPublished.DeleteOneAsync(
                    session, p => p.StableId == page.StableId);

                // Clone and insert page into published
                var publishedPage = CloneUtility.ClonePage(page, publishedAt: now);
                await _context.PagesPublished.InsertOneAsync(session, publishedPage);

                if (string.IsNullOrWhiteSpace(page.ParentPageId))
                    await SyncPublishedChildPageCardsAsync(session, page, now);

                // Clone and insert sections into published
                if (sections.Any())
                {
                    var publishedSections = sections
                        .Select(s => CloneUtility.CloneSection(s, publishedAt: now))
                        .ToList();
                    await _context.SectionsPublished.InsertManyAsync(session, publishedSections);
                }

                // Clone and insert blocks into published
                if (blocks.Any())
                {
                    var publishedBlocks = blocks
                        .Select(b => CloneUtility.CloneBlock(b, publishedAt: now))
                        .ToList();
                    await _context.BlocksPublished.InsertManyAsync(session, publishedBlocks);
                }

                // Update draft page status ? Published
                await _context.PagesDraft.UpdateOneAsync(
                    session,
                    p => p.Id == pageId,
                    Builders<Page>.Update
                        .Set(p => p.Status, PageStatus.Published)
                        .Set(p => p.PublishedAt, now)
                        .Set(p => p.UpdatedAt, now)
                        .Inc(p => p.Version, 1));

                await session.CommitTransactionAsync();
                return PublishResult.Ok(now);
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync();
                return PublishResult.Fail($"Publish failed: {ex.Message}");
            }
        }

        private async Task SyncPublishedChildPageCardsAsync(IClientSessionHandle session, Page parentPage, DateTime publishedAt)
        {
            var draftChildren = await _context.PagesDraft
                .Find(session, p => p.ParentPageId == parentPage.Id)
                .ToListAsync();

            foreach (var draftChild in draftChildren)
            {
                var alreadyPublished = await _context.PagesPublished
                    .Find(session, p => p.StableId == draftChild.StableId)
                    .AnyAsync();

                if (!alreadyPublished)
                    continue;

                var update = Builders<Page>.Update
                    .Set(p => p.SourceId, draftChild.Id)
                    .Set(p => p.Version, draftChild.Version + 1)
                    .Set(p => p.PublishedAt, publishedAt)
                    .Set(p => p.Name, new Dictionary<string, string>(draftChild.Name))
                    .Set(p => p.Slug, draftChild.Slug)
                    .Set(p => p.FullSlug, draftChild.FullSlug)
                    .Set(p => p.ParentPageId, draftChild.ParentPageId)
                    .Set(p => p.ParentSlug, draftChild.ParentSlug)
                    .Set(p => p.Access, draftChild.Access)
                    .Set(p => p.Visible, draftChild.Visible)
                    .Set(p => p.Order, draftChild.Order)
                    .Set(p => p.Status, PageStatus.Published)
                    .Set(p => p.Seo, new PageSeo
                    {
                        MetaTitle = new Dictionary<string, string>(draftChild.Seo.MetaTitle),
                        MetaDescription = new Dictionary<string, string>(draftChild.Seo.MetaDescription)
                    })
                    .Set(p => p.Card, draftChild.Card != null ? new PageCard
                    {
                        CardTitle = new Dictionary<string, string>(draftChild.Card.CardTitle),
                        CardContent = new Dictionary<string, string>(draftChild.Card.CardContent),
                        CardBackgroundType = draftChild.Card.CardBackgroundType,
                        CardBackgroundColor = draftChild.Card.CardBackgroundColor,
                        CardImageUrl = draftChild.Card.CardImageUrl,
                        IsCustomized = draftChild.Card.IsCustomized
                    } : null)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow);

                await _context.PagesPublished.UpdateOneAsync(
                    session,
                    p => p.StableId == draftChild.StableId,
                    update);
            }
        }
    }

   
}
