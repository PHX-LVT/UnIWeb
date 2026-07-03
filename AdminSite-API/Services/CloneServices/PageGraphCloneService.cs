using FullProject.Models;
using MongoDB.Bson;

namespace FullProject.Services.CloneServices
{
    public sealed class PageGraphCloneService
    {
        private readonly MongoDocumentCloneService _documents;

        public PageGraphCloneService(MongoDocumentCloneService documents)
        {
            _documents = documents;
        }

        public Page ClonePage(Page source, CloneProfile profile, DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var clone = _documents.Clone(source);

            ApplyPageIdentity(source, clone, profile, now);
            return clone;
        }

        public Section CloneSection(Section source, CloneProfile profile, DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var clone = CloneSectionDocument(source);

            ApplySectionIdentity(source, clone, profile, now);
            NormalizeSectionClone(clone);
            return clone;
        }

        public Block CloneBlock(Block source, CloneProfile profile, DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var clone = _documents.Clone(source);

            ApplyBlockIdentity(source, clone, profile, now);
            return clone;
        }

        public SectionStyle CloneSectionStyle(SectionStyle source, bool forceFreeform = false)
        {
            var clone = _documents.Clone(source);
            if (forceFreeform)
                clone.BlockLayoutMode = "freeform";

            return clone;
        }

        public List<Block> CloneBlocksForPresetCapture(IEnumerable<Block> sourceBlocks, DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var orderedSources = sourceBlocks.OrderBy(block => block.Order).ToList();
            var clones = orderedSources
                .Select(block => CloneBlock(block, CloneProfile.PresetCapture, now))
                .ToList();

            RemapParentBlockIds(orderedSources, clones);
            RemapGraphReferences(orderedSources, clones);
            return clones;
        }

        public List<Block> CloneBlocksForPresetApply(
            IEnumerable<Block> presetBlocks,
            string targetPageStableId,
            string targetSectionStableId,
            DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var orderedSources = presetBlocks.OrderBy(block => block.Order).ToList();
            var clones = orderedSources
                .Select(block =>
                {
                    var clone = CloneBlock(block, CloneProfile.PresetApply, now);
                    clone.PageStableId = targetPageStableId;
                    clone.SectionStableId = targetSectionStableId;
                    return clone;
                })
                .ToList();

            RemapParentBlockIds(orderedSources, clones);
            RemapGraphReferences(orderedSources, clones);
            return clones;
        }

        public List<Block> CloneBlocksAsNewContent(
            IEnumerable<Block> sourceBlocks,
            string targetPageStableId,
            string targetSectionStableId,
            DateTime? timestamp = null)
        {
            var now = timestamp ?? DateTime.UtcNow;
            var orderedSources = sourceBlocks.OrderBy(block => block.Order).ToList();
            var clones = orderedSources
                .Select(block =>
                {
                    var clone = CloneBlock(block, CloneProfile.DuplicateAsNewContent, now);
                    clone.PageStableId = targetPageStableId;
                    clone.SectionStableId = targetSectionStableId;
                    return clone;
                })
                .ToList();

            RemapParentBlockIds(orderedSources, clones, preserveExternalParent: true);
            RemapGraphReferences(orderedSources, clones);
            return clones;
        }

        private static void ApplyPageIdentity(Page source, Page clone, CloneProfile profile, DateTime now)
        {
            clone.Id = ObjectId.GenerateNewId().ToString();
            clone.SourceId = source.Id;
            clone.UpdatedAt = now;

            switch (profile)
            {
                case CloneProfile.PublishSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = now;
                    clone.Status = PageStatus.Published;
                    break;

                case CloneProfile.DraftResetSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = null;
                    clone.Status = PageStatus.Published;
                    break;

                case CloneProfile.DuplicateAsNewContent:
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.Status = PageStatus.Draft;
                    clone.CreatedAt = now;
                    break;

                case CloneProfile.PresetCapture:
                case CloneProfile.PresetApply:
                    throw new InvalidOperationException($"{profile} does not apply to page clones.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
            }
        }

        private static void ApplySectionIdentity(Section source, Section clone, CloneProfile profile, DateTime now)
        {
            clone.Id = ObjectId.GenerateNewId().ToString();
            clone.SourceId = source.Id;
            clone.UpdatedAt = now;

            switch (profile)
            {
                case CloneProfile.PublishSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = now;
                    break;

                case CloneProfile.DraftResetSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = null;
                    break;

                case CloneProfile.DuplicateAsNewContent:
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.SourceId = source.Id;
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.CreatedAt = now;
                    break;

                case CloneProfile.PresetCapture:
                case CloneProfile.PresetApply:
                    throw new InvalidOperationException($"{profile} does not apply to section clones.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
            }
        }

        private static void ApplyBlockIdentity(Block source, Block clone, CloneProfile profile, DateTime now)
        {
            clone.Id = ObjectId.GenerateNewId().ToString();
            clone.SourceId = source.Id;
            clone.UpdatedAt = now;

            switch (profile)
            {
                case CloneProfile.PublishSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = now;
                    break;

                case CloneProfile.DraftResetSnapshot:
                    clone.StableId = source.StableId;
                    clone.Version = source.Version + 1;
                    clone.PublishedAt = null;
                    break;

                case CloneProfile.DuplicateAsNewContent:
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.CreatedAt = now;
                    break;

                case CloneProfile.PresetCapture:
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.PageStableId = string.Empty;
                    clone.SectionStableId = string.Empty;
                    clone.ColumnSlotId = null;
                    clone.CreatedAt = now;
                    break;

                case CloneProfile.PresetApply:
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.ColumnSlotId = null;
                    clone.CreatedAt = now;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
            }
        }

        private static void NormalizeSectionClone(Section clone)
        {
            if (clone is not ColumnsSection columnsSection) return;

            foreach (var column in columnsSection.Columns)
            {
                column.Blocks = new List<Block>();
            }
        }

        private Section CloneSectionDocument(Section source)
        {
            if (source is not ColumnsSection columnsSection)
                return _documents.Clone(source);

            var originalBlocks = columnsSection.Columns
                .Select(column => column.Blocks)
                .ToList();

            try
            {
                foreach (var column in columnsSection.Columns)
                {
                    column.Blocks = new List<Block>();
                }

                return _documents.Clone(source);
            }
            finally
            {
                for (var i = 0; i < columnsSection.Columns.Count && i < originalBlocks.Count; i++)
                {
                    columnsSection.Columns[i].Blocks = originalBlocks[i];
                }
            }
        }

        private static void RemapParentBlockIds(
            IReadOnlyList<Block> sources,
            IReadOnlyList<Block> clones,
            bool preserveExternalParent = false)
        {
            var idMap = sources
                .Zip(clones, (source, clone) => new { source.Id, CloneId = clone.Id })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(item => item.Id, item => item.CloneId, StringComparer.Ordinal);

            foreach (var clone in clones)
            {
                if (string.IsNullOrWhiteSpace(clone.ParentBlockId))
                {
                    clone.ParentBlockId = null;
                    continue;
                }

                clone.ParentBlockId = idMap.TryGetValue(clone.ParentBlockId, out var newParentId)
                    ? newParentId
                    : preserveExternalParent ? clone.ParentBlockId : null;
            }
        }

        private static void RemapGraphReferences(
            IReadOnlyList<Block> sources,
            IReadOnlyList<Block> clones)
        {
            var stableIdMap = sources
                .Zip(clones, (source, clone) => new { source.StableId, CloneStableId = clone.StableId })
                .Where(item => !string.IsNullOrWhiteSpace(item.StableId))
                .ToDictionary(item => item.StableId, item => item.CloneStableId, StringComparer.Ordinal);

            static string Remap(string value, IReadOnlyDictionary<string, string> map) =>
                map.TryGetValue(value, out var remapped) ? remapped : value;

            foreach (var container in clones.OfType<ContainerBlock>())
            {
                foreach (var decoration in container.ContainerLayout.Diagram.Decorations)
                {
                    decoration.FromAnchor = Remap(decoration.FromAnchor, stableIdMap);
                    decoration.ToAnchor = Remap(decoration.ToAnchor, stableIdMap);
                }
                foreach (var connector in container.ContainerLayout.Diagram.Connectors)
                {
                    connector.FromAnchor = Remap(connector.FromAnchor, stableIdMap);
                    connector.ToAnchor = Remap(connector.ToAnchor, stableIdMap);
                }
            }
        }
    }
}
