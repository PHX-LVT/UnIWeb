using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.SectionServices
{
    public class CanvasSectionPresetService
    {
        private readonly MongoDbContext _context;

        public CanvasSectionPresetService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<CanvasSectionPreset>> GetAllAsync() =>
            await _context.CanvasSectionPresets
                .Find(_ => true)
                .SortByDescending(p => p.UpdatedAt)
                .ToListAsync();

        public async Task<CanvasSectionPreset?> CreateFromSectionAsync(CanvasSectionPresetCreateDto dto)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == dto.PageId).FirstOrDefaultAsync();
            if (page is null) return null;

            var section = await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId && s.Id == dto.SectionId)
                .FirstOrDefaultAsync();
            if (section is not CanvasSection canvas) return null;

            var blocks = await _context.BlocksDraft
                .Find(b => b.PageStableId == page.StableId && b.SectionStableId == canvas.StableId)
                .SortBy(b => b.Order)
                .ToListAsync();

            var preset = new CanvasSectionPreset
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = NormalizeName(dto.Name),
                Style = CloneStyle(canvas.Style),
                Blocks = blocks.Select(block => CloneUtility.CloneBlock(block)).ToList(),
                SchemaVersion = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.CanvasSectionPresets.InsertOneAsync(preset);
            return preset;
        }

        public async Task<CanvasSection?> ApplyAsync(string presetId, CanvasSectionPresetApplyDto dto)
        {
            var preset = await _context.CanvasSectionPresets.Find(p => p.Id == presetId).FirstOrDefaultAsync();
            var page = await _context.PagesDraft.Find(p => p.Id == dto.PageId).FirstOrDefaultAsync();
            if (preset is null || page is null) return null;

            var count = await _context.SectionsDraft.CountDocumentsAsync(s => s.PageStableId == page.StableId);
            var now = DateTime.UtcNow;
            var section = new CanvasSection
            {
                Id = ObjectId.GenerateNewId().ToString(),
                StableId = Guid.NewGuid().ToString(),
                SourceId = preset.Id,
                Version = 1,
                PageStableId = page.StableId,
                Visible = true,
                Order = (int)count,
                Style = CloneStyle(preset.Style),
                AdminLabel = new Dictionary<string, string>(preset.Name),
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.SectionsDraft.InsertOneAsync(section);

            var blocks = preset.Blocks
                .OrderBy(b => b.Order)
                .Select(b =>
                {
                    var clone = CloneUtility.CloneBlock(b);
                    clone.Id = ObjectId.GenerateNewId().ToString();
                    clone.StableId = Guid.NewGuid().ToString();
                    clone.SourceId = b.Id;
                    clone.Version = 1;
                    clone.PublishedAt = null;
                    clone.PageStableId = page.StableId;
                    clone.SectionStableId = section.StableId;
                    clone.ColumnSlotId = null;
                    clone.CreatedAt = now;
                    clone.UpdatedAt = now;
                    return clone;
                })
                .ToList();

            var blockIdMap = preset.Blocks
                .OrderBy(block => block.Order)
                .Zip(blocks, (source, clone) => new { source.Id, CloneId = clone.Id })
                .ToDictionary(item => item.Id, item => item.CloneId, StringComparer.Ordinal);

            foreach (var block in blocks)
            {
                block.ParentBlockId = !string.IsNullOrWhiteSpace(block.ParentBlockId) &&
                                      blockIdMap.TryGetValue(block.ParentBlockId, out var newParentId)
                    ? newParentId
                    : null;
            }

            if (blocks.Count > 0)
                await _context.BlocksDraft.InsertManyAsync(blocks);

            return section;
        }

        public async Task<bool> DeleteAsync(string presetId)
        {
            var result = await _context.CanvasSectionPresets.DeleteOneAsync(p => p.Id == presetId);
            return result.DeletedCount > 0;
        }

        private static Dictionary<string, string> NormalizeName(Dictionary<string, string>? name)
        {
            if (name is not null && name.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                return name;

            return new Dictionary<string, string> { ["en"] = "Canvas Preset" };
        }

        private static SectionStyle CloneStyle(SectionStyle s) => new()
        {
            BackgroundType = s.BackgroundType,
            BackgroundColor = s.BackgroundColor,
            BackgroundImageUrl = s.BackgroundImageUrl,
            BackgroundVideoUrl = s.BackgroundVideoUrl,
            BackgroundImageFit = s.BackgroundImageFit,
            BackgroundImagePosition = s.BackgroundImagePosition,
            GradientFrom = s.GradientFrom,
            GradientTo = s.GradientTo,
            GradientDirection = s.GradientDirection,
            OverlayColor = s.OverlayColor,
            OverlayOpacity = s.OverlayOpacity,
            Height = s.Height,
            CustomMinHeightPx = s.CustomMinHeightPx,
            Padding = s.Padding,
            ContentWidth = s.ContentWidth,
            TextColor = s.TextColor,
            MobileLayout = s.MobileLayout,
            BlockLayoutMode = "freeform",
            BlockGridColumns = s.BlockGridColumns,
            BlockGap = s.BlockGap
        };
    }
}
