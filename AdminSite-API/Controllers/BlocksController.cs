using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services.BlockServices;
using FullProject.Services;
using FullProject.Security;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Admin;
using Contracts.Auth;
using Contracts.Forms;
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
        private readonly BlockAssetMetadataService _assetMetadata;
        private readonly BlockAuthoringService _authoring;

        public BlocksController(
            BlockService service,
            SectionService sectionService,
            BlockAssetMetadataService assetMetadata,
            BlockAuthoringService authoring)
        {
            _service = service;
            _sectionService = sectionService;
            _assetMetadata = assetMetadata;
            _authoring = authoring;
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
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPost]
        public async Task<IActionResult> Create(string pageId, string sectionId,
            [FromBody] BlockCreateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            BlockStarterPresetCatalog.Apply(dto);
            var assetError = await _assetMetadata.CanonicalizeAsync(dto);
            if (assetError is not null)
                return BadRequest(ApiResult.BadRequest(assetError));
            var referenceError = await _service.ValidateFunctionalReferencesAsync(dto, false);
            if (referenceError is not null)
                return BadRequest(ApiResult.BadRequest(referenceError));
            var validationErrors = BlockContractService.Validate(dto);
            if (validationErrors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", validationErrors)));
            var parentError = await _service.ValidateParentAsync(pageId, sectionId, null, dto.ParentBlockId);
            if (parentError is not null)
                return BadRequest(ApiResult.BadRequest(parentError));
            if (dto is ContainerBlockCreateDto containerDto)
            {
                var diagramError = await _service.ValidateDiagramAnchorsAsync(
                    pageId, sectionId, string.Empty, containerDto.ContainerLayout?.Diagram);
                if (diagramError is not null)
                    return BadRequest(ApiResult.BadRequest(diagramError));
            }
            var section = await _sectionService.GetByIdAsync(pageId, sectionId);
            if (section is null) return NotFound(ApiResult.NotFound("Section not found."));

            Block created;
            try
            {
                created = await _service.CreateAsync(pageId, sectionId, dto);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(ApiResult.BadRequest(exception.Message));
            }
            return CreatedAtAction(nameof(GetById),
                new { pageId, sectionId, blockId = created.Id },
                ApiResult.Created(MapToDto(pageId, sectionId, created), "Block created.")
                    .WithNotification("NotificationBlockCreated"));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPut("{blockId}")]
        public async Task<IActionResult> Update(string pageId, string sectionId, string blockId,
            [FromBody] BlockUpdateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var lockError = await _authoring.PrepareContentUpdateAsync(pageId, sectionId, blockId, dto);
            if (lockError is not null)
                return BadRequest(ApiResult.BadRequest(lockError));
            var assetError = await _assetMetadata.CanonicalizeAsync(dto);
            if (assetError is not null)
                return BadRequest(ApiResult.BadRequest(assetError));
            var referenceError = await _service.ValidateFunctionalReferencesAsync(dto, true);
            if (referenceError is not null)
                return BadRequest(ApiResult.BadRequest(referenceError));
            var validationErrors = BlockContractService.Validate(dto);
            if (validationErrors.Count > 0)
                return BadRequest(ApiResult.BadRequest(string.Join(" ", validationErrors)));
            var parentError = await _service.ValidateParentAsync(pageId, sectionId, blockId, dto.ParentBlockId);
            if (parentError is not null)
                return BadRequest(ApiResult.BadRequest(parentError));
            if (dto is ContainerBlockUpdateDto containerDto)
            {
                var policyError = await _service.PrepareContainerPolicyUpdateAsync(
                    pageId, sectionId, blockId, containerDto.ContainerLayout, containerDto.PresetKey);
                if (policyError is not null)
                    return BadRequest(ApiResult.BadRequest(policyError));
                var diagramError = await _service.ValidateDiagramAnchorsAsync(
                    pageId, sectionId, blockId, containerDto.ContainerLayout?.Diagram);
                if (diagramError is not null)
                    return BadRequest(ApiResult.BadRequest(diagramError));
            }
            Block? updated;
            try
            {
                updated = await _service.UpdateAsync(pageId, sectionId, blockId, dto);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(ApiResult.BadRequest(exception.Message));
            }
            if (updated is null) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, updated), "Block updated.")
                .WithNotification("NotificationBlockUpdated"));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId/layout
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPut("{blockId}/layout")]
        public async Task<IActionResult> UpdateLayout(string pageId, string sectionId, string blockId,
            [FromBody] BlockLayoutDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var lockError = await _authoring.ValidateGeometryMutationAsync(pageId, sectionId, [blockId]);
            if (lockError is not null)
                return BadRequest(ApiResult.BadRequest(lockError));
            var updated = await _service.UpdateLayoutAsync(pageId, sectionId, blockId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, updated), "Block layout updated.")
                .WithNotification("NotificationLayoutSaved"));
        }

        // DELETE api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpDelete("{blockId}")]
        public async Task<IActionResult> Delete(string pageId, string sectionId, string blockId)
        {
            if (!CanUsePageBuilder) return Forbid();
            var error = await _authoring.DeleteGraphsAsync(pageId, sectionId, [blockId]);
            if (error is not null)
                return BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok("Block graph deleted.")
                .WithNotification("NotificationBlockDeleted"));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/:blockId/visibility
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPut("{blockId}/visibility")]
        public async Task<IActionResult> SetVisibility(string pageId, string sectionId,
            string blockId, [FromBody] VisibilityDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var lockError = await _authoring.ValidateContentMutationAsync(pageId, sectionId, blockId);
            if (lockError is not null)
                return BadRequest(ApiResult.BadRequest(lockError));
            var ok = await _service.SetVisibilityAsync(pageId, sectionId, blockId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Block not found."));
            return Ok(ApiResult.Ok($"Block {(dto.Visible ? "shown" : "hidden")}.")
                .WithNotification("NotificationBlockUpdated"));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/blocks/reorder
        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder(string pageId, string sectionId,
            [FromBody] ReorderDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var lockError = await _authoring.ValidateGeometryMutationAsync(pageId, sectionId, dto.OrderedIds);
            if (lockError is not null)
                return BadRequest(ApiResult.BadRequest(lockError));
            var ok = await _service.ReorderAsync(pageId, sectionId, dto.OrderedIds);
            if (!ok) return BadRequest(ApiResult.BadRequest("Reorder failed.")
                .WithNotification("NotificationReorderFailed"));
            return Ok(ApiResult.Ok("Blocks reordered.")
                .WithNotification("NotificationOrderSaved"));
        }

        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPut("authoring/layouts")]
        public async Task<IActionResult> UpdateLayouts(
            string pageId,
            string sectionId,
            [FromBody] BlockBulkLayoutUpdateDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var (blocks, error) = await _authoring.UpdateLayoutsAsync(pageId, sectionId, dto);
            if (error is not null) return BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok(new BlockAuthoringOperationResponseDto
            {
                BlockIds = blocks!.Select(block => block.Id).ToList()
            }, "Block layouts updated.")
                .WithNotification("NotificationLayoutSaved"));
        }

        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPost("authoring/duplicate")]
        public async Task<IActionResult> Duplicate(
            string pageId,
            string sectionId,
            [FromBody] BlockDuplicateRequestDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var (blocks, error) = await _authoring.DuplicateAsync(pageId, sectionId, dto.BlockIds);
            if (error is not null) return BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok(new BlockAuthoringOperationResponseDto
            {
                BlockIds = blocks!.Select(block => block.Id).ToList()
            }, "Blocks duplicated."));
        }

        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPost("authoring/delete-graphs")]
        public async Task<IActionResult> DeleteGraphs(
            string pageId,
            string sectionId,
            [FromBody] BlockDuplicateRequestDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var error = await _authoring.DeleteGraphsAsync(pageId, sectionId, dto.BlockIds);
            if (error is not null) return BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok("Block graphs deleted."));
        }

        [Authorize(Policy = AdminPermissionKeys.PageBuilder)]
        [HttpPost("authoring/move")]
        public async Task<IActionResult> Move(
            string pageId,
            string sectionId,
            [FromBody] BlockMoveRequestDto dto)
        {
            if (!CanUsePageBuilder) return Forbid();
            var (block, error) = await _authoring.MoveAsync(pageId, sectionId, dto);
            if (error is not null) return BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, block!), "Block moved.")
                .WithNotification("NotificationOrderSaved"));
        }

        [HttpPut("{blockId}/authoring-lock")]
        public async Task<IActionResult> UpdateAuthoringLock(
            string pageId,
            string sectionId,
            string blockId,
            [FromBody] BlockAuthoringLockUpdateDto dto)
        {
            if (!AdminAuthorization.IsAdminAdmin(User)) return Forbid();
            var (block, error) = await _authoring.UpdateLockAsync(
                pageId,
                sectionId,
                blockId,
                dto,
                canUnlock: true);
            if (error is not null)
                return error.Contains("Only AdminAdmin", StringComparison.Ordinal)
                    ? Forbid()
                    : BadRequest(ApiResult.BadRequest(error));
            return Ok(ApiResult.Ok(MapToDto(pageId, sectionId, block!), "Block lock updated."));
        }

       
   
        // â”€â”€ Mapping â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool CanUsePageBuilder => AdminAuthorization.CanUsePageBuilder(User);

        private static BlockResponseDto MapToDto(string pageId, string sectionId, Block b)
        {
            var dto = new BlockResponseDto
            {
                Id = b.Id,
                StableId = b.StableId,
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
                EditorLabel = b.EditorLabel ?? new Dictionary<string, string>(),
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
                Appearance = BlockContractService.ToAdminAppearance(b),
                Responsive = BlockContractService.ToAdminResponsive(b.Responsive),
                Animation = BlockContractService.ToAdminAnimation(b.Animation),
                Authoring = new BlockAuthoringPolicyDto
                {
                    SchemaVersion = Math.Max(b.Authoring?.SchemaVersion ?? 0, 1),
                    ContentLocked = b.Authoring?.ContentLocked ?? false,
                    GeometryLocked = b.Authoring?.GeometryLocked ?? false,
                    FullLocked = b.Authoring?.FullLocked ?? false,
                    PresetSlotName = b.Authoring?.PresetSlotName,
                    PresetSourceId = b.Authoring?.PresetSourceId
                },

            };

            switch (b)
            {
                case TextBlock t:
                    dto.Title = t.Title;
                    dto.Content = t.Content;
                    break;

                case ImageBlock img:
                    dto.ImageUrl = img.ImageUrl;
                    dto.Asset = BlockAssetMetadataService.ToAdmin(img.Asset);
                    dto.AltText = img.AltText;
                    dto.Caption = img.Caption;
                    dto.OpenInLightbox = img.OpenInLightbox;
                    dto.FocalPointX = img.FocalPointX;
                    dto.FocalPointY = img.FocalPointY;
                    break;

                case VideoBlock v:
                    dto.EmbedUrl = v.EmbedUrl;
                    dto.Asset = BlockAssetMetadataService.ToAdmin(v.Asset);
                    dto.SourceType = v.SourceType;
                    dto.Title = v.Title;
                    dto.ShowControls = v.ShowControls;
                    dto.Autoplay = v.Autoplay;
                    dto.Muted = v.Muted;
                    dto.Loop = v.Loop;
                    break;

                case FileBlock f:
                    dto.Asset = BlockAssetMetadataService.ToAdmin(f.Asset);
                    dto.Filename = f.Filename;
                    dto.FileType = f.FileType;
                    dto.FileUrl = f.FileUrl;
                    dto.DisplayName = f.DisplayName;
                    dto.OpenBehavior = f.OpenBehavior;
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
                        Href = p.Href,
                        Visible = p.Visible,
                        Order = p.Order
                    }).ToList();
                    break;

                // Form: fields returned as embedded array â€” no separate form endpoints
                case FormBlock form:
                    dto.FormDefinitionId = form.FormDefinitionId;
                    dto.FormScale = Math.Clamp(form.FormScale, FormBlockLayoutPolicy.MinimumScale, FormBlockLayoutPolicy.MaximumScale);
                    dto.FormDefaultWidthPx = form.DefaultWidthPx > 0
                        ? form.DefaultWidthPx
                        : null;
                    dto.FormDefaultWidthPercent = form.DefaultWidthPercent > 0
                        ? form.DefaultWidthPercent
                        : null;
                    dto.FormDefaultHeightPx = form.DefaultHeightPx > 0
                        ? form.DefaultHeightPx
                        : null;
                    break;

                case CardBlock card:
                    dto.Icon = card.Icon;
                    dto.Title = card.Title;
                    dto.Description = card.Description;
                    dto.ImageUrl = card.ImageUrl;
                    dto.Asset = BlockAssetMetadataService.ToAdmin(card.Asset);
                    dto.AltText = card.ImageAltText;
                    dto.ButtonLabel = card.ButtonLabel;
                    dto.Href = card.Href;
                    dto.Action = card.Action;
                    dto.FormDefinitionId = card.FormDefinitionId;
                    dto.Style = card.ButtonStyle;
                    break;

                case ButtonBlock button:
                    dto.Icon = button.Icon;
                    dto.IconPosition = button.IconPosition;
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
                    dto.AutoNumber = step.AutoNumber;
                    dto.StepLabel = step.StepLabel;
                    dto.Title = step.Title;
                    dto.Description = step.Description;
                    break;

                case IconBlock icon:
                    dto.Icon = icon.Icon;
                    dto.Label = icon.Label;
                    dto.Description = icon.Description;
                    dto.ActionEnabled = icon.ActionEnabled;
                    dto.Href = icon.Href;
                    dto.Action = icon.Action;
                    dto.FormDefinitionId = icon.FormDefinitionId;
                    break;
                case ContainerBlock container:
                    dto.PresetKey = ContainerPresetCatalog.EffectiveKey(container.PresetKey);
                    dto.Title = container.Title;
                    dto.ContainerLayout = BlockContractService.ToAdminContainerLayout(container.ContainerLayout);
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
