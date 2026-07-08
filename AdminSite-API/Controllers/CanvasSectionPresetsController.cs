using Contracts.Admin;
using Contracts.Auth;
using FullProject.Models;
using FullProject.Services.SectionServices;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FullProject.Controllers;

[ApiController]
[Authorize(Policy = AdminPermissionKeys.PageBuilder)]
[Route("api/admin/section-presets")]
[Route("api/admin/canvas-section-presets")]
public sealed class SectionPresetsController : ControllerBase
{
    private readonly SectionPresetService _service;

    public SectionPresetsController(SectionPresetService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var presets = await _service.GetAllAsync();
        return Ok(ApiResult.Ok(presets.Select(MapToDto).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SectionPresetCreateDto dto)
    {
        var (preset, error) = await _service.CreateFromSectionAsync(dto);
        if (error is not null)
            return BadRequest(ApiResult.BadRequest(error));

        return Ok(ApiResult.Ok(MapToDto(preset!), "Section preset saved."));
    }

    [HttpPost("{presetId}/apply")]
    public async Task<IActionResult> Apply(string presetId, [FromBody] SectionPresetApplyDto dto)
    {
        var (section, error) = await _service.ApplyAsync(presetId, dto);
        if (error is not null)
            return BadRequest(ApiResult.BadRequest(error));

        return Ok(ApiResult.Ok(
            new SectionPresetApplyResponseDto { SectionId = section!.Id },
            "Section preset inserted."));
    }

    [HttpDelete("{presetId}")]
    public async Task<IActionResult> Delete(string presetId)
    {
        var deleted = await _service.DeleteAsync(presetId);
        if (!deleted)
            return NotFound(ApiResult.NotFound("Section preset not found."));

        return Ok(ApiResult.Ok("Section preset deleted."));
    }

    private SectionPresetResponseDto MapToDto(SectionPreset preset)
    {
        var compatibility = _service.Compatibility(preset);
        var itemCount = CountItems(preset);
        return new SectionPresetResponseDto
        {
            Id = preset.Id,
            Name = preset.Name,
            Description = preset.Description,
            PreviewText = preset.PreviewText,
            SectionType = preset.SectionType,
            ThumbnailUrl = preset.ThumbnailUrl,
            ThumbnailBackground = preset.ThumbnailBackground,
            SummaryLabel = SummaryLabel(preset, itemCount),
            IconClass = IconClass(preset.SectionType),
            VisualKey = VisualKey(preset.SectionType),
            ItemCount = itemCount,
            BlockCount = preset.Blocks.Count,
            SchemaVersion = preset.SchemaVersion,
            IsCompatible = compatibility.IsCompatible,
            CompatibilityMessage = compatibility.Message,
            CreatedAt = preset.CreatedAt,
            UpdatedAt = preset.UpdatedAt
        };
    }

    private static int CountItems(SectionPreset preset) =>
        preset.Section switch
        {
            ListSection list => list.Items.Count(item => item.Visible),
            StatsSection stats => stats.Items.Count(item => item.Visible),
            CarouselSection carousel => carousel.Items.Count(item => item.Visible),
            NetworkMapSection map => map.Pins.Count(pin => pin.Visible),
            TestimonialSection testimonial => testimonial.Items.Count(item => item.Visible),
            ShowcaseSection showcase => showcase.ItemOverrides.Count,
            LibrarySection library => library.ContentTypes.Count,
            ColumnsSection columns => preset.Blocks.Count > 0
                ? preset.Blocks.Count
                : columns.Columns.Sum(column => column.Blocks.Count),
            CanvasSection => preset.Blocks.Count,
            _ => 0
        };

    private static string SummaryLabel(SectionPreset preset, int itemCount)
    {
        var blockCount = preset.Blocks.Count;
        return preset.Section switch
        {
            HtmlSection => "HTML / Custom CSS",
            ShowcaseSection showcase when !string.IsNullOrWhiteSpace(showcase.SourcePageId) => "Showcase / Dynamic Source",
            ShowcaseSection when itemCount > 0 => CountLabel("Showcase", itemCount, "Item"),
            LibrarySection => "Library / Dynamic Source",
            ColumnsSection when blockCount > 0 => CountLabel("Columns", blockCount, "Block"),
            ColumnsSection columns => CountLabel("Columns", Math.Max(columns.ColumnCount, itemCount), "Column"),
            CanvasSection when blockCount > 0 => CountLabel("Canvas", blockCount, "Block"),
            CanvasSection => "Canvas / Block Section",
            ListSection => CountLabel("List", itemCount, "Item"),
            StatsSection => CountLabel("Stats", itemCount, "Item"),
            CarouselSection => CountLabel("Carousel", itemCount, "Item"),
            NetworkMapSection => CountLabel("Map", itemCount, "Pin"),
            TestimonialSection => CountLabel("Testimonial", itemCount, "Item"),
            HeroSection => "Hero",
            CtaSection => "CTA",
            _ => string.IsNullOrWhiteSpace(preset.SectionType)
                ? "Saved Section"
                : ToTitle(preset.SectionType)
        };
    }

    private static string CountLabel(string label, int count, string unit) =>
        count > 0
            ? $"{label} · {count} {unit}{(count == 1 ? string.Empty : "s")}"
            : label;

    private static string IconClass(string sectionType) => sectionType switch
    {
        "hero" => "fa-image",
        "cta" => "fa-bullhorn",
        "columns" => "fa-columns",
        "list" => "fa-th-large",
        "stats" => "fa-chart-line",
        "testimonial" => "fa-comments",
        "carousel" => "fa-window-restore",
        "showcase" => "fa-sitemap",
        "library" => "fa-newspaper",
        "network-map" => "fa-map-location-dot",
        "html" => "fa-code",
        "canvas" => "fa-object-group",
        _ => "fa-layer-group"
    };

    private static string VisualKey(string sectionType) => sectionType switch
    {
        "html" => "code",
        "showcase" or "library" => "dynamic",
        "columns" or "stats" or "testimonial" => "grid",
        "carousel" => "carousel",
        "network-map" => "map",
        "canvas" => "canvas",
        _ => "section"
    };

    private static string ToTitle(string value)
    {
        var words = value.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0
            ? value
            : string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}
