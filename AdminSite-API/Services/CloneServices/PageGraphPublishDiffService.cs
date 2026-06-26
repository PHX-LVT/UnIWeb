using FullProject.Models;
using MongoDB.Bson;

namespace FullProject.Services.CloneServices
{
    public sealed class PageGraphPublishDiffService
    {
        public PageGraphPublishDiffResult BuildDiff(
            Page draftPage,
            IEnumerable<Section> draftSections,
            IEnumerable<Block> draftBlocks,
            Page? publishedPage,
            IEnumerable<Section>? publishedSections,
            IEnumerable<Block>? publishedBlocks)
        {
            ArgumentNullException.ThrowIfNull(draftPage);

            var issues = new List<string>();
            var draftSectionList = draftSections.ToList();
            var draftBlockList = draftBlocks.ToList();
            var publishedSectionList = (publishedSections ?? []).ToList();
            var publishedBlockList = (publishedBlocks ?? []).ToList();

            var pageToInsert = publishedPage is null ? draftPage : null;
            var pageToUpdate = publishedPage is not null &&
                               !DocumentsEqual(draftPage, publishedPage, PageGraphDocumentKind.Page)
                ? new PublishDiffUpdate<Page>(draftPage, publishedPage)
                : null;

            var sectionDiff = BuildCollectionDiff(
                draftSectionList,
                publishedSectionList,
                section => section.StableId,
                PageGraphDocumentKind.Section,
                "section",
                issues);

            var blockDiff = BuildCollectionDiff(
                draftBlockList,
                publishedBlockList,
                block => block.StableId,
                PageGraphDocumentKind.Block,
                "block",
                issues);

            return new PageGraphPublishDiffResult(
                draftPage,
                publishedPage,
                pageToInsert,
                pageToUpdate,
                sectionDiff.ToInsert,
                sectionDiff.ToUpdate,
                sectionDiff.ToDelete,
                sectionDiff.UnchangedStableIds,
                blockDiff.ToInsert,
                blockDiff.ToUpdate,
                blockDiff.ToDelete,
                blockDiff.UnchangedStableIds,
                issues);
        }

        private CollectionPublishDiff<T> BuildCollectionDiff<T>(
            IReadOnlyList<T> draftItems,
            IReadOnlyList<T> publishedItems,
            Func<T, string?> stableIdSelector,
            PageGraphDocumentKind documentKind,
            string itemLabel,
            List<string> issues)
            where T : class
        {
            var draftByStableId = IndexByStableId(draftItems, stableIdSelector, $"draft {itemLabel}", issues);
            var publishedByStableId = IndexByStableId(publishedItems, stableIdSelector, $"published {itemLabel}", issues);

            var toInsert = new List<T>();
            var toUpdate = new List<PublishDiffUpdate<T>>();
            var toDelete = new List<T>();
            var unchangedStableIds = new List<string>();

            foreach (var draft in draftItems)
            {
                var stableId = stableIdSelector(draft);
                if (string.IsNullOrWhiteSpace(stableId))
                    continue;

                if (!publishedByStableId.TryGetValue(stableId, out var published))
                {
                    toInsert.Add(draft);
                    continue;
                }

                if (DocumentsEqual(draft, published, documentKind))
                {
                    unchangedStableIds.Add(stableId);
                    continue;
                }

                toUpdate.Add(new PublishDiffUpdate<T>(draft, published));
            }

            foreach (var published in publishedItems)
            {
                var stableId = stableIdSelector(published);
                if (string.IsNullOrWhiteSpace(stableId))
                    continue;

                if (!draftByStableId.ContainsKey(stableId))
                    toDelete.Add(published);
            }

            return new CollectionPublishDiff<T>(toInsert, toUpdate, toDelete, unchangedStableIds);
        }

        private static Dictionary<string, T> IndexByStableId<T>(
            IEnumerable<T> items,
            Func<T, string?> stableIdSelector,
            string itemLabel,
            List<string> issues)
            where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var stableId = stableIdSelector(item);
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    issues.Add($"{itemLabel} has no StableId.");
                    continue;
                }

                if (!result.TryAdd(stableId, item))
                    issues.Add($"{itemLabel} has duplicate StableId '{stableId}'.");
            }

            return result;
        }

        private static bool DocumentsEqual(object draft, object published, PageGraphDocumentKind documentKind)
        {
            var draftDocument = ToComparableDocument(draft, documentKind);
            var publishedDocument = ToComparableDocument(published, documentKind);

            return draftDocument.Equals(publishedDocument);
        }

        private static BsonDocument ToComparableDocument(object source, PageGraphDocumentKind documentKind)
        {
            var document = source is ColumnsSection columnsSection
                ? ToBsonDocumentWithoutEmbeddedColumnBlocks(columnsSection)
                : source.ToBsonDocument(source.GetType());

            RemoveRootFields(
                document,
                "_id",
                "SourceId",
                "Version",
                "PublishedAt",
                "CreatedAt",
                "UpdatedAt");

            if (documentKind == PageGraphDocumentKind.Page)
                document.Remove("Status");

            return document;
        }

        private static BsonDocument ToBsonDocumentWithoutEmbeddedColumnBlocks(ColumnsSection section)
        {
            var originalBlocks = section.Columns
                .Select(column => column.Blocks)
                .ToList();

            try
            {
                foreach (var column in section.Columns)
                {
                    column.Blocks = new List<Block>();
                }

                return section.ToBsonDocument(section.GetType());
            }
            finally
            {
                for (var i = 0; i < section.Columns.Count && i < originalBlocks.Count; i++)
                {
                    section.Columns[i].Blocks = originalBlocks[i];
                }
            }
        }

        private static void RemoveRootFields(BsonDocument document, params string[] fieldNames)
        {
            foreach (var fieldName in fieldNames)
            {
                document.Remove(fieldName);
            }
        }

        private enum PageGraphDocumentKind
        {
            Page,
            Section,
            Block
        }

        private sealed record CollectionPublishDiff<T>(
            IReadOnlyList<T> ToInsert,
            IReadOnlyList<PublishDiffUpdate<T>> ToUpdate,
            IReadOnlyList<T> ToDelete,
            IReadOnlyList<string> UnchangedStableIds);
    }

    public sealed record PublishDiffUpdate<T>(T Draft, T Published);

    public sealed record PageGraphPublishDiffResult(
        Page DraftPage,
        Page? PublishedPage,
        Page? PageToInsert,
        PublishDiffUpdate<Page>? PageToUpdate,
        IReadOnlyList<Section> SectionsToInsert,
        IReadOnlyList<PublishDiffUpdate<Section>> SectionsToUpdate,
        IReadOnlyList<Section> SectionsToDelete,
        IReadOnlyList<string> UnchangedSectionStableIds,
        IReadOnlyList<Block> BlocksToInsert,
        IReadOnlyList<PublishDiffUpdate<Block>> BlocksToUpdate,
        IReadOnlyList<Block> BlocksToDelete,
        IReadOnlyList<string> UnchangedBlockStableIds,
        IReadOnlyList<string> IntegrityIssues)
    {
        public bool HasIntegrityIssues => IntegrityIssues.Count > 0;

        public bool HasChanges =>
            PageToInsert is not null ||
            PageToUpdate is not null ||
            SectionsToInsert.Count > 0 ||
            SectionsToUpdate.Count > 0 ||
            SectionsToDelete.Count > 0 ||
            BlocksToInsert.Count > 0 ||
            BlocksToUpdate.Count > 0 ||
            BlocksToDelete.Count > 0;
    }
}
