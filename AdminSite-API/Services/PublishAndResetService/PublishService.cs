using FullProject.Data;
using FullProject.Models;
using FullProject.Services.AssetService;
using FullProject.Services.CloneServices;
using MongoDB.Driver;

namespace FullProject.Services.PublishAndResetService
{
    public class PublishService
    {
        private readonly MongoDbContext _context;
        private readonly AssetCleanupService _assetCleanup;
        private readonly PageGraphCloneService _cloneService;
        private readonly PageGraphPublishDiffService _publishDiff;

        public PublishService(
            MongoDbContext context,
            AssetCleanupService assetCleanup,
            PageGraphCloneService cloneService,
            PageGraphPublishDiffService publishDiff)
        {
            _context = context;
            _assetCleanup = assetCleanup;
            _cloneService = cloneService;
            _publishDiff = publishDiff;
        }

        public async Task<PublishResult> PublishPageAsync(string pageId)
        {
            var page = await _context.PagesDraft
                .Find(p => p.Id == pageId)
                .FirstOrDefaultAsync();
            if (page is null)
                return PublishResult.Fail("Page not found.");

            var now = DateTime.UtcNow;
            var replacedPages = new List<Page?>();
            var replacedSections = new List<Section>();
            var replacedBlocks = new List<Block>();

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

                var existingPublishedPage = await _context.PagesPublished
                    .Find(session, p => p.StableId == page.StableId)
                    .FirstOrDefaultAsync();

                var publishedSections = await _context.SectionsPublished
                    .Find(session, s => s.PageStableId == page.StableId)
                    .ToListAsync();

                var publishedBlocks = await _context.BlocksPublished
                    .Find(session, b => b.PageStableId == page.StableId)
                    .ToListAsync();

                var diff = _publishDiff.BuildDiff(
                    page,
                    sections,
                    blocks,
                    existingPublishedPage,
                    publishedSections,
                    publishedBlocks);

                if (diff.HasIntegrityIssues)
                {
                    await session.AbortTransactionAsync();
                    return PublishResult.Fail(
                        $"Publish failed: {string.Join(" ", diff.IntegrityIssues)}");
                }

                AddReplacedRecordsForCleanup(diff, replacedPages, replacedSections, replacedBlocks);

                await ApplyPageDiffAsync(session, diff, now);
                await TouchPublishedPageMetadataAsync(session, diff, page, now);
                await ApplySectionDiffAsync(session, diff, now);
                await ApplyBlockDiffAsync(session, diff, now);

                if (string.IsNullOrWhiteSpace(page.ParentPageId))
                    replacedPages.AddRange(await SyncPublishedChildPageCardsAsync(session, page, now));

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
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync();
                return PublishResult.Fail($"Publish failed: {ex.Message}");
            }

            await _assetCleanup.DeleteUnusedPageGraphAssetsAsync(replacedPages, replacedSections, replacedBlocks);
            return PublishResult.Ok(now);
        }

        private async Task ApplyPageDiffAsync(
            IClientSessionHandle session,
            PageGraphPublishDiffResult diff,
            DateTime publishedAt)
        {
            if (diff.PageToInsert is not null)
            {
                var publishedPage = _cloneService.ClonePage(
                    diff.PageToInsert,
                    CloneProfile.PublishSnapshot,
                    publishedAt);

                await _context.PagesPublished.InsertOneAsync(session, publishedPage);
                return;
            }

            if (diff.PageToUpdate is not null)
            {
                var publishedPage = _cloneService.ClonePage(
                    diff.PageToUpdate.Draft,
                    CloneProfile.PublishSnapshot,
                    publishedAt);
                publishedPage.Id = diff.PageToUpdate.Published.Id;

                await _context.PagesPublished.ReplaceOneAsync(
                    session,
                    p => p.Id == diff.PageToUpdate.Published.Id,
                    publishedPage);
            }
        }

        private async Task TouchPublishedPageMetadataAsync(
            IClientSessionHandle session,
            PageGraphPublishDiffResult diff,
            Page draftPage,
            DateTime publishedAt)
        {
            if (diff.PublishedPage is null || diff.PageToUpdate is not null)
                return;

            await _context.PagesPublished.UpdateOneAsync(
                session,
                p => p.Id == diff.PublishedPage.Id,
                Builders<Page>.Update
                    .Set(p => p.SourceId, draftPage.Id)
                    .Set(p => p.Version, draftPage.Version + 1)
                    .Set(p => p.PublishedAt, publishedAt)
                    .Set(p => p.Status, PageStatus.Published)
                    .Set(p => p.UpdatedAt, publishedAt));
        }

        private async Task ApplySectionDiffAsync(
            IClientSessionHandle session,
            PageGraphPublishDiffResult diff,
            DateTime publishedAt)
        {
            if (diff.SectionsToDelete.Count > 0)
            {
                var deletedIds = diff.SectionsToDelete.Select(s => s.Id).ToList();
                await _context.SectionsPublished.DeleteManyAsync(
                    session,
                    s => deletedIds.Contains(s.Id));
            }

            if (diff.SectionsToInsert.Count > 0)
            {
                var insertedSections = diff.SectionsToInsert
                    .Select(s => _cloneService.CloneSection(s, CloneProfile.PublishSnapshot, publishedAt))
                    .ToList();
                await _context.SectionsPublished.InsertManyAsync(session, insertedSections);
            }

            foreach (var update in diff.SectionsToUpdate)
            {
                var publishedSection = _cloneService.CloneSection(
                    update.Draft,
                    CloneProfile.PublishSnapshot,
                    publishedAt);
                publishedSection.Id = update.Published.Id;

                await _context.SectionsPublished.ReplaceOneAsync(
                    session,
                    s => s.Id == update.Published.Id,
                    publishedSection);
            }
        }

        private async Task ApplyBlockDiffAsync(
            IClientSessionHandle session,
            PageGraphPublishDiffResult diff,
            DateTime publishedAt)
        {
            if (diff.BlocksToDelete.Count > 0)
            {
                var deletedIds = diff.BlocksToDelete.Select(b => b.Id).ToList();
                await _context.BlocksPublished.DeleteManyAsync(
                    session,
                    b => deletedIds.Contains(b.Id));
            }

            if (diff.BlocksToInsert.Count > 0)
            {
                var insertedBlocks = diff.BlocksToInsert
                    .Select(b => _cloneService.CloneBlock(b, CloneProfile.PublishSnapshot, publishedAt))
                    .ToList();
                await _context.BlocksPublished.InsertManyAsync(session, insertedBlocks);
            }

            foreach (var update in diff.BlocksToUpdate)
            {
                var publishedBlock = _cloneService.CloneBlock(
                    update.Draft,
                    CloneProfile.PublishSnapshot,
                    publishedAt);
                publishedBlock.Id = update.Published.Id;

                await _context.BlocksPublished.ReplaceOneAsync(
                    session,
                    b => b.Id == update.Published.Id,
                    publishedBlock);
            }
        }

        private static void AddReplacedRecordsForCleanup(
            PageGraphPublishDiffResult diff,
            List<Page?> replacedPages,
            List<Section> replacedSections,
            List<Block> replacedBlocks)
        {
            if (diff.PageToUpdate is not null)
                replacedPages.Add(diff.PageToUpdate.Published);

            replacedSections.AddRange(diff.SectionsToUpdate.Select(update => update.Published));
            replacedSections.AddRange(diff.SectionsToDelete);
            replacedBlocks.AddRange(diff.BlocksToUpdate.Select(update => update.Published));
            replacedBlocks.AddRange(diff.BlocksToDelete);
        }

        private async Task<List<Page>> SyncPublishedChildPageCardsAsync(IClientSessionHandle session, Page parentPage, DateTime publishedAt)
        {
            var replacedPages = new List<Page>();
            var draftChildren = await _context.PagesDraft
                .Find(session, p => p.ParentPageId == parentPage.Id)
                .ToListAsync();

            foreach (var draftChild in draftChildren)
            {
                var publishedChild = await _context.PagesPublished
                    .Find(session, p => p.StableId == draftChild.StableId)
                    .FirstOrDefaultAsync();

                if (publishedChild is null)
                    continue;

                replacedPages.Add(publishedChild);

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

            return replacedPages;
        }
    }

   
}
