using FullProject.Models;
using FullProject.Services;
using FullProject.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Admin;
using GlobalManager.Services.SectionServices;


namespace FullProject.Controllers
{
    [ApiController]
    [Route("api/admin/pages/{pageId}/sections")]
    [Authorize]
    public class SectionsController : ControllerBase
    {
        private readonly SectionService _service;
        private readonly PageService _pageService;

        public SectionsController(SectionService service, PageService pageService)
        {
            _service = service;
            _pageService = pageService;
        }

        // GET api/admin/pages/:pageId/sections
        [HttpGet]
        public async Task<IActionResult> GetAll(string pageId)
        {
            var page = await _pageService.GetByIdAsync(pageId);
            if (page is null) return NotFound(ApiResult.NotFound("Page not found."));

            var sections = await _service.GetByPageAsync(pageId);
            return Ok(ApiResult.Ok(sections.Select(s => MapToDto(pageId, s)).ToList()));
        }

        // GET api/admin/pages/:pageId/sections/:sectionId
        [HttpGet("{sectionId}")]
        public async Task<IActionResult> GetById(string pageId, string sectionId)
        {
            var section = await _service.GetByIdAsync(pageId, sectionId);
            if (section is null) return NotFound(ApiResult.NotFound("Section not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, section)));
        }

        // POST api/admin/pages/:pageId/sections
        [HttpPost]
        public async Task<IActionResult> Create(string pageId, [FromBody] SectionCreateDto dto)
        {
            var page = await _pageService.GetByIdAsync(pageId);
            if (page is null) return NotFound(ApiResult.NotFound("Page not found."));

            var created = await _service.CreateAsync(pageId, dto);
            return CreatedAtAction(nameof(GetById),
                new { pageId, sectionId = created.Id },
                ApiResult.Created(MapToDto(pageId, created), "Section created."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId
        [HttpPut("{sectionId}")]
        public async Task<IActionResult> Update(string pageId, string sectionId,
            [FromBody] SectionUpdateDto dto)
        {
            var updated = await _service.UpdateAsync(pageId, sectionId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Section not found."));


            return Ok(ApiResult.Ok(MapToDto(pageId, updated), "Section updated."));
        }

        // DELETE api/admin/pages/:pageId/sections/:sectionId
        [HttpDelete("{sectionId}")]
        public async Task<IActionResult> Delete(string pageId, string sectionId)
        {
            var ok = await _service.DeleteAsync(pageId, sectionId);
            if (!ok) return NotFound(ApiResult.NotFound("Section not found."));
            return Ok(ApiResult.Ok("Section deleted."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/visibility
        [HttpPut("{sectionId}/visibility")]
        public async Task<IActionResult> SetVisibility(string pageId, string sectionId,
            [FromBody] VisibilityDto dto)
        {
            var ok = await _service.SetVisibilityAsync(pageId, sectionId, dto.Visible);
            if (!ok) return NotFound(ApiResult.NotFound("Section not found."));
            return Ok(ApiResult.Ok($"Section {(dto.Visible ? "shown" : "hidden")}."));
        }

        // PUT api/admin/pages/:pageId/sections/:sectionId/style
        [HttpPut("{sectionId}/style")]
        public async Task<IActionResult> UpdateStyle(string pageId, string sectionId,
        [FromBody] SectionStyleDto dto)
        {
            var updated = await _service.UpdateStyleAsync(pageId, sectionId, dto);
            if (updated is null) return NotFound(ApiResult.NotFound("Section not found."));
            return Ok(ApiResult.Ok(MapToDto(pageId, updated), "Style updated."));
        }

        // PUT api/admin/pages/:pageId/sections/reorder
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder(string pageId, [FromBody] ReorderDto dto)
        {
            var ok = await _service.ReorderAsync(pageId, dto.OrderedIds);
            if (!ok) return BadRequest(ApiResult.BadRequest("Reorder failed."));
            return Ok(ApiResult.Ok("Sections reordered."));
        }

        // -- Mapping -------------------------------------------

        private static SectionResponseDto MapToDto(string pageId, Section s)
        {
            var dto = new SectionResponseDto
            {
                Id = s.Id,
                PageId = pageId,
                Type = s switch
                {
                    HeroSection => "hero",
                    CtaSection => "cta",
                    GallerySection => "gallery",
                    ListSection => "list",
                    DynamicSection => "dynamic",
                    HtmlSection => "html",
                    ColumnsSection => "columns",
                    ShowcaseSection => "showcase",
                    LibrarySection => "library",
                    StatsSection => "stats",
                    CarouselSection => "carousel",
                    NetworkMapSection => "network-map",
                    TestimonialSection => "testimonial",
                    CanvasSection => "canvas",
                    _ => "unknown"
                },
                Visible = s.Visible,
                Order = s.Order,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Style = new SectionStyleResponseDto
                {
                    BackgroundType = s.Style.BackgroundType,
                    BackgroundColor = s.Style.BackgroundColor,
                    BackgroundImageUrl = s.Style.BackgroundImageUrl,
                    BackgroundVideoUrl = s.Style.BackgroundVideoUrl,
                    GradientFrom = s.Style.GradientFrom,
                    GradientTo = s.Style.GradientTo,
                    GradientDirection = s.Style.GradientDirection,
                    OverlayColor = s.Style.OverlayColor,
                    OverlayOpacity = s.Style.OverlayOpacity,
                    Height = s.Style.Height,
                    CustomMinHeightPx = s.Style.CustomMinHeightPx,
                    Padding = s.Style.Padding,
                    ContentWidth = s.Style.ContentWidth,
                    TextColor = s.Style.TextColor,
                    MobileLayout = s.Style.MobileLayout,
                    BlockLayoutMode = s.Style.BlockLayoutMode,
                    BlockGridColumns = s.Style.BlockGridColumns,
                    BlockGap = s.Style.BlockGap
                }
            };

            switch (s)
            {
                case HeroSection h:
                    dto.Layout = h.Layout;
                    dto.Eyebrow = h.Eyebrow;
                    dto.Heading = h.Heading;
                    dto.Subheading = h.Subheading;
                    dto.HeadingSize = h.HeadingSize;
                    dto.ContentAlignment = h.ContentAlignment;
                    dto.ImageUrl = h.ImageUrl;
                    dto.Buttons = h.Buttons.Select(MapButtonToDto).ToList();
                    break;

                case CtaSection c:
                    dto.Layout = c.Layout;
                    dto.Heading = c.Heading;
                    dto.Subtext = c.Subtext;
                    dto.Button = c.Button != null ? MapButtonToDto(c.Button) : null;
                    dto.Buttons = c.Buttons.Select(MapButtonToDto).ToList();
                    break;

                case GallerySection g:
                    dto.Layout = g.Layout;
                    dto.Columns = g.Columns;
                    dto.Gap = g.Gap;
                    dto.ShowCaptions = g.ShowCaptions;
                    dto.Images = g.Images.Select(i => new GalleryImageResponseDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        Caption = i.Caption,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;

                case ListSection l:
                    dto.Layout = l.Layout;
                    dto.Columns = l.Columns;
                    dto.SectionTitle = l.SectionTitle;
                    dto.ShowIcon = l.ShowIcon;
                    dto.Items = l.Items.Select(i => new ListItemResponseDto
                    {
                        Id = i.Id,
                        Icon = i.Icon,
                        Title = i.Title,
                        Description = i.Description,
                        ImageUrl = i.ImageUrl,
                        LinkHref = i.LinkHref,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;

                case DynamicSection d:
                    dto.ScopeSectionIds = d.ScopeSectionIds;
                    dto.SearchBy = d.SearchBy;
                    dto.Display = d.Display;
                    dto.Placeholder = d.Placeholder;
                    dto.DefaultSort = d.DefaultSort;
                    dto.ShowSearchBar = d.ShowSearchBar;
                    break;

                case HtmlSection html:
                    dto.HtmlContent = html.Content;
                    break;

                case ColumnsSection col:
                    dto.ColumnCount = col.ColumnCount;
                    dto.ColumnRatio = col.ColumnRatio;
                    dto.Gap = col.Gap;
                    dto.StackOnMobile = col.StackOnMobile;
                    dto.ColumnSlots = col.Columns
                            .OrderBy(s => s.Order)
                            .Select(s => new ColumnSlotResponseDto { Id = s.Id, Order = s.Order })
                            .ToList();
                    break;

                case ShowcaseSection ld:
                    dto.Layout = ld.Layout;
                    dto.Columns = ld.Columns;
                    dto.Limit = ld.Limit;
                    dto.Eyebrow = ld.Eyebrow;
                    dto.SectionTitle = ld.SectionTitle;
                    dto.ShowImage = ld.ShowImage;
                    dto.ShowContent = ld.ShowContent;
                    dto.ShowItemButton = ld.ShowItemButton;
                    dto.ButtonLabel = ResolveShowcaseButtonLabel(ld);
                    dto.ActionButton = ld.ActionButton != null ? MapButtonToDto(ld.ActionButton) : null;
                    dto.ActionButtonPosition = ld.ActionButtonPosition;
                    dto.ShowSearchBar = ld.ShowSearchBar;
                    dto.SearchPlaceholder = ld.SearchPlaceholder;
                    dto.SourcePageId = ld.SourcePageId;
                    dto.ShowcaseItems = ld.ItemOverrides.Select(MapShowcaseItemOverrideToDto).ToList();
                    break;
                case LibrarySection library:
                    dto.Layout = library.Layout;
                    dto.Columns = library.Columns;
                    dto.ContentTypes = library.ContentTypes;
                    dto.Limit = library.Limit;
                    dto.Rows = library.Rows;
                    dto.EnableTabs = library.EnableTabs;
                    dto.EnablePagination = library.EnablePagination;
                    dto.Eyebrow = library.Eyebrow;
                    dto.SectionTitle = library.SectionTitle;
                    dto.Subheading = library.Subheading;
                    dto.ShowImage = library.ShowImage;
                    dto.ShowSummary = library.ShowSummary;
                    dto.ShowButton = library.ShowButton;
                    dto.ShowTime = library.ShowTime;
                    dto.ButtonLabel = library.ButtonLabel;
                    dto.ButtonStyle = library.ButtonStyle;
                    dto.ShowSearchBar = library.ShowSearchBar;
                    dto.ShowFilters = library.ShowFilters;
                    dto.SearchPlaceholder = library.SearchPlaceholder;
                    dto.SortMode = library.SortMode;
                    break;
                case StatsSection st:
                    dto.SectionTitle = st.SectionTitle;
                    dto.Columns = st.Columns;
                    dto.DurationMs = st.DurationMs;
                    dto.Stats = st.Items.Select(i => new StatItemResponseDto
                    {
                        Id = i.Id,
                        Label = i.Label,
                        Value = i.Value,
                        Prefix = i.Prefix,
                        Suffix = i.Suffix,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;
                case CarouselSection ca:
                    dto.SectionTitle = ca.SectionTitle;
                    dto.Layout = ca.Layout;
                    dto.Columns = ca.Columns;
                    dto.Autoplay = ca.Autoplay;
                    dto.ShowDots = ca.ShowDots;
                    dto.ShowArrows = ca.ShowArrows;
                    dto.CarouselItems = ca.Items.Select(i => new CarouselItemResponseDto
                    {
                        Id = i.Id,
                        Tag = i.Tag,
                        Title = i.Title,
                        Description = i.Description,
                        ImageUrl = i.ImageUrl,
                        LinkHref = i.LinkHref,
                        Metrics = i.Metrics.Select(m => new CarouselMetricDto
                        {
                            Value = m.Value,
                            Label = m.Label,
                            Tone = m.Tone,
                            Order = m.Order
                        }).ToList(),
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;
                case NetworkMapSection map:
                    dto.SectionTitle = map.SectionTitle;
                    dto.CenterLat = map.CenterLat;
                    dto.CenterLng = map.CenterLng;
                    dto.DefaultZoom = map.DefaultZoom;
                    dto.MapPins = map.Pins.Select(p => new NetworkMapPinResponseDto
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
                case TestimonialSection t:
                    dto.Layout = t.Layout;
                    dto.HeaderAlignment = t.HeaderAlignment;
                    dto.Eyebrow = t.Eyebrow;
                    dto.SectionTitle = t.SectionTitle;
                    dto.Subheading = t.Subheading;
                    dto.Columns = t.Columns;
                    dto.Testimonials = t.Items.Select(i => new TestimonialItemResponseDto
                    {
                        Id = i.Id,
                        Icon = i.Icon,
                        Title = i.Title,
                        Description = i.Description,
                        ImageUrl = i.ImageUrl,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList();
                    break;
                case CanvasSection canvas:
                    dto.AdminLabel = canvas.AdminLabel;
                    break;
            }

            return dto;
        }

        private static ShowcaseItemOverrideDto MapShowcaseItemOverrideToDto(ShowcaseItemOverride item) => new()
        {
            ChildPageId = item.ChildPageId,
            CardTitle = item.CardTitle ?? new(),
            CardContent = item.CardContent ?? new(),
            CardBackgroundType = item.CardBackgroundType,
            CardBackgroundColor = item.CardBackgroundColor,
            CardImageUrl = item.CardImageUrl
        };

        private static Dictionary<string, string> ResolveShowcaseButtonLabel(ShowcaseSection section) =>
            section.ButtonLabelText.Values.Any(v => !string.IsNullOrWhiteSpace(v))
                ? section.ButtonLabelText
                : new Dictionary<string, string> { ["en"] = "Learn More" };

        private static SectionButtonResponseDto MapButtonToDto(SectionButton b) => new()
        {
            Id = b.Id,
            Label = b.Label,
            Action = b.Action,
            Href = b.Href,
            FormDefinitionId = b.FormDefinitionId,
            Style = b.Style,
            Visible = b.Visible,
            Order = b.Order
        };
    }
}





