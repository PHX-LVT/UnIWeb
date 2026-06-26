using FullProject.Models;
using MongoDB.Bson;

namespace FullProject.Utils
{
    [Obsolete("Use PageGraphCloneService with an explicit CloneProfile.")]
    public static class CloneUtility
    {
        // -----------------------------------------------------------
        // PAGE
        // -----------------------------------------------------------

        public static Page ClonePage(Page source, DateTime? publishedAt = null) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            StableId = source.StableId,       // preserve logical identity
            SourceId = source.Id,             // track origin document
            Version = source.Version + 1,
            PublishedAt = publishedAt,
            Name = new Dictionary<string, string>(source.Name),
            Slug = source.Slug,
            FullSlug = source.FullSlug,
            ParentPageId = source.ParentPageId,
            ParentSlug = source.ParentSlug,
            Access = source.Access,
            Visible = source.Visible,
            Order = source.Order,
            Status = publishedAt.HasValue ? PageStatus.Published : source.Status,
            Seo = new PageSeo
            {
                MetaTitle = new Dictionary<string, string>(source.Seo.MetaTitle),
                MetaDescription = new Dictionary<string, string>(source.Seo.MetaDescription)
            },
            Card = source.Card != null ? new PageCard
            {
                CardTitle = new Dictionary<string, string>(source.Card.CardTitle),
                CardContent = new Dictionary<string, string>(source.Card.CardContent),
                CardBackgroundType = source.Card.CardBackgroundType,
                CardBackgroundColor = source.Card.CardBackgroundColor,
                CardImageUrl = source.Card.CardImageUrl,
                IsCustomized = source.Card.IsCustomized
            } : null,
            CreatedAt = source.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        // -----------------------------------------------------------
        // SECTION
        // -----------------------------------------------------------

        public static Section CloneSection(Section source, DateTime? publishedAt = null)
        {
            Section clone = source switch
            {
                HeroSection h => new HeroSection
                {
                    Layout = h.Layout,
                    Eyebrow = new Dictionary<string, string>(h.Eyebrow),
                    Heading = new Dictionary<string, string>(h.Heading),
                    Subheading = new Dictionary<string, string>(h.Subheading),
                    HeadingSize = h.HeadingSize,
                    ContentAlignment = h.ContentAlignment,
                    ImageUrl = h.ImageUrl,
                    Buttons = h.Buttons.Select(CloneSectionButton).ToList()
                },
                CtaSection c => new CtaSection
                {
                    Layout = c.Layout,
                    Heading = new Dictionary<string, string>(c.Heading),
                    Subtext = new Dictionary<string, string>(c.Subtext),
                    Button = c.Button != null ? CloneSectionButton(c.Button) : null,
                    Buttons = c.Buttons.Select(CloneSectionButton).ToList()
                },
                ListSection l => new ListSection
                {
                    Layout = l.Layout,
                    Columns = l.Columns,
                    SectionTitle = new Dictionary<string, string>(l.SectionTitle),
                    ShowIcon = l.ShowIcon,
                    Items = l.Items.Select(CloneListItem).ToList()
                },
                DynamicSection dynamicSection => new DynamicSection
                {
                    ScopeSectionIds = dynamicSection.ScopeSectionIds.ToList(),
                    SearchBy = dynamicSection.SearchBy,
                    Display = dynamicSection.Display,
                    Placeholder = new Dictionary<string, string>(dynamicSection.Placeholder),
                    DefaultSort = dynamicSection.DefaultSort,
                    ShowSearchBar = dynamicSection.ShowSearchBar
                },
                HtmlSection html => new HtmlSection
                {
                    Content = html.Content.ToDictionary(kv => kv.Key, kv => kv.Value)
                },
                ColumnsSection col => new ColumnsSection
                {
                    ColumnCount = col.ColumnCount,
                    ColumnRatio = col.ColumnRatio,
                    Gap = col.Gap,
                    StackOnMobile = col.StackOnMobile,
                    // Keep same slot IDs � published blocks reference them by ColumnSlotId
                    Columns = col.Columns.Select(slot => new ColumnSlot
                    {
                        Id = slot.Id,
                        Order = slot.Order,
                        Blocks = new List<Block>() // blocks live in separate collection
                    }).ToList()
                },
                ShowcaseSection ld => new ShowcaseSection
                {
                    SourcePageId = ld.SourcePageId,
                    Layout = ld.Layout,
                    Columns = ld.Columns,
                    Limit = ld.Limit,
                    Eyebrow = new Dictionary<string, string>(ld.Eyebrow),
                    SectionTitle = new Dictionary<string, string>(ld.SectionTitle),
                    ShowImage = ld.ShowImage,
                    ShowContent = ld.ShowContent,
                    ShowItemButton = ld.ShowItemButton,
                    ButtonLabelText = new Dictionary<string, string>(ld.ButtonLabelText),
                    ActionButton = ld.ActionButton != null ? CloneSectionButton(ld.ActionButton) : null,
                    ActionButtonPosition = ld.ActionButtonPosition,
                    ShowSearchBar = ld.ShowSearchBar,
                    SearchPlaceholder = new Dictionary<string, string>(ld.SearchPlaceholder),
                    ItemOverrides = ld.ItemOverrides.Select(CloneShowcaseItemOverride).ToList()
                },
                LibrarySection library => new LibrarySection
                {
                    ContentTypes = library.ContentTypes.ToList(),
                    Layout = library.Layout,
                    Columns = library.Columns,
                    Rows = library.Rows,
                    Limit = library.Limit,
                    EnableTabs = library.EnableTabs,
                    EnablePagination = library.EnablePagination,
                    Eyebrow = new Dictionary<string, string>(library.Eyebrow),
                    SectionTitle = new Dictionary<string, string>(library.SectionTitle),
                    Subheading = new Dictionary<string, string>(library.Subheading),
                    ShowImage = library.ShowImage,
                    ShowSummary = library.ShowSummary,
                    ShowButton = library.ShowButton,
                    ShowTime = library.ShowTime,
                    ButtonLabel = new Dictionary<string, string>(library.ButtonLabel),
                    ButtonStyle = library.ButtonStyle,
                    ShowSearchBar = library.ShowSearchBar,
                    ShowFilters = library.ShowFilters,
                    SearchPlaceholder = new Dictionary<string, string>(library.SearchPlaceholder),
                    SortMode = library.SortMode
                },
                StatsSection st => new StatsSection
                {
                    SectionTitle = new Dictionary<string, string>(st.SectionTitle),
                    Columns = st.Columns,
                    DurationMs = st.DurationMs,
                    Items = st.Items.Select(CloneStatItem).ToList()
                },
                CarouselSection ca => new CarouselSection
                {
                    SectionTitle = new Dictionary<string, string>(ca.SectionTitle),
                    Layout = ca.Layout,
                    Columns = ca.Columns,
                    Autoplay = ca.Autoplay,
                    ShowDots = ca.ShowDots,
                    ShowArrows = ca.ShowArrows,
                    Items = ca.Items.Select(CloneCarouselItem).ToList()
                },
                NetworkMapSection map => new NetworkMapSection
                {
                    SectionTitle = new Dictionary<string, string>(map.SectionTitle),
                    CenterLat = map.CenterLat,
                    CenterLng = map.CenterLng,
                    DefaultZoom = map.DefaultZoom,
                    Pins = map.Pins.Select(CloneNetworkMapPin).ToList()
                },
                TestimonialSection testimonial => new TestimonialSection
                {
                    Eyebrow = new Dictionary<string, string>(testimonial.Eyebrow),
                    SectionTitle = new Dictionary<string, string>(testimonial.SectionTitle),
                    Subheading = new Dictionary<string, string>(testimonial.Subheading),
                    Layout = testimonial.Layout,
                    HeaderAlignment = testimonial.HeaderAlignment,
                    Columns = testimonial.Columns,
                    Items = testimonial.Items.Select(CloneTestimonialItem).ToList()
                },
                CanvasSection canvas => new CanvasSection
                {
                    AdminLabel = new Dictionary<string, string>(canvas.AdminLabel)
                },
                _ => throw new ArgumentException($"Unknown section type: {source.GetType().Name}")
            };

            clone.Id = ObjectId.GenerateNewId().ToString();
            clone.StableId = source.StableId;
            clone.SourceId = source.Id;
            clone.Version = source.Version + 1;
            clone.PublishedAt = publishedAt;
            clone.PageStableId = source.PageStableId;
            clone.Visible = source.Visible;
            clone.Order = source.Order;
            clone.Style = CloneSectionStyle(source.Style);
            clone.CreatedAt = source.CreatedAt;
            clone.UpdatedAt = DateTime.UtcNow;

            return clone;
        }

        // -----------------------------------------------------------
        // BLOCK
        // -----------------------------------------------------------

        public static Block CloneBlock(Block source, DateTime? publishedAt = null)
        {
            Block clone = source switch
            {
                TextBlock t => new TextBlock
                {
                    Title = new Dictionary<string, string>(t.Title),
                    Content = new Dictionary<string, string>(t.Content)
                },
                ImageBlock img => new ImageBlock
                {
                    ImageUrl = img.ImageUrl,
                    AltText = new Dictionary<string, string>(img.AltText)
                },
                VideoBlock v => new VideoBlock
                {
                    EmbedUrl = v.EmbedUrl,
                    Title = new Dictionary<string, string>(v.Title)
                },
                FileBlock f => new FileBlock
                {
                    FileUrl = f.FileUrl,
                    Filename = f.Filename,
                    FileType = f.FileType
                },
                MapBlock m => new MapBlock
                {
                    CenterLat = m.CenterLat,
                    CenterLng = m.CenterLng,
                    DefaultZoom = m.DefaultZoom,
                    Pins = m.Pins.Select(p => new MapPin
                    {
                        Id = p.Id,
                        Label = p.Label,
                        Lat = p.Lat,
                        Lng = p.Lng,
                        Href = p.Href
                    }).ToList()
                },
                FormBlock form => new FormBlock
                {
                    FormDefinitionId = form.FormDefinitionId,
                    SubmitButtonLabel = new Dictionary<string, string>(form.SubmitButtonLabel),
                    Fields = form.Fields.Select(f => new FormField
                    {
                        Name = f.Name,
                        Type = f.Type,
                        Label = new Dictionary<string, string>(f.Label),
                        Required = f.Required,
                        Options = f.Options?.ToList(),
                        Order = f.Order
                    }).ToList()
                },
                CardBlock card => new CardBlock
                {
                    Icon = card.Icon,
                    Title = new Dictionary<string, string>(card.Title),
                    Description = new Dictionary<string, string>(card.Description),
                    ImageUrl = card.ImageUrl,
                    ButtonLabel = new Dictionary<string, string>(card.ButtonLabel),
                    Href = card.Href,
                    Action = card.Action,
                    FormDefinitionId = card.FormDefinitionId
                },
                ButtonBlock button => new ButtonBlock
                {
                    Label = new Dictionary<string, string>(button.Label),
                    Href = button.Href,
                    Action = button.Action,
                    FormDefinitionId = button.FormDefinitionId,
                    Style = button.Style
                },
                MetricBlock metric => new MetricBlock
                {
                    Icon = metric.Icon,
                    Label = new Dictionary<string, string>(metric.Label),
                    Value = metric.Value,
                    Prefix = metric.Prefix,
                    Suffix = metric.Suffix,
                    Description = new Dictionary<string, string>(metric.Description)
                },
                BulletListBlock list => new BulletListBlock
                {
                    Title = new Dictionary<string, string>(list.Title),
                    Items = list.Items.Select(CloneBulletListItem).ToList()
                },
                StepBlock step => new StepBlock
                {
                    Icon = step.Icon,
                    StepLabel = new Dictionary<string, string>(step.StepLabel),
                    Title = new Dictionary<string, string>(step.Title),
                    Description = new Dictionary<string, string>(step.Description)
                },
                IconBlock icon => new IconBlock
                {
                    Icon = icon.Icon,
                    Label = new Dictionary<string, string>(icon.Label),
                    Description = new Dictionary<string, string>(icon.Description)
                },
                ContainerBlock container => new ContainerBlock
                {
                    Title = new Dictionary<string, string>(container.Title),
                    LayoutMode = container.LayoutMode,
                    Columns = container.Columns,
                    Gap = container.Gap,
                    OrbitRadius = container.OrbitRadius,
                    OrbitStartAngle = container.OrbitStartAngle,
                    SemicircleRadius = container.SemicircleRadius,
                    SemicircleStartAngle = container.SemicircleStartAngle,
                    SemicircleEndAngle = container.SemicircleEndAngle
                },
                _ => throw new ArgumentException($"Unknown block type: {source.GetType().Name}")
            };

            clone.Id = ObjectId.GenerateNewId().ToString();
            clone.StableId = source.StableId;
            clone.SourceId = source.Id;
            clone.Version = source.Version + 1;
            clone.PublishedAt = publishedAt;
            clone.PageStableId = source.PageStableId;
            clone.SectionStableId = source.SectionStableId;
            clone.Visible = source.Visible;
            clone.Order = source.Order;
            clone.ColumnSlotId = source.ColumnSlotId;
            clone.BlockZone = source.BlockZone;
            clone.PositionMode = source.PositionMode;
            clone.ParentBlockId = source.ParentBlockId;
            clone.Layout = CloneBlockLayout(source.Layout);
            clone.Buttons = source.Buttons.Select(b => new BlockButton
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Label = new Dictionary<string, string>(b.Label),
                Action = b.Action,
                Href = b.Href,
                FormDefinitionId = b.FormDefinitionId,
                Visible = b.Visible,
                Order = b.Order,
                ColumnSlotId = b.ColumnSlotId
            }).ToList();
            clone.CreatedAt = source.CreatedAt;
            clone.UpdatedAt = DateTime.UtcNow;

            return clone;
        }

        // -----------------------------------------------------------
        // NESTED HELPERS
        // -----------------------------------------------------------

        private static TestimonialItem CloneTestimonialItem(TestimonialItem i) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Icon = i.Icon,
            Title = new Dictionary<string, string>(i.Title),
            Description = new Dictionary<string, string>(i.Description),
            ImageUrl = i.ImageUrl,
            Visible = i.Visible,
            Order = i.Order
        };

        private static SectionStyle CloneSectionStyle(SectionStyle s) => new()
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
            BlockLayoutMode = s.BlockLayoutMode,
            BlockGridColumns = s.BlockGridColumns,
            BlockGap = s.BlockGap
        };

        private static BlockLayout CloneBlockLayout(BlockLayout? layout)
        {
            layout ??= new BlockLayout();

            return new BlockLayout
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

        private static BulletListItem CloneBulletListItem(BulletListItem item) => new()
        {
            Id = item.Id,
            Icon = item.Icon,
            Text = new Dictionary<string, string>(item.Text),
            Visible = item.Visible,
            Order = item.Order
        };

        private static SectionButton CloneSectionButton(SectionButton b) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = new Dictionary<string, string>(b.Label),
            Action = b.Action,
            Href = b.Href,
            FormDefinitionId = b.FormDefinitionId,
            Style = b.Style,
            Visible = b.Visible,
            Order = b.Order
        };

        private static ShowcaseItemOverride CloneShowcaseItemOverride(ShowcaseItemOverride item) => new()
        {
            ChildPageId = item.ChildPageId,
            CardTitle = new Dictionary<string, string>(item.CardTitle),
            CardContent = new Dictionary<string, string>(item.CardContent),
            CardBackgroundType = item.CardBackgroundType,
            CardBackgroundColor = item.CardBackgroundColor,
            CardImageUrl = item.CardImageUrl
        };
        private static ListItem CloneListItem(ListItem item) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Icon = item.Icon,
            Title = new Dictionary<string, string>(item.Title),
            Description = new Dictionary<string, string>(item.Description),
            ImageUrl = item.ImageUrl,
            LinkHref = item.LinkHref,
            Visible = item.Visible,
            Order = item.Order
        };

        private static StatItem CloneStatItem(StatItem item) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = new Dictionary<string, string>(item.Label),
            Value = item.Value,
            Prefix = item.Prefix,
            Suffix = item.Suffix,
            Visible = item.Visible,
            Order = item.Order
        };

        private static CarouselItem CloneCarouselItem(CarouselItem item) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Tag = new Dictionary<string, string>(item.Tag),
            Title = new Dictionary<string, string>(item.Title),
            Description = new Dictionary<string, string>(item.Description),
            ImageUrl = item.ImageUrl,
            LinkHref = item.LinkHref,
            Metrics = item.Metrics.Select(CloneCarouselMetric).ToList(),
            Visible = item.Visible,
            Order = item.Order
        };

        private static CarouselMetric CloneCarouselMetric(CarouselMetric metric) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Value = new Dictionary<string, string>(metric.Value),
            Label = new Dictionary<string, string>(metric.Label),
            Tone = metric.Tone,
            Order = metric.Order
        };

        private static NetworkMapPin CloneNetworkMapPin(NetworkMapPin pin) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Label = pin.Label,
            Lat = pin.Lat,
            Lng = pin.Lng,
            Href = pin.Href,
            Visible = pin.Visible,
            Order = pin.Order
        };
    }
}




