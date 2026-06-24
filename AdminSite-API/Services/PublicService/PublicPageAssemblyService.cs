using Contracts.Public;
using FullProject.Models;
using FullProject.Services.FormServices;
using GlobalManager.Services.SectionServices;
using System.Net;
using System.Text.RegularExpressions;

namespace FullProject.Services.PublicService
{
    public class PublicPageAssemblyService
    {
        private readonly PageService _pageService;
        private readonly SectionService _sectionService;
        private readonly BlockService _blockService;
        private readonly ContentService _contentService;
        private readonly FormDefinitionService _formDefinitionService;

        public PublicPageAssemblyService(
            PageService pageService,
            SectionService sectionService,
            BlockService blockService,
            ContentService contentService,
            FormDefinitionService formDefinitionService)
        {
            _pageService = pageService;
            _sectionService = sectionService;
            _blockService = blockService;
            _contentService = contentService;
            _formDefinitionService = formDefinitionService;
        }

        public async Task<object?> GetPageResponseAsync(string slug)
        {
            var page = await _pageService.GetByFullSlugAsync(slug);
            if (page is null || page.Status != PageStatus.Published)
                return null;

            var sectionDtos = await BuildVisibleSectionDtosAsync(page);
            return new
            {
                page.Id,
                page.Name,
                page.Slug,
                page.Status,
                page.Seo,
                Sections = sectionDtos
            };
        }

        public async Task<object?> GetChildPageResponseAsync(string parentSlug, string childSlug)
        {
            var fullSlug = $"{parentSlug}/{childSlug}";
            var page = await _pageService.GetByFullSlugAsync(fullSlug);
            if (page is null || page.Status != PageStatus.Published)
                return null;

            var sectionDtos = await BuildVisibleSectionDtosAsync(page);
            return new
            {
                page.Id,
                page.Name,
                page.Slug,
                FullSlug = page.FullSlug,
                ParentPageId = page.ParentPageId,
                page.Status,
                page.Seo,
                Sections = sectionDtos
            };
        }

        public async Task<PublicPageDto?> GetContentPageAsync(string typeKey, string slug)
        {
            var item = await _contentService.GetPublishedBySlugAsync(typeKey, slug);
            if (item is null)
                return null;

            var contentType = (await _contentService.GetTypesAsync())
                .FirstOrDefault(t => string.Equals(t.Key, item.ContentTypeKey, StringComparison.OrdinalIgnoreCase));

            return await BuildContentDetailPageAsync(item, contentType);
        }

        private async Task<List<object>> BuildVisibleSectionDtosAsync(Page page)
        {
            var sections = await _sectionService.GetPublicSectionsByPageAsync(page.StableId);
            var visibleSections = sections
                .Where(s => s.Visible)
                .OrderBy(s => s.Order)
                .ToList();

            return await BuildSectionDtosAsync(page, visibleSections);
        }

        public async Task<List<object>> BuildSectionDtosAsync(Page page, List<Section> visibleSections)
        {
            var allBlocks = await _blockService.GetPublicByPageAsync(page.StableId);
            var formDefinitions = (await _formDefinitionService.GetActiveByIdsAsync(
                    allBlocks.OfType<FormBlock>()
                        .Select(block => block.FormDefinitionId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!)))
                .ToDictionary(definition => definition.Id, StringComparer.Ordinal);

            // We filter for visible blocks here to keep the logic clean inside the loops
            var blocksBySection = allBlocks
                .Where(b => b.Visible)
                .GroupBy(b => b.SectionStableId)
                .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());

            var sectionDtos = new List<object>();

            foreach (var section in visibleSections)
            {
                // 1. ShowcaseSection branch
                if (section is ShowcaseSection lds)
                {
                    var children = await _pageService.GetPublicChildrenAsync(lds.SourcePageId);
                    if (lds.Limit > 0)
                    {
                        children = children.Take(Math.Clamp(lds.Limit, 1, 200)).ToList();
                    }

                    var overrides = lds.ItemOverrides
                        .Where(i => !string.IsNullOrWhiteSpace(i.ChildPageId))
                        .GroupBy(i => i.ChildPageId)
                        .ToDictionary(g => g.Key, g => g.Last());

                    sectionDtos.Add(new PublicShowcaseSectionDto
                    {
                        Id = section.Id,
                        Type = "showcase",
                        Visible = section.Visible,
                        Order = section.Order,
                        Style = MapStyle(section.Style),
                        Layout = lds.Layout,
                        Columns = lds.Columns,
                        Limit = lds.Limit,
                        Eyebrow = lds.Eyebrow,
                        SectionTitle = lds.SectionTitle,
                        ShowImage = lds.ShowImage,
                        ShowContent = lds.ShowContent,
                        ShowItemButton = lds.ShowItemButton,
                        ButtonLabel = ResolveShowcaseButtonLabel(lds),
                        ActionButton = lds.ActionButton != null ? MapButton(lds.ActionButton) : null,
                        ActionButtonPosition = lds.ActionButtonPosition,
                        ShowSearchBar = lds.ShowSearchBar,
                        SearchPlaceholder = lds.SearchPlaceholder,
                        Children = children.Select(c => new
                        PublicChildCardDto
                        {
                            Id = c.Id,
                            StableId = c.StableId,
                            SourceId = c.SourceId,
                            FullSlug = c.FullSlug ?? c.Slug,
                            Name = c.Name,
                            Card = MapShowcaseCard(c.Card, FindShowcaseOverride(c, overrides))
                        }).ToList()
                    });
                    continue;
                }

                if (section is LibrarySection library)
                {
                    var itemLimit = library.EnablePagination
                        ? 200
                        : Math.Clamp(library.Limit, 1, 24);
                    var items = await _contentService.GetPublishedLibraryItemsAsync(library.ContentTypes, itemLimit, library.SortMode);
                    var typesByKey = (await _contentService.GetTypesAsync())
                        .ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);

                    sectionDtos.Add(new PublicLibrarySectionDto
                    {
                        Id = section.Id,
                        Type = "library",
                        Visible = section.Visible,
                        Order = section.Order,
                        Style = MapStyle(section.Style),
                        ContentTypes = library.ContentTypes,
                        Layout = library.Layout,
                        Columns = library.Columns,
                        Rows = library.Rows,
                        Limit = library.Limit,
                        EnableTabs = library.EnableTabs,
                        EnablePagination = library.EnablePagination,
                        Eyebrow = library.Eyebrow,
                        SectionTitle = library.SectionTitle,
                        Subheading = library.Subheading,
                        ShowImage = library.ShowImage,
                        ShowSummary = library.ShowSummary,
                        ShowButton = library.ShowButton,
                        ShowTime = library.ShowTime,
                        ButtonLabel = library.ButtonLabel,
                        ButtonStyle = library.ButtonStyle,
                        ShowSearchBar = library.ShowSearchBar,
                        ShowFilters = library.ShowFilters,
                        SearchPlaceholder = library.SearchPlaceholder,
                        SortMode = library.SortMode,
                        Items = items.Select(item =>
                            MapLibraryItem(item, typesByKey.TryGetValue(item.ContentTypeKey, out var type) ? type : null)).ToList()
                    });
                    continue;
                }

                // 2. ColumnsSection branch (The "Fix 5" addition)
                if (section is ColumnsSection cs)
                {
                    var sectionBlocks = blocksBySection.TryGetValue(section.StableId, out var sb)
                        ? sb : new List<Block>();

                    // Group the flat block list by ColumnSlotId for this specific section
                    var blocksBySlot = sectionBlocks
                        .GroupBy(b => b.ColumnSlotId ?? "")
                        .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());

                    sectionDtos.Add(new
                    {
                        section.Id,
                        section.PageId,
                        Type = "columns",
                        section.Visible,
                        section.Order,
                        section.Style,
                        cs.ColumnCount,
                        cs.ColumnRatio,
                        cs.Gap,
                        cs.StackOnMobile,
                        ColumnSlots = cs.Columns
                            .OrderBy(slot => slot.Order)
                            .Select(slot => new
                            {
                                slot.Id,
                                slot.Order,
                                Blocks = blocksBySlot.TryGetValue(slot.Id, out var slotBlocks)
                                    ? MapPublicBlocks(slotBlocks, formDefinitions)
                                    : new List<PublicBlockDto>()
                            }).ToList()
                    });
                    continue;
                }

                // 3. Catch-all for standard sections (Hero, CTA, Gallery, etc.)
                var visibleBlocks = blocksBySection.TryGetValue(section.StableId, out var sBlocks)
                    ? sBlocks : new List<Block>();

                sectionDtos.Add(new
                {
                    section.Id,
                    section.PageId,
                    Type = section switch
                    {
                        HeroSection => "hero",
                        CtaSection => "cta",
                        GallerySection => "gallery",
                        ListSection => "list",
                        DynamicSection => "dynamic",
                        HtmlSection => "html",
                        LibrarySection => "library",
                        StatsSection => "stats",
                        CarouselSection => "carousel",
                        NetworkMapSection => "network-map",
                        TestimonialSection => "testimonial",
                        CanvasSection => "canvas",
                        _ => "unknown"
                    },
                    section.Visible,
                    section.Order,
                    section.Style,
                    Heading = (section as HeroSection)?.Heading ?? (section as CtaSection)?.Heading,
                    Subheading = (section as HeroSection)?.Subheading ?? (section as TestimonialSection)?.Subheading,
                    Subtext = (section as CtaSection)?.Subtext,
                    Layout = (section as HeroSection)?.Layout
                                ?? (section as CtaSection)?.Layout
                                ?? (section as GallerySection)?.Layout
                                ?? (section as ListSection)?.Layout
                                ?? (section as CarouselSection)?.Layout
                                ?? (section as TestimonialSection)?.Layout,
                    HeaderAlignment = (section as TestimonialSection)?.HeaderAlignment,
                    Buttons = section switch
                    {
                        HeroSection hero => hero.Buttons,
                        CtaSection cta => cta.Buttons,
                        _ => null
                    },
                    Button = (section as CtaSection)?.Button,
                    ImageUrl = (section as HeroSection)?.ImageUrl,
                    Images = (section as GallerySection)?.Images?.Where(i => i.Visible).OrderBy(i => i.Order).ToList(),
                    Items = section switch
                    {
                        ListSection list => list.Items
                            .Where(i => i.Visible)
                            .OrderBy(i => i.Order)
                            .Select(i => new { i.Id, i.Icon, i.Title, i.Description, i.ImageUrl, i.LinkHref, i.Visible, i.Order })
                            .Cast<object>()
                            .ToList(),
                        StatsSection stats => stats.Items
                            .Where(i => i.Visible)
                            .OrderBy(i => i.Order)
                            .Select(i => new { i.Id, i.Label, i.Value, i.Prefix, i.Suffix, i.Order })
                            .Cast<object>()
                            .ToList(),
                        CarouselSection carousel => carousel.Items
                            .Where(i => i.Visible)
                            .OrderBy(i => i.Order)
                            .Select(i => new
                            {
                                i.Id,
                                i.Tag,
                                i.Title,
                                i.Description,
                                i.ImageUrl,
                                i.LinkHref,
                                Metrics = i.Metrics
                                    .OrderBy(m => m.Order)
                                    .Select(m => new { m.Id, m.Value, m.Label, m.Tone, m.Order })
                                    .ToList(),
                                i.Order
                            })
                            .Cast<object>()
                            .ToList(),
                        TestimonialSection testimonial => testimonial.Items
                            .Where(i => i.Visible)
                            .OrderBy(i => i.Order)
                            .Select(i => new { i.Id, i.Icon, i.Title, i.Description, i.ImageUrl, i.Order })
                            .Cast<object>()
                            .ToList(),
                        _ => null
                    },
                    Eyebrow = (section as HeroSection)?.Eyebrow ?? (section as TestimonialSection)?.Eyebrow,
                    SectionTitle = (section as ListSection)?.SectionTitle
                        ?? (section as StatsSection)?.SectionTitle
                        ?? (section as CarouselSection)?.SectionTitle
                        ?? (section as NetworkMapSection)?.SectionTitle
                        ?? (section as TestimonialSection)?.SectionTitle,
                    ShowIcon = (section as ListSection)?.ShowIcon,
                    Columns = (section as ListSection)?.Columns
                        ?? (section as StatsSection)?.Columns
                        ?? (section as CarouselSection)?.Columns
                        ?? (section as TestimonialSection)?.Columns,
                    ScopeSectionIds = (section as DynamicSection)?.ScopeSectionIds,
                    SearchBy = (section as DynamicSection)?.SearchBy,
                    Display = (section as DynamicSection)?.Display,
                    Placeholder = (section as DynamicSection)?.Placeholder,
                    DefaultSort = (section as DynamicSection)?.DefaultSort,
                    ShowSearchBar = (section as DynamicSection)?.ShowSearchBar,
                    HtmlContent = (section as HtmlSection)?.Content,
                    DurationMs = (section as StatsSection)?.DurationMs,
                    Autoplay = (section as CarouselSection)?.Autoplay,
                    ShowDots = (section as CarouselSection)?.ShowDots,
                    ShowArrows = (section as CarouselSection)?.ShowArrows,
                    Pins = (section as NetworkMapSection)?.Pins?
                                .Where(i => i.Visible)
                                .OrderBy(i => i.Order)
                                .Select(i => new { i.Id, i.Label, i.Lat, i.Lng, i.Href, i.Order })
                                .ToList(),
                    CenterLat = (section as NetworkMapSection)?.CenterLat,
                    CenterLng = (section as NetworkMapSection)?.CenterLng,
                    DefaultZoom = (section as NetworkMapSection)?.DefaultZoom,
                    AdminLabel = (section as CanvasSection)?.AdminLabel,
                    Blocks = MapPublicBlocks(visibleBlocks
                        .Where(b => string.IsNullOrWhiteSpace(b.ColumnSlotId)), formDefinitions)
                });
            }

            return sectionDtos;
        }

        private static List<PublicBlockDto> MapPublicBlocks(
            IEnumerable<Block> blocks,
            IReadOnlyDictionary<string, FormDefinition> formDefinitions,
            string? parentBlockId = null)
        {
            var blockList = blocks.ToList();
            var mapped = blockList
                .Where(b => string.IsNullOrWhiteSpace(parentBlockId)
                    ? string.IsNullOrWhiteSpace(b.ParentBlockId)
                    : string.Equals(b.ParentBlockId, parentBlockId, StringComparison.Ordinal))
                .OrderBy(b => b.Order)
                .Select(block => MapPublicBlock(block, formDefinitions))
                .Where(b => b is not null)
                .Select(b => b!)
                .ToList();

            foreach (var container in mapped.OfType<PublicContainerBlockDto>())
            {
                container.Children = MapPublicBlocks(blockList, formDefinitions, container.Id);
            }

            return mapped;
        }

        private static PublicLibraryItemDto MapLibraryItem(ContentItem item, ContentType? type) => new()
        {
            Id = item.Id,
            StableId = item.StableId,
            ContentTypeKey = item.ContentTypeKey,
            ContentBehavior = ResolveLibraryContentBehavior(type),
            Slug = item.Slug,
            Title = item.Title,
            Summary = item.Summary,
            HeroImageUrl = item.HeroImageUrl,
            ThumbnailUrl = item.ThumbnailUrl,
            VideoUrl = !string.IsNullOrWhiteSpace(item.VideoUrl)
                ? item.VideoUrl
                : item.BodyItems.FirstOrDefault(i => i.Type == "video" && !string.IsNullOrWhiteSpace(i.Url))?.Url,
            ExternalUrl = item.ExternalUrl,
            ClickBehavior = ResolveLibraryClickBehavior(item, type),
            Tags = item.Tags,
            Attachments = item.Attachments.Select(a => new PublicLibraryAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                Url = a.Url,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes
            }).ToList(),
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            PublishedAt = item.PublishedAt
        };

        public async Task<PublicPageDto> BuildContentDetailPageAsync(ContentItem item, ContentType? type = null)
        {
            var typeRoute = ContentTypeRoute(item.ContentTypeKey);
            var expertForm = await _formDefinitionService.GetActiveByKeyAsync("expert");
            var fullSlug = $"insights/{typeRoute}/{item.Slug}";
            return new PublicPageDto
            {
                Id = item.StableId,
                Slug = item.Slug,
                FullSlug = fullSlug,
                Name = item.Title,
                Sections = new List<PublicSectionDto>
                {
                    new PublicHtmlSectionDto
                    {
                        Id = $"{item.StableId}-hero",
                        Type = "html",
                        Visible = true,
                        Order = 0,
                        Style = ContentDetailHeroStyle(),
                        HtmlContent = BuildContentDetailHeroHtml(item, typeRoute)
                    },
                    new PublicHtmlSectionDto
                    {
                        Id = $"{item.StableId}-body",
                        Type = "html",
                        Visible = true,
                        Order = 1,
                        Style = ContentBodyStyle(),
                        HtmlContent = BuildContentDetailBodyHtml(item, typeRoute, type)
                    },
                    new PublicCtaSectionDto
                    {
                        Id = $"{item.StableId}-cta",
                        Type = "cta",
                        Visible = true,
                        Order = 2,
                        Style = ContentCtaStyle(),
                        Layout = "final-card",
                        Heading = new Dictionary<string, string>
                        {
                            ["en"] = "Ready to move in sync?",
                            ["vi"] = "Ready to move in sync?",
                            ["cn"] = "Ready to move in sync?"
                        },
                        Subtext = new Dictionary<string, string>
                        {
                            ["en"] = "Talk to U&I Logistics about building a more connected supply chain.",
                            ["vi"] = "Talk to U&I Logistics about building a more connected supply chain.",
                            ["cn"] = "Talk to U&I Logistics about building a more connected supply chain."
                        },
                        Buttons = new()
                        {
                            new PublicSectionButtonDto
                            {
                                Id = $"{item.StableId}-cta-button",
                                Label = new Dictionary<string, string>
                                {
                                    ["en"] = "Talk to Expert",
                                    ["vi"] = "Talk to Expert",
                                    ["cn"] = "Talk to Expert"
                                },
                                Action = expertForm is null ? "linkToPage" : "openForm",
                                Href = expertForm is null ? "/contact" : null,
                                FormDefinitionId = expertForm?.Id,
                                Style = "filled",
                                Visible = true,
                                Order = 0
                            }
                        }
                    }
                }
            };
        }

        private static PublicSectionStyleDto ContentDetailHeroStyle() => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#f4f7fb",
            Height = "auto",
            Padding = "none",
            ContentWidth = "wide",
            TextColor = "dark",
            MobileLayout = "stack",
            BlockLayoutMode = "stack",
            BlockGridColumns = 12,
            BlockGap = "medium"
        };

        private static Dictionary<string, string> BuildContentDetailHeroHtml(ContentItem item, string typeRoute)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in new[] { "en", "vi", "cn" })
                result[lang] = BuildContentDetailHeroHtml(item, typeRoute, lang);

            return result;
        }

        private static string BuildContentDetailHeroHtml(ContentItem item, string typeRoute, string lang)
        {
            var title = H(LangValue(item.Title, lang, item.Slug));
            var summary = H(LangValue(item.Summary, lang, string.Empty));
            var typeLabel = H(ContentTypeLabel(item.ContentTypeKey));
            var typeQuery = U(typeRoute);
            var imageUrl = item.HeroImageUrl ?? item.ThumbnailUrl;
            var author = H(DisplayAuthor(item.AuthorId));
            var published = item.PublishedAt ?? item.UpdatedAt;
            var date = H(published.ToLocalTime().ToString("dd MMM yyyy"));
            var readTime = EstimateReadTime(item, lang);
            var language = H(lang.ToUpperInvariant());
            var alt = H(!string.IsNullOrWhiteSpace(item.HeroImageAlt) ? item.HeroImageAlt! : LangValue(item.Title, lang, item.Slug));
            var imageHtml = string.IsNullOrWhiteSpace(imageUrl)
                ? string.Empty
                : $"<img class=\"sc-insight-hero-image\" src=\"{H(imageUrl)}\" alt=\"{alt}\">";

            return $"""
                <div class="sc-insight-detail-hero">
                    <nav class="sc-insight-breadcrumb" aria-label="Breadcrumb">
                        <a href="/home">Home</a>
                        <span>/</span>
                        <a href="/insights">Insights</a>
                        <span>/</span>
                        <a href="/insights?type={typeQuery}">{typeLabel}</a>
                        <span>/</span>
                        <span>{title}</span>
                    </nav>
                    <div class="sc-insight-badge">{typeLabel}</div>
                    <h1>{title}</h1>
                    <p>{summary}</p>
                    <div class="sc-insight-meta">
                        <span class="sc-insight-author"><span>{Initials(author)}</span>{author}</span>
                        <span>{date}</span>
                        <span>{readTime} min read</span>
                        <span>{language}</span>
                    </div>
                    {imageHtml}
                </div>
                """;
        }

        private static Dictionary<string, string> BuildContentDetailBodyHtml(ContentItem item, string typeRoute, ContentType? type)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in new[] { "en", "vi", "cn" })
                result[lang] = BuildContentDetailBodyHtml(item, typeRoute, lang, type);

            return result;
        }

        private static string BuildContentDetailBodyHtml(ContentItem item, string typeRoute, string lang, ContentType? type)
        {
            var body = type?.RequiresBody == false
                ? BuildResourceContentHtml(item, type, lang)
                : BuildContentBodyItemsHtml(item, lang);
            if (string.IsNullOrWhiteSpace(body))
                body = LangValue(item.BodyHtml, lang, string.Empty);
            if (string.IsNullOrWhiteSpace(body))
                body = $"<p>{H(LangValue(item.Summary, lang, string.Empty))}</p>";

            var backLabel = lang switch
            {
                "vi" => "Tro ve Insights",
                "cn" => "Back to Insights",
                _ => "Back to Insights"
            };

            return $"""
                <div class="sc-insight-detail-layout">
                    <article class="sc-insight-article">
                        {body}
                    </article>
                    {BuildContentDetailSidebarHtml(item, typeRoute, lang)}
                </div>
                <p class="sc-insight-back"><a href="/insights">{H(backLabel)}</a></p>
                """;
        }

        private static string BuildResourceContentHtml(ContentItem item, ContentType type, string lang)
        {
            var title = H(LangValue(item.Title, lang, item.Slug));
            var summary = H(LangValue(item.Summary, lang, string.Empty));
            var behavior = ResolveLibraryClickBehavior(item, type);
            var href = ResolveResourceHref(item, behavior);
            var buttonLabel = behavior switch
            {
                "video" => "Watch Video",
                "external" => "Open Link",
                _ => "Open File"
            };

            var action = string.IsNullOrWhiteSpace(href)
                ? "<p class=\"sc-insight-resource-note\">No file, video, or external URL is attached yet.</p>"
                : $"<a class=\"sc-insight-download\" href=\"{H(href)}\" target=\"_blank\" rel=\"noopener\"><span><strong>{title}</strong>{ResourceMeta(item, behavior)}</span><b>{H(buttonLabel)}</b></a>";

            return $"""
                <div class="sc-insight-resource-card">
                    <h2>{title}</h2>
                    <p>{summary}</p>
                    {action}
                </div>
                """;
        }

        private static string ResolveResourceHref(ContentItem item, string behavior) =>
            behavior switch
            {
                "video" => item.VideoUrl
                    ?? item.BodyItems.FirstOrDefault(i => i.Type == "video" && !string.IsNullOrWhiteSpace(i.Url))?.Url
                    ?? string.Empty,
                "external" => item.ExternalUrl ?? string.Empty,
                _ => item.Attachments.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Url))?.Url
                    ?? item.BodyItems.FirstOrDefault(i => i.Type == "file" && !string.IsNullOrWhiteSpace(i.Url))?.Url
                    ?? string.Empty
            };

        private static string ResourceMeta(ContentItem item, string behavior)
        {
            if (behavior == "video")
                return "<small>Video / Webinar</small>";
            if (behavior == "external")
                return "<small>External resource</small>";

            var file = item.Attachments.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Url));
            if (file is null)
                return "<small>Downloadable resource</small>";

            var meta = string.Join(" / ", new[] { FormatBytes(file.SizeBytes), file.ContentType }
                .Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(meta) ? string.Empty : $"<small>{H(meta)}</small>";
        }

        private static string BuildContentBodyItemsHtml(ContentItem item, string lang)
        {
            if (item.BodyItems.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, item.BodyItems
                .Where(i => i.Visible)
                .OrderBy(i => i.Order)
                .Select(i => RenderContentBodyItemHtml(i, lang))
                .Where(html => !string.IsNullOrWhiteSpace(html)));
        }

        private static string RenderContentBodyItemHtml(ContentBodyItem item, string lang)
        {
            var content = LangValue(item.Content, lang, string.Empty);
            var caption = LangValue(item.Caption, lang, string.Empty);
            var url = item.Url ?? string.Empty;
            var fileName = !string.IsNullOrWhiteSpace(item.FileName) ? item.FileName! : "Download";

            return item.Type switch
            {
                "image" when !string.IsNullOrWhiteSpace(url) =>
                    $"<figure class=\"sc-content-body-item sc-content-body-image\"><img src=\"{H(url)}\" alt=\"{H(caption)}\" />{RenderContentCaption(caption)}</figure>",
                "video" when !string.IsNullOrWhiteSpace(url) =>
                    $"<div class=\"sc-content-body-item sc-content-body-video\"><iframe src=\"{H(ToEmbedVideoUrl(url))}\" title=\"{H(caption)}\" loading=\"lazy\" allowfullscreen></iframe>{RenderContentCaption(caption)}</div>",
                "file" when !string.IsNullOrWhiteSpace(url) =>
                    $"<div class=\"sc-content-body-item\"><a class=\"sc-insight-download\" href=\"{H(url)}\" target=\"_blank\" rel=\"noopener\"><span><strong>{H(fileName)}</strong>{RenderContentFileMeta(item)}</span><b>Download</b></a></div>",
                "quote" when !string.IsNullOrWhiteSpace(content) =>
                    $"<blockquote class=\"sc-content-body-item sc-content-body-quote\">{H(content)}</blockquote>",
                "cta" when !string.IsNullOrWhiteSpace(content) =>
                    $"<div class=\"sc-content-body-item sc-content-body-cta\">{content}</div>",
                "divider" => "<hr class=\"sc-content-body-item sc-content-body-divider\" />",
                _ when !string.IsNullOrWhiteSpace(content) => $"<div class=\"sc-content-body-item sc-content-body-text\">{PlainTextToParagraphHtml(content)}</div>",
                _ => string.Empty
            };
        }

        private static string RenderContentCaption(string caption) =>
            string.IsNullOrWhiteSpace(caption) ? string.Empty : $"<figcaption>{H(caption)}</figcaption>";

        private static string RenderContentFileMeta(ContentBodyItem item)
        {
            var parts = new[] { FormatBytes(item.SizeBytes), item.ContentType }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            var meta = string.Join(" / ", parts);
            return string.IsNullOrWhiteSpace(meta) ? string.Empty : $"<small>{H(meta)}</small>";
        }

        private static string ToEmbedVideoUrl(string url)
        {
            if (url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase))
                return url;

            var match = Regex.Match(url, @"(?:youtube\.com/watch\?v=|youtu\.be/)([A-Za-z0-9_-]{6,})", RegexOptions.IgnoreCase);
            return match.Success ? $"https://www.youtube.com/embed/{match.Groups[1].Value}" : url;
        }

        private static string PlainTextToParagraphHtml(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (ContainsHtmlMarkup(value))
                return value;

            var paragraphs = Regex.Split(value.Trim(), @"(?:\r?\n){2,}")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => $"<p>{H(p).Replace("\n", "<br />")}</p>");

            return string.Join(Environment.NewLine, paragraphs);
        }

        private static bool ContainsHtmlMarkup(string value) =>
            Regex.IsMatch(
                value,
                @"<\s*(p|h[1-6]|ul|ol|li|blockquote|div|figure|figcaption|table|thead|tbody|tr|td|th|br|span|strong|b|em|i|u|a)\b",
                RegexOptions.IgnoreCase);

        private static string BuildContentDetailSidebarHtml(ContentItem item, string typeRoute, string lang)
        {
            var blocks = new List<string>();
            if (item.Attachments.Count > 0)
            {
                var downloads = string.Join("", item.Attachments.Select(a =>
                    $"<a class=\"sc-insight-download\" href=\"{H(a.Url)}\" target=\"_blank\" rel=\"noopener\"><span><strong>{H(a.FileName)}</strong><small>{H(FormatBytes(a.SizeBytes))} / {H(a.ContentType)}</small></span><b>Download</b></a>"));
                blocks.Add($"<div class=\"sc-insight-sidebar-block\"><h3>Download</h3>{downloads}</div>");
            }

            var title = U(LangValue(item.Title, lang, item.Slug));
            var pageUrl = U($"/insights/{typeRoute}/{item.Slug}");
            blocks.Add($"""
                <div class="sc-insight-sidebar-block">
                    <h3>Share</h3>
                    <div class="sc-insight-share">
                        <a href="/insights/{H(typeRoute)}/{H(item.Slug)}">Link</a>
                        <a target="_blank" rel="noopener" href="https://www.linkedin.com/shareArticle?mini=true&url={pageUrl}&title={title}">LinkedIn</a>
                        <a target="_blank" rel="noopener" href="https://twitter.com/intent/tweet?url={pageUrl}&text={title}">X</a>
                        <a href="mailto:?subject={title}&body={pageUrl}">Email</a>
                    </div>
                </div>
                """);

            if (item.Tags.Count > 0)
            {
                var tags = string.Join("", item.Tags.Select(t =>
                    $"<a class=\"sc-insight-tag\" href=\"/insights?tag={U(t)}\">{H(t)}</a>"));
                blocks.Add($"<div class=\"sc-insight-sidebar-block\"><h3>Tags</h3><div class=\"sc-insight-tags\">{tags}</div></div>");
            }

            return $"<aside class=\"sc-insight-sidebar\">{string.Join("", blocks)}</aside>";
        }

        private static Dictionary<string, string> AppendBackToInsightsLink(Dictionary<string, string> content)
        {
            var result = new Dictionary<string, string>(content);
            foreach (var lang in new[] { "en", "vi", "cn" })
            {
                var label = lang switch
                {
                    "vi" => "Quay lại Insights",
                    "cn" => "返回 Insights",
                    _ => "Back to Insights"
                };

                result[lang] = $"{result.GetValueOrDefault(lang, result.GetValueOrDefault("en", string.Empty))}<p style=\"text-align:right;margin-top:2rem\"><a href=\"/insights\" style=\"text-decoration:underline;color:inherit\">{label}</a></p>";
            }

            return result;
        }

        private static PublicSectionStyleDto ContentBodyStyle() => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#ffffff",
            Height = "auto",
            Padding = "large",
            ContentWidth = "normal",
            TextColor = "dark",
            MobileLayout = "stack",
            BlockLayoutMode = "stack",
            BlockGridColumns = 12,
            BlockGap = "medium"
        };

        private static string LangValue(Dictionary<string, string>? values, string lang, string fallback)
        {
            if (values is null || values.Count == 0)
                return fallback;
            if (values.TryGetValue(lang, out var localized) && !string.IsNullOrWhiteSpace(localized))
                return localized;
            if (values.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
                return english;
            return values.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? fallback;
        }

        private static int EstimateReadTime(ContentItem item, string lang)
        {
            var body = Regex.Replace(LangValue(item.BodyHtml, lang, string.Empty), "<.*?>", " ");
            var words = Regex.Matches(body, @"\b[\w'-]+\b").Count;
            return Math.Max(1, (int)Math.Ceiling(words / 220d));
        }

        private static string DisplayAuthor(string authorId) =>
            string.Equals(authorId, "admin", StringComparison.OrdinalIgnoreCase) ||
            authorId.Contains("admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "U&I Logistics";

        private static string Initials(string value)
        {
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "UI";

            return string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
        }

        private static string ContentTypeRoute(string typeKey) => typeKey switch
        {
            "article" => "articles",
            "case-study" => "case-studies",
            "whitepaper" => "whitepapers",
            "video" => "videos",
            "tool" => "tools",
            "technology" => "technology",
            _ => typeKey
        };

        private static string ContentTypeLabel(string typeKey) => typeKey switch
        {
            "case-study" => "Case Study",
            "whitepaper" => "Whitepaper",
            "video" => "Video",
            "tool" => "Tool",
            _ => "Article"
        };

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "File";

            var units = new[] { "B", "KB", "MB", "GB" };
            var size = (double)bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.#} {units[unit]}";
        }

        private static string H(string value) => WebUtility.HtmlEncode(value);

        private static string U(string value) => WebUtility.UrlEncode(value);

        private static PublicSectionStyleDto ContentCtaStyle() => new()
        {
            BackgroundType = "color",
            BackgroundColor = "#ffffff",
            Height = "auto",
            Padding = "large",
            ContentWidth = "normal",
            TextColor = "dark",
            MobileLayout = "stack",
            BlockLayoutMode = "stack",
            BlockGridColumns = 12,
            BlockGap = "medium"
        };

        private static string ResolveLibraryClickBehavior(ContentItem item, ContentType? type = null)
        {
            var configured = type?.ClickBehavior?.Trim().ToLowerInvariant();
            var behavior = ResolveLibraryContentBehavior(type);

            if (configured == "video" || behavior == "video-resource" || IsVideoLibraryType(item.ContentTypeKey) || IsVideoLibraryType(type?.Key))
                return "video";
            if (configured == "image" || behavior is "image-resource" or "gallery" || IsImageLibraryType(item.ContentTypeKey) || IsImageLibraryType(type?.Key))
                return "image";
            if (type?.RequiresBody == true || behavior == "page")
                return "detail";
            if (configured is "download" or "external")
                return configured;
            if (behavior == "file-resource" || type?.RequiresBody == false)
            {
                if (!string.IsNullOrWhiteSpace(item.VideoUrl) ||
                    item.BodyItems.Any(i => i.Type == "video" && !string.IsNullOrWhiteSpace(i.Url)))
                    return "video";
                if (!string.IsNullOrWhiteSpace(item.ExternalUrl))
                    return "external";
                return "download";
            }
            if (string.Equals(item.ContentTypeKey, "article", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ContentTypeKey, "case-study", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ContentTypeKey, "technology", StringComparison.OrdinalIgnoreCase))
                return "detail";
            if (item.Attachments.Any(a => !string.IsNullOrWhiteSpace(a.Url)) ||
                item.BodyItems.Any(i => i.Type == "file" && !string.IsNullOrWhiteSpace(i.Url)) ||
                string.Equals(item.ContentTypeKey, "whitepaper", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ContentTypeKey, "tool", StringComparison.OrdinalIgnoreCase))
                return "download";
            if (!string.IsNullOrWhiteSpace(item.ExternalUrl))
                return "external";
            return "detail";
        }

        private static string ResolveLibraryContentBehavior(ContentType? type)
        {
            var behavior = (type?.Behavior ?? string.Empty).Trim().ToLowerInvariant();
            if (behavior is "page" or "file-resource" or "video-resource" or "image-resource" or "gallery")
                return behavior;
            if (type?.RequiresVideoUrl == true || string.Equals(type?.ClickBehavior, "video", StringComparison.OrdinalIgnoreCase))
                return "video-resource";
            if (type?.RequiresFile == true || string.Equals(type?.ClickBehavior, "download", StringComparison.OrdinalIgnoreCase))
                return "file-resource";
            return type?.RequiresBody == false ? "file-resource" : "page";
        }

        private static bool IsVideoLibraryType(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var normalized = key.Trim().ToLowerInvariant();
            return normalized.Contains("video", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("webinar", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageLibraryType(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var normalized = key.Trim().ToLowerInvariant();
            return normalized.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("gallery", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("photo", StringComparison.OrdinalIgnoreCase);
        }

        private static ShowcaseItemOverride? FindShowcaseOverride(Page child, IReadOnlyDictionary<string, ShowcaseItemOverride> overrides)
        {
            if (overrides.TryGetValue(child.Id, out var byId))
                return byId;

            if (!string.IsNullOrWhiteSpace(child.SourceId) && overrides.TryGetValue(child.SourceId, out var bySourceId))
                return bySourceId;

            if (!string.IsNullOrWhiteSpace(child.StableId) && overrides.TryGetValue(child.StableId, out var byStableId))
                return byStableId;

            return null;
        }

        private static PublicChildCardContentDto MapShowcaseCard(PageCard? fallback, ShowcaseItemOverride? itemOverride) => new()
        {
            CardTitle = HasValues(itemOverride?.CardTitle) ? itemOverride!.CardTitle : fallback?.CardTitle ?? new Dictionary<string, string>(),
            CardContent = HasValues(itemOverride?.CardContent) ? itemOverride!.CardContent : fallback?.CardContent ?? new Dictionary<string, string>(),
            CardBackgroundType = !string.IsNullOrWhiteSpace(itemOverride?.CardBackgroundType) ? itemOverride!.CardBackgroundType : fallback?.CardBackgroundType,
            CardBackgroundColor = !string.IsNullOrWhiteSpace(itemOverride?.CardBackgroundColor) ? itemOverride!.CardBackgroundColor : fallback?.CardBackgroundColor,
            CardImageUrl = itemOverride?.CardImageUrl ?? fallback?.CardImageUrl
        };

        private static bool HasValues(Dictionary<string, string>? value) =>
            value is not null && value.Values.Any(v => !string.IsNullOrWhiteSpace(v));

        private static Dictionary<string, string> ResolveShowcaseButtonLabel(ShowcaseSection section) =>
            section.ButtonLabelText.Values.Any(v => !string.IsNullOrWhiteSpace(v))
                ? section.ButtonLabelText
                : new Dictionary<string, string> { ["en"] = "Learn More" };

        private static PublicSectionButtonDto MapButton(SectionButton b) => new()
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

        private static PublicSectionStyleDto MapStyle(SectionStyle style) => new()
        {
            BackgroundType = style.BackgroundType,
            BackgroundColor = style.BackgroundColor,
            BackgroundImageUrl = style.BackgroundImageUrl,
            BackgroundVideoUrl = style.BackgroundVideoUrl,
            GradientFrom = style.GradientFrom,
            GradientTo = style.GradientTo,
            GradientDirection = style.GradientDirection,
            OverlayColor = style.OverlayColor,
            OverlayOpacity = style.OverlayOpacity,
            Height = style.Height,
            CustomMinHeightPx = style.CustomMinHeightPx,
            Padding = style.Padding,
            ContentWidth = style.ContentWidth,
            TextColor = style.TextColor,
            MobileLayout = style.MobileLayout,
            BlockLayoutMode = style.BlockLayoutMode,
            BlockGridColumns = style.BlockGridColumns,
            BlockGap = style.BlockGap
        };

        private static PublicBlockDto? MapPublicBlock(
            Block block,
            IReadOnlyDictionary<string, FormDefinition> formDefinitions)
        {
            PublicBlockDto? mapped = block switch
            {
                TextBlock text => new PublicTextBlockDto
                {
                    Type = "text",
                    Title = text.Title,
                    Content = text.Content
                },
                ImageBlock image => new PublicImageBlockDto
                {
                    Type = "image",
                    ImageUrl = image.ImageUrl,
                    AltText = image.AltText
                },
                VideoBlock video => new PublicVideoBlockDto
                {
                    Type = "video",
                    EmbedUrl = video.EmbedUrl
                },
                FileBlock file => new PublicFileBlockDto
                {
                    Type = "file",
                    FileUrl = file.FileUrl,
                    Filename = file.Filename,
                    FileType = file.FileType
                },
                MapBlock map => new PublicMapBlockDto
                {
                    Type = "map",
                    CenterLat = map.CenterLat,
                    CenterLng = map.CenterLng,
                    DefaultZoom = map.DefaultZoom,
                    Pins = map.Pins.Select(p => new PublicMapPinDto
                    {
                        Id = p.Id,
                        Label = p.Label,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Href = p.Href
                    }).ToList()
                },
                FormBlock form => MapFormBlock(form, formDefinitions),
                CardBlock card => new PublicCardBlockDto
                {
                    Type = "card",
                    Icon = card.Icon,
                    Title = card.Title,
                    Description = card.Description,
                    ImageUrl = card.ImageUrl,
                    ButtonLabel = card.ButtonLabel,
                    Href = card.Href,
                    Action = card.Action,
                    FormDefinitionId = card.FormDefinitionId
                },
                ButtonBlock button => new PublicButtonBlockDto
                {
                    Type = "button",
                    Label = button.Label,
                    Href = button.Href,
                    Action = button.Action,
                    FormDefinitionId = button.FormDefinitionId,
                    Style = button.Style
                },
                MetricBlock metric => new PublicMetricBlockDto
                {
                    Type = "metric",
                    Icon = metric.Icon,
                    Label = metric.Label,
                    Value = metric.Value,
                    Prefix = metric.Prefix,
                    Suffix = metric.Suffix,
                    Description = metric.Description
                },
                BulletListBlock list => new PublicBulletListBlockDto
                {
                    Type = "bullet-list",
                    Title = list.Title,
                    Items = list.Items.Select(i => new PublicBulletListItemDto
                    {
                        Id = i.Id,
                        Icon = i.Icon,
                        Text = i.Text,
                        Visible = i.Visible,
                        Order = i.Order
                    }).ToList()
                },
                StepBlock step => new PublicStepBlockDto
                {
                    Type = "step",
                    Icon = step.Icon,
                    StepLabel = step.StepLabel,
                    Title = step.Title,
                    Description = step.Description
                },
                IconBlock icon => new PublicIconBlockDto
                {
                    Type = "icon",
                    Icon = icon.Icon,
                    Label = icon.Label,
                    Description = icon.Description
                },
                ContainerBlock container => new PublicContainerBlockDto
                {
                    Type = "container",
                    Title = container.Title,
                    LayoutMode = container.LayoutMode,
                    Columns = container.Columns,
                    Gap = container.Gap,
                    OrbitRadius = container.OrbitRadius,
                    OrbitStartAngle = container.OrbitStartAngle,
                    SemicircleRadius = container.SemicircleRadius,
                    SemicircleStartAngle = container.SemicircleStartAngle,
                    SemicircleEndAngle = container.SemicircleEndAngle
                },
                _ => null
            };

            if (mapped is null) return null;

            mapped.Id = block.Id;
            mapped.Visible = block.Visible;
            mapped.Order = block.Order;
            mapped.BlockZone = block.BlockZone;
            mapped.ZoneId = block.BlockZone;
            mapped.PositionMode = ResolveBlockPositionMode(block);
            mapped.ParentBlockId = block.ParentBlockId;
            mapped.Layout = MapBlockLayout(block.Layout);
            mapped.Buttons = block.Buttons
                .Where(b => b.Visible)
                .OrderBy(b => b.Order)
                .Select(b => new PublicBlockButtonDto
                {
                    Id = b.Id,
                    Label = b.Label,
                    Action = b.Action.ToString(),
                    Href = b.Href,
                    FormDefinitionId = b.FormDefinitionId,
                    Visible = b.Visible,
                    Order = b.Order
                })
                .ToList();

            return mapped;
        }

        private static PublicFormBlockDto MapFormBlock(
            FormBlock block,
            IReadOnlyDictionary<string, FormDefinition> definitions)
        {
            if (!string.IsNullOrWhiteSpace(block.FormDefinitionId) &&
                definitions.TryGetValue(block.FormDefinitionId, out var definition))
            {
                return new PublicFormBlockDto
                {
                    Type = "form",
                    FormDefinitionId = definition.Id,
                    Name = definition.Name,
                    Introduction = definition.Introduction,
                    FormLayoutMode = definition.Layout == Contracts.Forms.FormLayout.TwoColumns ? "two-columns" : "stacked",
                    SubmitButtonLabel = definition.SubmitButtonLabel,
                    Fields = definition.Fields
                        .OrderBy(field => field.Order)
                        .Select(field => new PublicFormFieldDto
                        {
                            Name = field.Key,
                            Type = field.Type,
                            Label = field.Label,
                            Placeholder = field.Placeholder,
                            Required = field.Required,
                            Options = field.Options
                                .OrderBy(option => option.Order)
                                .Select(option => new PublicFormFieldOptionDto
                                {
                                    Value = option.Value,
                                    Label = option.Label,
                                    Order = option.Order
                                }).ToList(),
                            Order = field.Order
                        }).ToList()
                };
            }

            return new PublicFormBlockDto
            {
                Type = "form",
                FormDefinitionId = block.FormDefinitionId,
                SubmitButtonLabel = block.SubmitButtonLabel,
                Fields = block.Fields.Select(field => new PublicFormFieldDto
                {
                    Name = field.Name,
                    Type = field.Type,
                    Label = field.Label,
                    Required = field.Required,
                    Options = (field.Options ?? new List<string>())
                        .Select((option, index) => new PublicFormFieldOptionDto
                        {
                            Value = option,
                            Label = new Dictionary<string, string> { ["en"] = option },
                            Order = index
                        }).ToList(),
                    Order = field.Order
                }).ToList()
            };
        }

        private static PublicBlockLayoutDto MapBlockLayout(BlockLayout? layout)
        {
            layout ??= new BlockLayout();

            return new PublicBlockLayoutDto
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

        private static string ResolveBlockPositionMode(Block block)
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

