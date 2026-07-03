using Contracts.Admin;
using FullProject.Data;
using FullProject.Models;
using FullProject.Services.CloneServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FullProject.Services.SectionServices;

public sealed class CanvasSectionPresetService
{
    private readonly MongoDbContext _context;
    private readonly PageGraphCloneService _cloneService;
    private readonly CanvasPresetContractService _contracts;

    public CanvasSectionPresetService(
        MongoDbContext context,
        PageGraphCloneService cloneService,
        CanvasPresetContractService contracts)
    {
        _context = context;
        _cloneService = cloneService;
        _contracts = contracts;
    }

    public async Task<List<CanvasSectionPreset>> GetAllAsync()
    {
        var presets = await _context.CanvasSectionPresets
            .Find(_ => true)
            .SortByDescending(preset => preset.UpdatedAt)
            .ToListAsync();
        foreach (var preset in presets.Where(preset => _contracts.Compatibility(preset).IsCompatible))
            _contracts.UpgradeInPlace(preset);
        return presets;
    }

    public async Task<(CanvasSectionPreset? Preset, string? Error)> CreateFromSectionAsync(
        CanvasSectionPresetCreateDto dto)
    {
        var page = await _context.PagesDraft.Find(item => item.Id == dto.PageId).FirstOrDefaultAsync();
        if (page is null) return (null, "Page not found.");
        var section = await _context.SectionsDraft
            .Find(item => item.PageStableId == page.StableId && item.Id == dto.SectionId)
            .FirstOrDefaultAsync();
        if (section is not CanvasSection canvas) return (null, "Canvas section not found.");

        var sourceBlocks = await _context.BlocksDraft
            .Find(block => block.PageStableId == page.StableId && block.SectionStableId == canvas.StableId)
            .SortBy(block => block.Order)
            .ToListAsync();
        var capturedBlocks = _cloneService.CloneBlocksForPresetCapture(sourceBlocks);
        var capturedStableIds = sourceBlocks
            .Zip(capturedBlocks, (source, captured) => new { source.StableId, CapturedStableId = captured.StableId })
            .ToDictionary(item => item.StableId, item => item.CapturedStableId, StringComparer.Ordinal);
        if (dto.EditableSlots.Any(slot => !capturedStableIds.ContainsKey(slot.BlockStableId)))
            return (null, "An editable preset slot references a Block outside this Canvas section.");

        var preset = new CanvasSectionPreset
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = NormalizeName(dto.Name),
            Style = _cloneService.CloneSectionStyle(canvas.Style, forceFreeform: true),
            Blocks = capturedBlocks,
            SchemaVersion = CanvasPresetContractService.CurrentSchemaVersion,
            EditableSlots = dto.EditableSlots
                .Select(slot => new CanvasPresetEditableSlot
                {
                    Name = CanvasPresetContractService.NormalizeSlotName(slot.Name),
                    BlockStableId = capturedStableIds[slot.BlockStableId],
                    Kind = CanvasPresetContractService.NormalizeSlotKind(slot.Kind),
                    Label = slot.Label ?? new()
                })
                .ToList(),
            LockPolicy = new CanvasPresetLockPolicy
            {
                LockGeometryOnApply = dto.LockPolicy?.LockGeometryOnApply ?? false,
                LockContentOutsideSlots = dto.LockPolicy?.LockContentOutsideSlots ?? false
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var error = _contracts.PrepareAndValidate(preset);
        if (error is not null) return (null, error);
        await _context.CanvasSectionPresets.InsertOneAsync(preset);
        return (preset, null);
    }

    public async Task<(CanvasSection? Section, string? Error)> ApplyAsync(
        string presetId,
        CanvasSectionPresetApplyDto dto)
    {
        var preset = await _context.CanvasSectionPresets.Find(item => item.Id == presetId).FirstOrDefaultAsync();
        if (preset is null) return (null, "Canvas preset not found.");
        var error = _contracts.PrepareAndValidate(preset);
        if (error is not null) return (null, error);
        var page = await _context.PagesDraft.Find(item => item.Id == dto.PageId).FirstOrDefaultAsync();
        if (page is null) return (null, "Page not found.");

        var count = await _context.SectionsDraft.CountDocumentsAsync(section => section.PageStableId == page.StableId);
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

        var blocks = _cloneService.CloneBlocksForPresetApply(
            preset.Blocks,
            page.StableId,
            section.StableId,
            now);
        _contracts.ApplyPolicy(preset, blocks);

        await _context.SectionsDraft.InsertOneAsync(section);
        try
        {
            if (blocks.Count > 0) await _context.BlocksDraft.InsertManyAsync(blocks);
        }
        catch
        {
            await _context.SectionsDraft.DeleteOneAsync(item => item.Id == section.Id);
            throw;
        }
        return (section, null);
    }

    public async Task<bool> DeleteAsync(string presetId)
    {
        var result = await _context.CanvasSectionPresets.DeleteOneAsync(preset => preset.Id == presetId);
        return result.DeletedCount > 0;
    }

    public CanvasPresetCompatibility Compatibility(CanvasSectionPreset preset) =>
        _contracts.Compatibility(preset);

    private static Dictionary<string, string> NormalizeName(Dictionary<string, string>? name) =>
        name is not null && name.Values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? name.ToDictionary(item => item.Key, item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string> { ["en"] = "Canvas Preset" };
}
