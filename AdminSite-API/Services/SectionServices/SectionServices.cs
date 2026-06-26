using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using FullProject.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Contracts.Admin;
using FullProject.Services.AssetService;

namespace FullProject.Services.SectionServices
{
    public class SectionService
    {
        private readonly MongoDbContext _context;
        private readonly BlockService _blockService;
        private readonly AssetCleanupService _assetCleanup;
        private static readonly Ganss.Xss.HtmlSanitizer _sanitizer = new();

        public SectionService(MongoDbContext context, BlockService blockService, AssetCleanupService assetCleanup)
        {
            _context = context;
            _blockService = blockService;
            _assetCleanup = assetCleanup;
        }

        // -----------------------------------------------------------
        // ADMIN WORKSPACE BACKEND METHODS (DRAFT EXCLUSIVE)
        // -----------------------------------------------------------

        public async Task<List<Section>> GetByPageAsync(string pageId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return new();
            return await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId)
                .SortBy(s => s.Order)
                .ToListAsync();
        }

        public async Task<Section?> GetByIdAsync(string pageId, string sectionId)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return null;
            return await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId && s.Id == sectionId)
                .FirstOrDefaultAsync();
        }

        public async Task<Section> CreateAsync(string pageId, SectionCreateDto dto)
        {
            // Fetch page first — needed for StableId and count
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync()
                ?? throw new ArgumentException("Page not found");

            var count = await _context.SectionsDraft
                .CountDocumentsAsync(s => s.PageStableId == page.StableId);

            Section section = dto switch
            {
                HeroSectionCreateDto h => new HeroSection
                {
                    Layout = h.Layout,
                    Eyebrow = h.Eyebrow,
                    Heading = h.Heading,
                    Subheading = h.Subheading,
                    HeadingSize = h.HeadingSize,
                    ContentAlignment = h.ContentAlignment,
                    ImageUrl = h.ImageUrl,
                    Buttons = h.Buttons.Select(MapButton).ToList()
                },
                CtaSectionCreateDto c => new CtaSection
                {
                    Layout = c.Layout,
                    Heading = c.Heading,
                    Subtext = c.Subtext,
                    Button = c.Button != null ? MapButton(c.Button) : null,
                    Buttons = c.Buttons.Select(MapButton).ToList()
                },
                ListSectionCreateDto l => new ListSection
                {
                    Layout = l.Layout,
                    Columns = l.Columns,
                    SectionTitle = l.SectionTitle,
                    ShowIcon = l.ShowIcon,
                    Items = l.Items.Select((item, i) => new ListItem
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Icon = item.Icon,
                        Title = item.Title,
                        Description = item.Description,
                        ImageUrl = item.ImageUrl,
                        LinkHref = CleanUrl(item.LinkHref),
                        Visible = item.Visible,
                        Order = i
                    }).ToList()
                },
                HtmlSectionCreateDto html => new HtmlSection
                {
                    Content = html.Content.ToDictionary(
                        kv => kv.Key,
                        kv => SanitizeHtml(kv.Value))
                },
                ColumnsSectionCreateDto col => new ColumnsSection
                {
                    ColumnCount = col.ColumnCount,
                    ColumnRatio = col.ColumnRatio,
                    Gap = col.Gap,
                    StackOnMobile = col.StackOnMobile,
                    Columns = Enumerable.Range(0, col.ColumnCount).Select(i => new ColumnSlot
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Order = i
                    }).ToList()
                },
                ShowcaseSectionCreateDto ld => new ShowcaseSection
                {
                    SourcePageId = ld.SourcePageId,
                    Layout = ld.Layout,
                    Columns = ld.Columns,
                    Limit = NormalizeShowcaseLimit(ld.Limit),
                    Eyebrow = ld.Eyebrow,
                    SectionTitle = ld.SectionTitle,
                    ShowImage = ld.ShowImage,
                    ShowContent = ld.ShowContent,
                    ShowItemButton = ld.ShowItemButton,
                    ButtonLabelText = NormalizeButtonLabel(ld.ButtonLabel),
                    ActionButton = ld.ActionButton != null ? MapButton(ld.ActionButton) : null,
                    ActionButtonPosition = NormalizeActionButtonPosition(ld.ActionButtonPosition),
                    ShowSearchBar = ld.ShowSearchBar,
                    SearchPlaceholder = ld.SearchPlaceholder,
                    ItemOverrides = ld.Items.Select(MapShowcaseItemOverride).ToList()
                },
                LibrarySectionCreateDto library => new LibrarySection
                {
                    ContentTypes = NormalizeContentTypes(library.ContentTypes),
                    Layout = NormalizeLibraryLayout(library.Layout),
                    Columns = Math.Clamp(library.Columns, 1, 6),
                    Rows = Math.Clamp(library.Rows, 1, 12),
                    Limit = Math.Clamp(library.Limit, 1, 24),
                    EnableTabs = library.EnableTabs,
                    EnablePagination = library.EnablePagination,
                    Eyebrow = library.Eyebrow,
                    SectionTitle = library.SectionTitle,
                    Subheading = library.Subheading,
                    ShowImage = library.ShowImage,
                    ShowSummary = library.ShowSummary,
                    ShowButton = library.ShowButton,
                    ShowTime = library.ShowTime,
                    ButtonLabel = NormalizeButtonLabel(library.ButtonLabel),
                    ButtonStyle = NormalizeButtonStyle(library.ButtonStyle),
                    ShowSearchBar = library.ShowSearchBar,
                    ShowFilters = library.ShowFilters,
                    SearchPlaceholder = library.SearchPlaceholder,
                    SortMode = NormalizeLibrarySort(library.SortMode)
                },
                StatsSectionCreateDto st => new StatsSection
                {
                    SectionTitle = st.SectionTitle,
                    Columns = st.Columns,
                    DurationMs = st.DurationMs,
                    Items = st.Items.Select((item, i) => new StatItem
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Label = item.Label,
                        Value = item.Value,
                        Prefix = item.Prefix,
                        Suffix = item.Suffix,
                        Visible = item.Visible,
                        Order = i
                    }).ToList()
                },
                CarouselSectionCreateDto ca => new CarouselSection
                {
                    SectionTitle = ca.SectionTitle,
                    Layout = NormalizeCarouselLayout(ca.Layout),
                    Columns = ca.Columns,
                    Autoplay = ca.Autoplay,
                    ShowDots = ca.ShowDots,
                    ShowArrows = ca.ShowArrows,
                    Items = ca.Items.Select(MapCarouselItem).ToList()
                },
                NetworkMapSectionCreateDto map => new NetworkMapSection
                {
                    SectionTitle = map.SectionTitle,
                    CenterLat = map.CenterLat,
                    CenterLng = map.CenterLng,
                    DefaultZoom = map.DefaultZoom,
                    Pins = map.Pins.Select((pin, i) => new NetworkMapPin
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Label = pin.Label,
                        Lat = pin.Lat,
                        Lng = pin.Lng,
                        Href = CleanUrl(pin.Href),
                        Visible = pin.Visible,
                        Order = i
                    }).ToList()
                },
                TestimonialSectionCreateDto t => new TestimonialSection
                {
                    Eyebrow = t.Eyebrow,
                    SectionTitle = t.SectionTitle,
                    Subheading = t.Subheading,
                    Layout = t.Layout,
                    HeaderAlignment = t.HeaderAlignment,
                    Columns = t.Columns,
                    Items = t.Items.Select(MapTestimonialItem).ToList()
                },
                CanvasSectionCreateDto canvas => new CanvasSection
                {
                    AdminLabel = canvas.AdminLabel
                },
                _ => throw new ArgumentException("Unknown section type")
            };

            section.StableId = Guid.NewGuid().ToString();
            section.PageStableId = page.StableId; // GUID, not ObjectId
            section.Visible = dto.Visible;
            section.Order = (int)count;
            section.Version = 1;
            section.CreatedAt = DateTime.UtcNow;
            section.UpdatedAt = DateTime.UtcNow;

            if (dto.Style != null)
            {
                var s = dto.Style;
                if (s.BackgroundType != null) section.Style.BackgroundType = s.BackgroundType;
                if (s.BackgroundColor != null) section.Style.BackgroundColor = s.BackgroundColor;
                if (s.BackgroundImageUrl != null) section.Style.BackgroundImageUrl = s.BackgroundImageUrl;
                if (s.BackgroundVideoUrl != null) section.Style.BackgroundVideoUrl = CleanBackgroundVideoUrl(s.BackgroundVideoUrl);
                if (s.BackgroundImageFit != null) section.Style.BackgroundImageFit = NormalizeBackgroundImageFit(s.BackgroundImageFit);
                if (s.BackgroundImagePosition != null) section.Style.BackgroundImagePosition = NormalizeBackgroundImagePosition(s.BackgroundImagePosition);
                if (s.GradientFrom != null) section.Style.GradientFrom = s.GradientFrom;
                if (s.GradientTo != null) section.Style.GradientTo = s.GradientTo;
                if (s.GradientDirection != null) section.Style.GradientDirection = s.GradientDirection;
                if (s.OverlayColor != null) section.Style.OverlayColor = s.OverlayColor;
                if (s.OverlayOpacity != null) section.Style.OverlayOpacity = s.OverlayOpacity.Value;
                if (s.Height != null) section.Style.Height = s.Height;
                if (s.CustomMinHeightPx != null) section.Style.CustomMinHeightPx = Math.Clamp(s.CustomMinHeightPx.Value, 120, 3000);
                if (s.Padding != null) section.Style.Padding = s.Padding;
                if (s.ContentWidth != null) section.Style.ContentWidth = s.ContentWidth;
                if (s.TextColor != null) section.Style.TextColor = s.TextColor;
                if (s.MobileLayout != null) section.Style.MobileLayout = s.MobileLayout;
                if (s.BlockLayoutMode != null) section.Style.BlockLayoutMode = NormalizeBlockLayoutMode(s.BlockLayoutMode);
                if (s.BlockGridColumns != null) section.Style.BlockGridColumns = Math.Clamp(s.BlockGridColumns.Value, 1, 12);
                if (s.BlockGap != null) section.Style.BlockGap = NormalizeBlockGap(s.BlockGap);
            }

            if (section is CanvasSection)
            {
                section.Style.BlockLayoutMode = "freeform";
                section.Style.BlockGap = "none";
                section.Style.ContentWidth = "full";
                section.Style.Padding = "none";
                section.Style.Height = "custom";
                section.Style.CustomMinHeightPx ??= 640;
            }

            await _context.SectionsDraft.InsertOneAsync(section);

            // Cascade Hero heading/image to page card if not customized
            // Uses already-fetched page — no duplicate query
            if (dto is HeroSectionCreateDto heroDto && page.Card != null && !page.Card.IsCustomized)
            {
                var cardUpdates = new List<UpdateDefinition<Page>>
                {
                    Builders<Page>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow),
                    Builders<Page>.Update.Inc(p => p.Version, 1)
                };
                if (heroDto.Heading != null)
                    cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardTitle, heroDto.Heading));
                if (heroDto.Subheading != null)
                    cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardContent, heroDto.Subheading));
                if (heroDto.ImageUrl != null)
                    cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardImageUrl, heroDto.ImageUrl));

                await _context.PagesDraft.UpdateOneAsync(
                    p => p.Id == pageId,
                    Builders<Page>.Update.Combine(cardUpdates));
            }

            return section;
        }

        public async Task<Section?> UpdateAsync(string pageId, string sectionId, SectionUpdateDto dto)
        {
            var existing = await GetByIdAsync(pageId, sectionId);
            if (existing is null) return null;

            var baseUpdate = new List<UpdateDefinition<Section>>
            {
                Builders<Section>.Update.Set(s => s.UpdatedAt, DateTime.UtcNow),
                Builders<Section>.Update.Inc(s => s.Version, 1)
            };

            if (dto.Visible.HasValue)
                baseUpdate.Add(Builders<Section>.Update.Set(s => s.Visible, dto.Visible.Value));

            switch (existing, dto)
            {
                case (HeroSection _, HeroSectionUpdateDto hDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (hDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).Layout, hDto.Layout));
                        if (hDto.Eyebrow != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).Eyebrow, hDto.Eyebrow));
                        if (hDto.Heading != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).Heading, hDto.Heading));
                        if (hDto.Subheading != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).Subheading, hDto.Subheading));
                        if (hDto.HeadingSize != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).HeadingSize, hDto.HeadingSize));
                        if (hDto.ContentAlignment != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).ContentAlignment, hDto.ContentAlignment));
                        if (hDto.ImageUrl != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).ImageUrl, EmptyToNull(hDto.ImageUrl)));
                        if (hDto.Buttons != null) u.Add(Builders<Section>.Update.Set(s => ((HeroSection)s).Buttons, hDto.Buttons.Select(MapButton).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));

                        var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
                        if (page?.Card != null && !page.Card.IsCustomized)
                        {
                            var cardUpdates = new List<UpdateDefinition<Page>>
                        {
                            Builders<Page>.Update.Set(p => p.UpdatedAt, DateTime.UtcNow),
                            Builders<Page>.Update.Inc(p => p.Version, 1)
                        };
                            if (hDto.Heading != null)
                                cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardTitle, hDto.Heading));
                            if (hDto.Subheading != null)
                                cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardContent, hDto.Subheading));
                            if (hDto.ImageUrl != null)
                                cardUpdates.Add(Builders<Page>.Update.Set(p => p.Card!.CardImageUrl, EmptyToNull(hDto.ImageUrl)));
                            await _context.PagesDraft.UpdateOneAsync(p => p.Id == pageId, Builders<Page>.Update.Combine(cardUpdates));
                        }
                        break;
                    }
                case (CtaSection _, CtaSectionUpdateDto cDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (cDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((CtaSection)s).Layout, cDto.Layout));
                        if (cDto.Heading != null) u.Add(Builders<Section>.Update.Set(s => ((CtaSection)s).Heading, cDto.Heading));
                        if (cDto.Subtext != null) u.Add(Builders<Section>.Update.Set(s => ((CtaSection)s).Subtext, cDto.Subtext));
                        if (cDto.Button != null) u.Add(Builders<Section>.Update.Set(s => ((CtaSection)s).Button, MapButton(cDto.Button)));
                        if (cDto.Buttons != null) u.Add(Builders<Section>.Update.Set(s => ((CtaSection)s).Buttons, cDto.Buttons.Select(MapButton).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (ListSection _, ListSectionUpdateDto lDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (lDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((ListSection)s).Layout, lDto.Layout));
                        if (lDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((ListSection)s).Columns, lDto.Columns.Value));
                        if (lDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((ListSection)s).SectionTitle, lDto.SectionTitle));
                        if (lDto.ShowIcon != null) u.Add(Builders<Section>.Update.Set(s => ((ListSection)s).ShowIcon, lDto.ShowIcon.Value));
                        if (lDto.Items != null) u.Add(Builders<Section>.Update.Set(s => ((ListSection)s).Items,
                            lDto.Items.Select((item, i) => new ListItem
                            {
                                Id = ObjectId.GenerateNewId().ToString(),
                                Icon = item.Icon,
                                Title = item.Title,
                                Description = item.Description,
                                ImageUrl = item.ImageUrl,
                                LinkHref = CleanUrl(item.LinkHref),
                                Visible = item.Visible,
                                Order = i
                            }).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (HtmlSection _, HtmlSectionUpdateDto htmlDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (htmlDto.Content != null)
                        {
                            var sanitized = htmlDto.Content.ToDictionary(
                                kv => kv.Key,
                                kv => SanitizeHtml(kv.Value));
                            u.Add(Builders<Section>.Update.Set(s => ((HtmlSection)s).Content, sanitized));
                        }
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (ColumnsSection col, ColumnsSectionUpdateDto colDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (colDto.ColumnCount != null)
                        {
                            var slots = ReconcileColumnSlots(col.Columns, colDto.ColumnCount.Value);
                            var removedSlotIds = col.Columns
                                .Select(slot => slot.Id)
                                .Except(slots.Select(slot => slot.Id))
                                .ToList();

                            if (removedSlotIds.Count > 0)
                            {
                                var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
                                if (page is not null)
                                {
                                    await _blockService.DeleteByColumnSlotsAsync(page.StableId, col.StableId, removedSlotIds);
                                }
                            }

                            u.Add(Builders<Section>.Update.Set(s => ((ColumnsSection)s).ColumnCount, slots.Count));
                            u.Add(Builders<Section>.Update.Set(s => ((ColumnsSection)s).Columns, slots));
                        }
                        if (colDto.ColumnRatio != null) u.Add(Builders<Section>.Update.Set(s => ((ColumnsSection)s).ColumnRatio, colDto.ColumnRatio));
                        if (colDto.Gap != null) u.Add(Builders<Section>.Update.Set(s => ((ColumnsSection)s).Gap, colDto.Gap));
                        if (colDto.StackOnMobile != null) u.Add(Builders<Section>.Update.Set(s => ((ColumnsSection)s).StackOnMobile, colDto.StackOnMobile.Value));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (ShowcaseSection _, ShowcaseSectionUpdateDto ldDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (ldDto.SourcePageId != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).SourcePageId, ldDto.SourcePageId));
                        if (ldDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).Layout, ldDto.Layout));
                        if (ldDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).Columns, ldDto.Columns.Value));
                        if (ldDto.Limit != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).Limit, NormalizeShowcaseLimit(ldDto.Limit.Value)));
                        if (ldDto.Eyebrow != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).Eyebrow, ldDto.Eyebrow));
                        if (ldDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).SectionTitle, ldDto.SectionTitle));
                        if (ldDto.ShowImage != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ShowImage, ldDto.ShowImage.Value));
                        if (ldDto.ShowContent != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ShowContent, ldDto.ShowContent.Value));
                        if (ldDto.ShowItemButton != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ShowItemButton, ldDto.ShowItemButton.Value));
                        if (ldDto.ButtonLabel != null)
                        {
                            u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ButtonLabelText, NormalizeButtonLabel(ldDto.ButtonLabel)));
                        }
                        if (ldDto.ActionButton != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ActionButton, MapButton(ldDto.ActionButton)));
                        if (ldDto.ActionButtonPosition != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ActionButtonPosition, NormalizeActionButtonPosition(ldDto.ActionButtonPosition)));
                        if (ldDto.ShowSearchBar != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ShowSearchBar, ldDto.ShowSearchBar.Value));
                        if (ldDto.SearchPlaceholder != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).SearchPlaceholder, ldDto.SearchPlaceholder));
                        if (ldDto.Items != null) u.Add(Builders<Section>.Update.Set(s => ((ShowcaseSection)s).ItemOverrides, ldDto.Items.Select(MapShowcaseItemOverride).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (LibrarySection _, LibrarySectionUpdateDto libraryDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (libraryDto.ContentTypes != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ContentTypes, NormalizeContentTypes(libraryDto.ContentTypes)));
                        if (libraryDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Layout, NormalizeLibraryLayout(libraryDto.Layout)));
                        if (libraryDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Columns, Math.Clamp(libraryDto.Columns.Value, 1, 6)));
                        if (libraryDto.Rows != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Rows, Math.Clamp(libraryDto.Rows.Value, 1, 12)));
                        if (libraryDto.Limit != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Limit, Math.Clamp(libraryDto.Limit.Value, 1, 24)));
                        if (libraryDto.EnableTabs != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).EnableTabs, libraryDto.EnableTabs.Value));
                        if (libraryDto.EnablePagination != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).EnablePagination, libraryDto.EnablePagination.Value));
                        if (libraryDto.Eyebrow != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Eyebrow, libraryDto.Eyebrow));
                        if (libraryDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).SectionTitle, libraryDto.SectionTitle));
                        if (libraryDto.Subheading != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).Subheading, libraryDto.Subheading));
                        if (libraryDto.ShowImage != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowImage, libraryDto.ShowImage.Value));
                        if (libraryDto.ShowSummary != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowSummary, libraryDto.ShowSummary.Value));
                        if (libraryDto.ShowButton != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowButton, libraryDto.ShowButton.Value));
                        if (libraryDto.ShowTime != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowTime, libraryDto.ShowTime.Value));
                        if (libraryDto.ButtonLabel != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ButtonLabel, NormalizeButtonLabel(libraryDto.ButtonLabel)));
                        if (libraryDto.ButtonStyle != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ButtonStyle, NormalizeButtonStyle(libraryDto.ButtonStyle)));
                        if (libraryDto.ShowSearchBar != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowSearchBar, libraryDto.ShowSearchBar.Value));
                        if (libraryDto.ShowFilters != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).ShowFilters, libraryDto.ShowFilters.Value));
                        if (libraryDto.SearchPlaceholder != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).SearchPlaceholder, libraryDto.SearchPlaceholder));
                        if (libraryDto.SortMode != null) u.Add(Builders<Section>.Update.Set(s => ((LibrarySection)s).SortMode, NormalizeLibrarySort(libraryDto.SortMode)));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (StatsSection _, StatsSectionUpdateDto stDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (stDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((StatsSection)s).SectionTitle, stDto.SectionTitle));
                        if (stDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((StatsSection)s).Columns, stDto.Columns.Value));
                        if (stDto.DurationMs != null) u.Add(Builders<Section>.Update.Set(s => ((StatsSection)s).DurationMs, stDto.DurationMs.Value));
                        if (stDto.Items != null) u.Add(Builders<Section>.Update.Set(s => ((StatsSection)s).Items,
                            stDto.Items.Select((item, i) => new StatItem
                            {
                                Id = ObjectId.GenerateNewId().ToString(),
                                Label = item.Label,
                                Value = item.Value,
                                Prefix = item.Prefix,
                                Suffix = item.Suffix,
                                Visible = item.Visible,
                                Order = i
                            }).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (CarouselSection _, CarouselSectionUpdateDto caDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (caDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).SectionTitle, caDto.SectionTitle));
                        if (caDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).Layout, NormalizeCarouselLayout(caDto.Layout)));
                        if (caDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).Columns, caDto.Columns.Value));
                        if (caDto.Autoplay != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).Autoplay, caDto.Autoplay.Value));
                        if (caDto.ShowDots != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).ShowDots, caDto.ShowDots.Value));
                        if (caDto.ShowArrows != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).ShowArrows, caDto.ShowArrows.Value));
                        if (caDto.Items != null) u.Add(Builders<Section>.Update.Set(s => ((CarouselSection)s).Items,
                            caDto.Items.Select(MapCarouselItem).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (NetworkMapSection _, NetworkMapSectionUpdateDto mapDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (mapDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((NetworkMapSection)s).SectionTitle, mapDto.SectionTitle));
                        if (mapDto.CenterLat != null) u.Add(Builders<Section>.Update.Set(s => ((NetworkMapSection)s).CenterLat, mapDto.CenterLat.Value));
                        if (mapDto.CenterLng != null) u.Add(Builders<Section>.Update.Set(s => ((NetworkMapSection)s).CenterLng, mapDto.CenterLng.Value));
                        if (mapDto.DefaultZoom != null) u.Add(Builders<Section>.Update.Set(s => ((NetworkMapSection)s).DefaultZoom, mapDto.DefaultZoom.Value));
                        if (mapDto.Pins != null) u.Add(Builders<Section>.Update.Set(s => ((NetworkMapSection)s).Pins,
                            mapDto.Pins.Select((pin, i) => new NetworkMapPin
                            {
                                Id = ObjectId.GenerateNewId().ToString(),
                                Label = pin.Label,
                                Lat = pin.Lat,
                                Lng = pin.Lng,
                                Href = CleanUrl(pin.Href),
                                Visible = pin.Visible,
                                Order = i
                            }).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (TestimonialSection _, TestimonialSectionUpdateDto tDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (tDto.Eyebrow != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).Eyebrow, tDto.Eyebrow));
                        if (tDto.SectionTitle != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).SectionTitle, tDto.SectionTitle));
                        if (tDto.Subheading != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).Subheading, tDto.Subheading));
                        if (tDto.Layout != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).Layout, tDto.Layout));
                        if (tDto.HeaderAlignment != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).HeaderAlignment, tDto.HeaderAlignment));
                        if (tDto.Columns != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).Columns, tDto.Columns.Value));
                        if (tDto.Items != null) u.Add(Builders<Section>.Update.Set(s => ((TestimonialSection)s).Items, tDto.Items.Select(MapTestimonialItem).ToList()));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                case (CanvasSection _, CanvasSectionUpdateDto canvasDto):
                    {
                        var u = new List<UpdateDefinition<Section>>(baseUpdate);
                        if (canvasDto.AdminLabel != null) u.Add(Builders<Section>.Update.Set(s => ((CanvasSection)s).AdminLabel, canvasDto.AdminLabel));
                        await _context.SectionsDraft.UpdateOneAsync(s => s.Id == sectionId, Builders<Section>.Update.Combine(u));
                        break;
                    }
                default:
                    await _context.SectionsDraft.UpdateOneAsync(
                        s => s.Id == sectionId,
                        Builders<Section>.Update.Combine(baseUpdate));
                    break;
            }

            await DeleteReplacedSectionAssetsAsync(existing, dto);

            return await GetByIdAsync(pageId, sectionId);
        }

        public async Task<bool> DeleteAsync(string pageId, string sectionId)
        {
            // Fetch both before deleting — need StableIds for block cleanup
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;

            var section = await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId && s.Id == sectionId)
                .FirstOrDefaultAsync();
            if (section is null) return false;

            var removedAssetUrls = _assetCleanup.SectionAssetUrls(section).ToList();

            var result = await _context.SectionsDraft.DeleteOneAsync(
                s => s.PageStableId == page.StableId && s.Id == sectionId);

            if (result.DeletedCount > 0)
            {
                await _blockService.DeleteBySectionAsync(page.StableId, section.StableId);
                await _assetCleanup.DeleteUnusedAsync(removedAssetUrls);
            }

            return result.DeletedCount > 0;
        }

        public async Task<bool> SetVisibilityAsync(string pageId, string sectionId, bool visible)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;

            var result = await _context.SectionsDraft.UpdateOneAsync(
                s => s.PageStableId == page.StableId && s.Id == sectionId,
                Builders<Section>.Update
                    .Set(s => s.Visible, visible)
                    .Inc(s => s.Version, 1)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow));
            return result.ModifiedCount > 0;
        }

        public async Task<Section?> UpdateStyleAsync(string pageId, string sectionId, SectionStyleDto dto)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return null;

            var section = await _context.SectionsDraft
                .Find(s => s.PageStableId == page.StableId && s.Id == sectionId)
                .FirstOrDefaultAsync();
            if (section is null) return null;

            var oldBackgroundImageUrl = section.Style?.BackgroundImageUrl;
            var oldBackgroundVideoUrl = section.Style?.BackgroundVideoUrl;
            var style = section.Style ?? new SectionStyle();

            if (dto.BackgroundType != null) style.BackgroundType = dto.BackgroundType;
            if (dto.BackgroundColor != null) style.BackgroundColor = dto.BackgroundColor;
            if (dto.BackgroundImageUrl != null) style.BackgroundImageUrl = EmptyToNull(dto.BackgroundImageUrl);
            if (dto.BackgroundVideoUrl != null) style.BackgroundVideoUrl = CleanBackgroundVideoUrl(dto.BackgroundVideoUrl);
            if (dto.BackgroundImageFit != null) style.BackgroundImageFit = NormalizeBackgroundImageFit(dto.BackgroundImageFit);
            if (dto.BackgroundImagePosition != null) style.BackgroundImagePosition = NormalizeBackgroundImagePosition(dto.BackgroundImagePosition);
            if (dto.GradientFrom != null) style.GradientFrom = EmptyToNull(dto.GradientFrom);
            if (dto.GradientTo != null) style.GradientTo = EmptyToNull(dto.GradientTo);
            if (dto.GradientDirection != null) style.GradientDirection = string.IsNullOrWhiteSpace(dto.GradientDirection) ? "top" : dto.GradientDirection;
            if (dto.OverlayColor != null) style.OverlayColor = EmptyToNull(dto.OverlayColor);
            if (dto.OverlayOpacity != null) style.OverlayOpacity = dto.OverlayOpacity.Value;
            if (dto.Height != null) style.Height = dto.Height;
            if (dto.CustomMinHeightPx != null) style.CustomMinHeightPx = Math.Clamp(dto.CustomMinHeightPx.Value, 120, 3000);
            if (dto.Padding != null) style.Padding = dto.Padding;
            if (dto.ContentWidth != null) style.ContentWidth = dto.ContentWidth;
            if (dto.TextColor != null) style.TextColor = dto.TextColor;
            if (dto.MobileLayout != null) style.MobileLayout = dto.MobileLayout;
            if (dto.BlockLayoutMode != null) style.BlockLayoutMode = NormalizeBlockLayoutMode(dto.BlockLayoutMode);
            if (dto.BlockGridColumns != null) style.BlockGridColumns = Math.Clamp(dto.BlockGridColumns.Value, 1, 12);
            if (dto.BlockGap != null) style.BlockGap = NormalizeBlockGap(dto.BlockGap);

            var result = await _context.SectionsDraft.UpdateOneAsync(
                s => s.PageStableId == page.StableId && s.Id == sectionId,
                Builders<Section>.Update
                    .Set(s => s.Style, style)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow)
                    .Inc(s => s.Version, 1));

            if (result.MatchedCount == 0) return null;
            if (dto.BackgroundImageUrl != null)
                await _assetCleanup.DeleteIfUnusedAsync(oldBackgroundImageUrl, style.BackgroundImageUrl);
            if (dto.BackgroundVideoUrl != null)
                await _assetCleanup.DeleteIfUnusedAsync(oldBackgroundVideoUrl, style.BackgroundVideoUrl);
            return await GetByIdAsync(pageId, sectionId);
        }


        private async Task DeleteReplacedSectionAssetsAsync(Section existing, SectionUpdateDto dto)
        {
            switch (existing, dto)
            {
                case (HeroSection hero, HeroSectionUpdateDto heroDto) when heroDto.ImageUrl != null:
                    await _assetCleanup.DeleteIfUnusedAsync(hero.ImageUrl, heroDto.ImageUrl);
                    break;
                case (ListSection list, ListSectionUpdateDto listDto) when listDto.Items != null:
                    await DeleteRemovedUrlsAsync(list.Items.Select(i => i.ImageUrl), listDto.Items.Select(i => i.ImageUrl));
                    break;
                case (ShowcaseSection showcase, ShowcaseSectionUpdateDto showcaseDto) when showcaseDto.Items != null:
                    await DeleteRemovedUrlsAsync(showcase.ItemOverrides.Select(i => i.CardImageUrl), showcaseDto.Items.Select(i => i.CardImageUrl));
                    break;
                case (CarouselSection carousel, CarouselSectionUpdateDto carouselDto) when carouselDto.Items != null:
                    await DeleteRemovedUrlsAsync(carousel.Items.Select(i => i.ImageUrl), carouselDto.Items.Select(i => i.ImageUrl));
                    break;
                case (TestimonialSection testimonial, TestimonialSectionUpdateDto testimonialDto) when testimonialDto.Items != null:
                    await DeleteRemovedUrlsAsync(testimonial.Items.Select(i => i.ImageUrl), testimonialDto.Items.Select(i => i.ImageUrl));
                    break;
            }
        }

        private static string NormalizeBlockLayoutMode(string? value) => value switch
        {
            "grid" => "grid",
            "split" => "split",
            "freeform" => "freeform",
            _ => "stack"
        };

        private static string NormalizeBlockGap(string? value) => value switch
        {
            "none" => "none",
            "small" => "small",
            "large" => "large",
            _ => "medium"
        };

        private static string NormalizeBackgroundImageFit(string? value) => value switch
        {
            "contain" => "contain",
            _ => "cover"
        };

        private static string NormalizeBackgroundImagePosition(string? value) => value switch
        {
            "top" => "top",
            "bottom" => "bottom",
            "left" => "left",
            "right" => "right",
            _ => "center"
        };

        private async Task DeleteRemovedUrlsAsync(IEnumerable<string?> oldUrls, IEnumerable<string?> newUrls)
        {
            var newSet = new HashSet<string>(
                newUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var oldUrl in oldUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!newSet.Contains(oldUrl!))
                    await _assetCleanup.DeleteIfUnusedAsync(oldUrl, null);
            }
        }
        private static string? EmptyToNull(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static string? CleanBackgroundVideoUrl(string value)
        {
            var cleaned = EmptyToNull(value);
            if (cleaned is null) return null;

            if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
                return cleaned.StartsWith("/", StringComparison.Ordinal) &&
                       !cleaned.StartsWith("//", StringComparison.Ordinal) &&
                       HasAllowedVideoExtension(cleaned)
                    ? cleaned
                    : null;

            if (uri.Scheme is not ("http" or "https")) return null;
            return HasAllowedVideoExtension(uri.AbsolutePath) ? cleaned : null;
        }

        private static bool HasAllowedVideoExtension(string value)
        {
            var path = value.Split(new[] { '?', '#' }, 2)[0];
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp4" or ".webm" or ".mov";
        }

        public async Task<bool> ReorderAsync(string pageId, List<string> orderedIds)
        {
            var page = await _context.PagesDraft.Find(p => p.Id == pageId).FirstOrDefaultAsync();
            if (page is null) return false;

            var writes = orderedIds.Select((id, i) =>
                new UpdateOneModel<Section>(
                    Builders<Section>.Filter.Where(s => s.PageStableId == page.StableId && s.Id == id),
                    Builders<Section>.Update.Set(s => s.Order, i).Inc(s => s.Version, 1))
            ).Cast<WriteModel<Section>>().ToList();

            if (writes.Count == 0) return true;
            await _context.SectionsDraft.BulkWriteAsync(writes);
            return true;
        }

        // -----------------------------------------------------------
        // PUBLIC USER SITE RENDER METHODS (PUBLISHED EXCLUSIVE)
        // -----------------------------------------------------------

        public async Task<List<Section>> GetPublicSectionsByPageAsync(string pageStableId) =>
            await _context.SectionsPublished
                .Find(s => s.PageStableId == pageStableId && s.Visible == true)
                .SortBy(s => s.Order)
                .ToListAsync();

        // -----------------------------------------------------------
        // INNER STRATEGY UTILITIES
        // -----------------------------------------------------------

        private static string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            return _sanitizer.Sanitize(html);
        }

        private static SectionButton MapButton(SectionButtonDto dto) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = dto.Label,
            Action = dto.Action,
            Href = CleanUrl(dto.Href),
            FormDefinitionId = dto.FormDefinitionId,
            Style = dto.Style,
            Visible = dto.Visible,
            Order = dto.Order
        };

        private static string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)
                    || url.StartsWith("#", StringComparison.Ordinal)
                    ? url
                    : null;
            return uri.Scheme is "http" or "https" or "mailto" or "tel" ? url : null;
        }

        private static List<ColumnSlot> ReconcileColumnSlots(List<ColumnSlot> existing, int requestedCount)
        {
            var count = Math.Clamp(requestedCount, 1, 4);
            var slots = existing
                .OrderBy(slot => slot.Order)
                .Take(count)
                .Select((slot, i) => new ColumnSlot
                {
                    Id = string.IsNullOrWhiteSpace(slot.Id)
                        ? ObjectId.GenerateNewId().ToString()
                        : slot.Id,
                    Order = i
                })
                .ToList();

            while (slots.Count < count)
            {
                slots.Add(new ColumnSlot
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Order = slots.Count
                });
            }

            return slots;
        }

        private static ShowcaseItemOverride MapShowcaseItemOverride(ShowcaseItemOverrideDto item) => new()
        {
            ChildPageId = item.ChildPageId,
            CardTitle = item.CardTitle ?? new(),
            CardContent = item.CardContent ?? new(),
            CardBackgroundType = string.IsNullOrWhiteSpace(item.CardBackgroundType) ? "color" : item.CardBackgroundType!,
            CardBackgroundColor = string.IsNullOrWhiteSpace(item.CardBackgroundColor) ? "#ffffff" : item.CardBackgroundColor!,
            CardImageUrl = item.CardImageUrl
        };

        private static int NormalizeShowcaseLimit(int value) =>
            Math.Clamp(value, 0, 200);

        private static Dictionary<string, string> NormalizeButtonLabel(Dictionary<string, string>? value)
        {
            if (value is null || !value.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                return new Dictionary<string, string> { ["en"] = "Learn More" };

            return new Dictionary<string, string>(value);
        }

        private static string NormalizeActionButtonPosition(string? value) => value switch
        {
            "top-right" => "top-right",
            "bottom-left" => "bottom-left",
            "bottom-right" => "bottom-right",
            _ => "bottom-center"
        };

        private static List<string> NormalizeContentTypes(IEnumerable<string>? values)
        {
            var normalized = (values ?? Enumerable.Empty<string>())
                .Select(v => v.Trim().ToLowerInvariant())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalized;
        }

        private static string NormalizeLibraryLayout(string? value) => value switch
        {
            "card" => "card",
            "grid" => "grid",
            "rows" => "rows",
            "lists" => "lists",
            "featured-grid" => "card",
            "card-grid" => "card",
            "compact-list" => "lists",
            "resource-grid" => "grid",
            "resource-list" => "lists",
            _ => "card"
        };

        private static string NormalizeCarouselLayout(string? value) => value switch
        {
            "case-metrics" => "case-metrics",
            _ => "cards"
        };

        private static string NormalizeLibrarySort(string? value) => value switch
        {
            "oldest" => "oldest",
            "title" => "title",
            _ => "newest"
        };

        private static string NormalizeButtonStyle(string? value) => value switch
        {
            "outline" => "outline",
            "ghost" => "ghost",
            _ => "filled"
        };

        private static CarouselItem MapCarouselItem(CarouselItemDto item, int i) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Tag = item.Tag ?? new(),
            Title = item.Title ?? new(),
            Description = item.Description ?? new(),
            ImageUrl = item.ImageUrl,
            LinkHref = CleanUrl(item.LinkHref),
            Metrics = (item.Metrics ?? new()).Select(MapCarouselMetric).ToList(),
            Visible = item.Visible,
            Order = i
        };

        private static CarouselMetric MapCarouselMetric(CarouselMetricDto metric, int i) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Value = metric.Value ?? new(),
            Label = metric.Label ?? new(),
            Tone = NormalizeMetricTone(metric.Tone),
            Order = i
        };

        private static string NormalizeMetricTone(string? tone) => tone switch
        {
            "negative" => "negative",
            "neutral" => "neutral",
            _ => "positive"
        };

        private static TestimonialItem MapTestimonialItem(TestimonialItemDto item, int i) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Icon = item.Icon,
            Title = item.Title ?? new(),
            Description = item.Description ?? new(),
            ImageUrl = item.ImageUrl,
            Visible = item.Visible,
            Order = i
        };
    }
}




