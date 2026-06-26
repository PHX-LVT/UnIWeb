using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using FullProject.Security;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Admin;
using FullProject.Services.SectionServices;

namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/pages/{pageId}/sections/{sectionId}/blocks")]
    [Authorize]
    public class BlocksController : ControllerBase
    {
        private readonly BlockService _service;
        private readonly SectionService _sectionService;

        public BlocksController(BlockService service, SectionService sectionService)
        {
            _service = service;
            _sectionService = sectionService;
        }

        // GET api/admin/pages/:pageId/sections/:sectionId/blocks
        [HttpGet]
        public async Task<IActionResult> GetAll(string pageId, string sectionId)
        {
            var section = await _sectionService.GetByIdAsync(pageId, sectionId);
            if (section is null) return NotFound(ApiResult.NotFound("Section not found."));

            var blocks = await _service.GetBySectionAsync(pageId, sectionId);
            return Ok(ApiResult.Ok(blocks
                .Select(b => MapToDto(pageId, sectionId, b))
                .ToList()));
        }

        // GET api/admin/pages/:pageId/blocks
        [HttpGet("/api/admin/pages/{pageId}/blocks")]
        public async Task<IActionResult> GetAllForPage(string pageId)
        {
            var sections = await _sectionService.GetByPageAsync(pageId);
            if (sections.Count == 0)
                return Ok(ApiResult.Ok(new List<BlockResponseDto>()));

            var sectionIdsByStableId = sections.ToDictionary(s => s.StableId, s => s.Id);
            var blocks = await _service.GetByPageAsync(pageId);

            return Ok(ApiResult.Ok(blocks
                .Select(b => MapToDto(
                    pageId,
                    sectionIdsByStableId.TryGetValue(b.SectionStableId, out var sectionId)
                        ? sectionId
                        : b.SectionStableId,
                    b))
                .ToList()));
        }

        // GET api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId
        [HttpGet("{blockId}")]
        public async Task<IActionResult> GetById(string pageId, string sectionId, string blockId)
        {
            var block = await _service.GetByIdAsync(pageId, sectionId, blockId);
            if (block is null) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, block)));
        }

        // POST api/admin/pages/:pageId/sections/:sectionId/blocks
        [HttpPost]
        public async Task<IActionResult> Create(string pageId, string sectionId,
            [FromBody] BlockCreateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var section = await _sectionService.GetByIdAsync(pageId, sectionId);
            if (section is null) return NotFound(ApiResult.NotFound("Section not found."));

            var created = await _service.CreateAsync(pageId, sectionId, dto);
            return CreatedAtAction(nameof(GetById),
                new { pageId, sectionId, blockId = created.Id },
                ApiResult.Created(MapToDto(pageId, sectionId, created), "Block created."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId
        [HttpPut("{blockId}")]
        public async Task<IActionResult> Update(string pageId, string sectionId, string blockId,
            [FromBody] BlockUpdateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var updated = await _service.UpdateAsync(pageId, sectionId, blockId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, updated), "Block updated."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId/layout
        [HttpPut("{blockId}/layout")]
        public async Task<IActionResult> UpdateLayout(string pageId, string sectionId, string blockId,
            [FromBody] BlockLayoutDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var updated = await _service.UpdateLayoutAsync(pageId, sectionId, blockId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, updated), "Block layout updated."));
        }

        // DELETE api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId
        [HttpDelete("{blockId}")]
        public async Task<IActionResult> Delete(string pageId, string sectionId, string blockId)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.DeleteAsync(pageId, sectionId, blockId);
            if (!ok) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok("Block deleted."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId/visibility
        [HttpPut("{blockId}/visibility")]
        public async Task<IActionResult> SetVisibility(string pageId, string sectionId,
            string blockId, [FromBody] VisibilityDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.SetVisibilityAsync(pageId, sectionId, blockId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok($"Block {(dto.Visible ? "shown" : "hidden")}."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder(string pageId, string sectionId,
            [FromBody] ReorderDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var ok = await _service.ReorderAsync(pageId, sectionId, dto.OrderedIds);
            if (!ok) return BadRequest(ApiResult.BadRequest("Reorder failed."));
            return Ok(ApiResult.Ok("Blocks reordered."));
        }

       
   
        // â”€â”€ Mapping â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool CanUsePageBuilder => AdminAuthorization.CanUsePageBuilder(User);

        private static BlockResponseDto MapToDto(string pageId, string sectionId, Block b)
        {
            var dto = new BlockResponseDto
            {
                Id = b.Id,
                PageId = pageId,
                SectionId = sectionId,
                Type = b switch
                {
                    TextBlock => "text",
                    ImageBlock => "image",
                    VideoBlock => "video",
                    FileBlock => "file",
                    MapBlock => "map",
                    FormBlock => "form",
                    CardBlock => "card",
                    ButtonBlock => "button",
                    MetricBlock => "metric",
                    BulletListBlock => "bullet-list",
                    StepBlock => "step",
                    IconBlock => "icon",
                    ContainerBlock => "container",
                    _ => "unknown"
                },
                Visible = b.Visible,
                Order = b.Order,
                Layout = MapLayoutToDto(b.Layout),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
                Buttons = b.Buttons.Select(btn => new BlockButtonResponseDto
                {
                    Id = btn.Id,
                    Label = btn.Label,
                    Action = btn.Action,
                    Href = btn.Href,
                    FormDefinitionId = btn.FormDefinitionId,
                    Visible = btn.Visible,
                    Order = btn.Order
                }).ToList(),
                ColumnSlotId = b.ColumnSlotId,
                BlockZone = b.BlockZone,
                ZoneId = b.BlockZone,
                PositionMode = ResolvePositionMode(b),
                ParentBlockId = b.ParentBlockId,

            };

            switch (b)
            {
                case TextBlock t:
                    dto.Title = t.Title;
                    dto.Content = t.Content;
                    break;

                case ImageBlock img:
                    dto.ImageUrl = img.ImageUrl;
                    dto.AltText = img.AltText;
                    break;

                case VideoBlock v:
                    dto.EmbedUrl = v.EmbedUrl;
                    dto.Title = v.Title;
                    break;

                case FileBlock f:
                    dto.Filename = f.Filename;
                    dto.FileType = f.FileType;
                    dto.FileUrl = f.FileUrl;
                    break;

                // Map: pins returned as embedded array â€” no separate pin endpoints
                case MapBlock m:
                    dto.CenterLat = m.CenterLat;
                    dto.CenterLng = m.CenterLng;
                    dto.DefaultZoom = m.DefaultZoom;
                    dto.Pins = m.Pins.Select(p => new MapPinDto
                    {
                        Id = p.Id,
                        Label = p.Label,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Href = p.Href
                    }).ToList();
                    break;

                // Form: fields returned as embedded array â€” no separate form endpoints
                case FormBlock form:
                    dto.FormDefinitionId = form.FormDefinitionId;
                    dto.Fields = form.Fields.Select(f => new FormFieldDto
                    {
                        Name = f.Name,
                        Type = f.Type,
                        Label = f.Label,
                        Required = f.Required,
                        Options = f.Options,
                        Order = f.Order
                    }).ToList();
                    dto.SubmitButtonLabel = form.SubmitButtonLabel;
                    break;

                case CardBlock card:
                    dto.Icon = card.Icon;
                    dto.Title = card.Title;
                    dto.Description = card.Description;
                    dto.ImageUrl = card.ImageUrl;
                    dto.ButtonLabel = card.ButtonLabel;
                    dto.Href = card.Href;
                    dto.Action = card.Action;
                    dto.FormDefinitionId = card.FormDefinitionId;
                    break;

                case ButtonBlock button:
                    dto.Label = button.Label;
                    dto.Href = button.Href;
                    dto.Action = button.Action;
                    dto.FormDefinitionId = button.FormDefinitionId;
                    dto.Style = button.Style;
                    break;

                case MetricBlock metric:
                    dto.Icon = metric.Icon;
                    dto.Label = metric.Label;
                    dto.Value = metric.Value;
                    dto.Prefix = metric.Prefix;
                    dto.Suffix = metric.Suffix;
                    dto.Description = metric.Description;
                    break;

                case BulletListBlock list:
                    dto.Title = list.Title;
                    dto.BulletItems = list.Items.Select(i => new BulletListItemDto
                    {
                        Id = i.Id,
                        Icon = i.Icon,
                        Text = i.Text,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;

                case StepBlock step:
                    dto.Icon = step.Icon;
                    dto.StepLabel = step.StepLabel;
                    dto.Title = step.Title;
                    dto.Description = step.Description;
                    break;

                case IconBlock icon:
                    dto.Icon = icon.Icon;
                    dto.Label = icon.Label;
                    dto.Description = icon.Description;
                    break;
                case ContainerBlock container:
                    dto.Title = container.Title;
                    dto.LayoutMode = container.LayoutMode;
                    dto.Columns = container.Columns;
                    dto.Gap = container.Gap;
                    dto.OrbitRadius = container.OrbitRadius;
                    dto.OrbitStartAngle = container.OrbitStartAngle;
                    dto.SemicircleRadius = container.SemicircleRadius;
                    dto.SemicircleStartAngle = container.SemicircleStartAngle;
                    dto.SemicircleEndAngle = container.SemicircleEndAngle;
                    break;
            }

            return dto;
        }

        private static BlockLayoutResponseDto MapLayoutToDto(BlockLayout? layout)
        {
            layout ??= new BlockLayout();

            return new BlockLayoutResponseDto
            {
                Width = layout.Width,
                ColumnSpan = layout.ColumnSpan,
                Align = layout.Align,
                Justify = layout.Justify,
                Padding = layout.Padding,
                Margin = layout.Margin,
                BackgroundColor = layout.BackgroundColor,
                BorderRadius = layout.BorderRadius,
                ZIndex = layout.ZIndex,
                ZOrder = layout.ZIndex,
                X = layout.X,
                Y = layout.Y,
                W = layout.W,
                H = layout.H,
                LeftPercent = layout.LeftPercent,
                TopPx = layout.TopPx,
                WidthPercent = layout.WidthPercent,
                HeightPx = layout.HeightPx
            };
        }

        private static string ResolvePositionMode(Block block)
        {
            if (!string.IsNullOrWhiteSpace(block.PositionMode))
                return string.Equals(block.PositionMode, "freeform", StringComparison.OrdinalIgnoreCase)
                    ? "freeform"
                    : "flow";

            return !string.IsNullOrWhiteSpace(block.ColumnSlotId) ? "flow" :
                string.Equals(block.BlockZone, "canvas", StringComparison.OrdinalIgnoreCase) ? "freeform" : "flow";
        }
    }
}
