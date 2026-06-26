using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services.CloneServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.SectionServices
{
    public class CanvasSectionPresetService
    {
        private readonly MongoDbContext _context;
        private readonly PageGraphCloneService _cloneService;

        public CanvasSectionPresetService(MongoDbContext context, PageGraphCloneService cloneService)
        {
            _context = context;
            _cloneService = cloneService;
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
                Style = _cloneService.CloneSectionStyle(canvas.Style, forceFreeform: true),
                Blocks = _cloneService.CloneBlocksForPresetCapture(blocks),
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
                Style = _cloneService.CloneSectionStyle(preset.Style, forceFreeform: true),
                AdminLabel = new Dictionary<string, string>(preset.Name),
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.SectionsDraft.InsertOneAsync(section);

            var blocks = _cloneService.CloneBlocksForPresetApply(
                preset.Blocks,
                page.StableId,
                section.StableId,
                now);

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

    }
}
