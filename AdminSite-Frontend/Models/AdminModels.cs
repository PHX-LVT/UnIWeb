using System.Text.Json.Serialization;


namespace AdminSite.Models 

{ 

    // Stored in localStorage after login
    public class AdminSession
    {
        public string AdminId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AdminRole Role { get; set; } = AdminRole.Viewer;
        public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
        public List<string> Permissions { get; set; } = new();
        public string Token { get; set; } = string.Empty;
    }

 
    // -- Branding ----------------------------------------------
    public class BrandingModel
    {
        public string? CompanyName { get; set; }
        public string? LogoUrl { get; set; }
        public string? Href { get; set; }
    }

    // -- Theme -------------------------------------------------
    public class ThemeModel
    {
        public string? FontBody { get; set; }
        public string? FontHeading { get; set; }
        public string? TextSizeBase { get; set; }
        public string? TextSizeEyebrow { get; set; }
        public string? TextSizeHeading { get; set; }
        public string? TextSizeSubheading { get; set; }
        public string? TextSizeBody { get; set; }
        public string? TextSizeSmall { get; set; }
        public string? TextSizeItemTitle { get; set; }
        public string? ColorPrimary { get; set; }
        public string? ColorAccent { get; set; }
        public string? ColorBackground { get; set; }
        public string? ColorText { get; set; }
        public string? BorderRadius { get; set; }
        public string? ButtonSizeScale { get; set; }
        public string? ButtonTextSize { get; set; }
        public bool? AnimationsEnabled { get; set; }
        public string? AnimationSpeed { get; set; }
        public string? SpacingScale { get; set; }
    }

    // -- Global Buttons -----------------------------------------
    public class GlobalButtonModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> LabelText { get; set; } = new();
        public string? Action { get; set; }
        public string? Href { get; set; }
        public string? Position { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }
    public class GlobalButtonRequest
    {
        public Dictionary<string, string>? LabelText { get; set; }
        public string? Href { get; set; }
        public string? Action { get; set; }
        public string? Position { get; set; }
        // public string? Style { get; set; }
        public bool Visible { get; set; } = true;
    }

    // -- Footer ------------------------------------------------
    public class FooterModel
    {
        public string? CompanyName { get; set; }
        public List<FooterGroupModel> Groups { get; set; } = new();
    }

    public class FooterGroupModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
        public List<FooterLinkModel> Links { get; set; } = new();
    }

    public class FooterLinkModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? Href { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class FooterGroupRequest
    {
        public string? Label { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class FooterLinkRequest
    {
        public string? Label { get; set; }
        public string? Href { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class FooterMetaRequest
    {
        public string? CompanyName { get; set; }
    }

    // -- Social ------------------------------------------------
    public class SocialGroupModel
    {
        public bool Visible { get; set; }
        public List<SocialButtonModel> Buttons { get; set; } = new();
    }

    public class SocialButtonModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? Href { get; set; }
        public string? Icon { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class SocialButtonRequest
    {
        public string? Label { get; set; }
        public string? Href { get; set; }
        public string? Icon { get; set; }
        public bool Visible { get; set; } = true;
    }

    // -- Pages -------------------------------------------------
    public class PageModel
    {
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string? SourceId { get; set; }
        public Dictionary<string, string> Name { get; set; } = new();
        public string Slug { get; set; } = string.Empty;
        public string? FullSlug { get; set; }
        public string? ParentPageId { get; set; }
        public bool Access { get; set; } = true;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public PageStatus Status { get; set; } = PageStatus.Draft;
        public string? Template { get; set; }
        public PageSeoModel Seo { get; set; } = new();
        public PageCardModel? Card { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PageSeoModel
    {
        public Dictionary<string, string> MetaTitle { get; set; } = new();
        public Dictionary<string, string> MetaDescription { get; set; } = new();
    }

    public class PageCardModel
    {
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
        public bool IsCustomized { get; set; }
    }

    public class PageCardRequest
    {
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
    }

    public class PageRequest
    {
        public Dictionary<string, string> Name { get; set; } = new();

        public string? Slug { get; set; } = string.Empty;
        public bool Access { get; set; } = true;
        public bool Visible { get; set; } = true;
    }
    public class ChildPageRequest
    {
        public Dictionary<string, string> Name { get; set; } = new();
    }
    // -- Sections ----------------------------------------------
    public class SectionModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public SectionStyleModel Style { get; set; } = new();

        // Hero
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? Layout { get; set; }
        public string? HeadingSize { get; set; }
        public string? ContentAlignment { get; set; }
        public string? ImageUrl { get; set; }
        public List<SectionButtonModel>? Buttons { get; set; }

        // CTA
        public Dictionary<string, string>? Subtext { get; set; }
        public SectionButtonModel? Button { get; set; }

        // Gallery
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public bool? ShowCaptions { get; set; }
        public List<GalleryImageModel>? Images { get; set; }

        // Shared section heading fields
        public Dictionary<string, string>? Eyebrow { get; set; }
        public string? HeaderAlignment { get; set; }

        // List
        public Dictionary<string, string>? SectionTitle { get; set; }
        public bool? ShowIcon { get; set; }
        public List<ListItemModel>? Items { get; set; }
        public List<ColumnSlotModel>? ColumnSlots { get; set; }

        // Add new class:
        public class ColumnSlotModel
        {
            public string Id { get; set; } = string.Empty;
            public int Order { get; set; }
        }

    // Html
        [JsonPropertyName("htmlContent")]
        public Dictionary<string, string>? Content { get; set; }
    // Columns
        public int? ColumnCount { get; set; }
        public string? ColumnRatio { get; set; }
        public bool? StackOnMobile { get; set; }

        // Dynamic
        public List<string>? ScopeSectionIds { get; set; }
        public string? SearchBy { get; set; }
        public string? Display { get; set; }
        public string? Placeholder { get; set; }
        public string? DefaultSort { get; set; }
        public bool? ShowSearchBar { get; set; }

        // Showcase
        public string? SourcePageId { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public SectionButtonModel? ActionButton { get; set; }
        public string? ActionButtonPosition { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowContent { get; set; }
        public bool? ShowItemButton { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public List<ShowcaseItemOverrideModel>? ShowcaseItems { get; set; }

        // Library
        public List<string>? ContentTypes { get; set; }
        public int? Limit { get; set; }
        public int? Rows { get; set; }
        public bool? EnableTabs { get; set; }
        public bool? EnablePagination { get; set; }
        public bool? ShowSummary { get; set; }
        public bool? ShowButton { get; set; }
        public bool? ShowTime { get; set; }
        public string? ButtonStyle { get; set; }
        public bool? ShowFilters { get; set; }
        public string? SortMode { get; set; }

        public int? DurationMs { get; set; }
        public bool? Autoplay { get; set; }
        public bool? ShowDots { get; set; }
        public bool? ShowArrows { get; set; }
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<StatItemModel>? Stats { get; set; }
        public List<CarouselItemModel>? CarouselItems { get; set; }
        public List<NetworkMapPinModel>? MapPins { get; set; }
        public List<TestimonialItemModel>? Testimonials { get; set; }
        public Dictionary<string, string>? AdminLabel { get; set; }
    }

    public class SectionStyleModel
    {
        public string? BackgroundType { get; set; }
        public string? BackgroundColor { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? BackgroundVideoUrl { get; set; }
        public string? GradientFrom { get; set; }
        public string? GradientTo { get; set; }
        public string? GradientDirection { get; set; }
        public string? OverlayColor { get; set; }
        public double? OverlayOpacity { get; set; }
        public string? Height { get; set; }
        public int? CustomMinHeightPx { get; set; }
        public string? Padding { get; set; }
        public string? ContentWidth { get; set; }
        public string? TextColor { get; set; }
        public string? MobileLayout { get; set; }
        public string? BlockLayoutMode { get; set; }
        public int? BlockGridColumns { get; set; }
        public string? BlockGap { get; set; }
    }

    public class BlockLayoutModel
    {
        public string? Width { get; set; }
        public int? ColumnSpan { get; set; }
        public string? Align { get; set; }
        public string? Justify { get; set; }
        public string? Padding { get; set; }
        public string? Margin { get; set; }
        public string? BackgroundColor { get; set; }
        public string? BorderRadius { get; set; }
        public int? ZIndex { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? W { get; set; }
        public int? H { get; set; }
    }

    public class ShowcaseItemOverrideModel
    {
        public string ChildPageId { get; set; } = string.Empty;
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
    }

    public class SectionButtonModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string>? Label { get; set; }

        [JsonIgnore]
        public string LabelText
        {
            get => Label is not null && Label.TryGetValue("en", out var value) ? value : string.Empty;
            set => Label = new Dictionary<string, string> { ["en"] = value };
        }

        public string? Action { get; set; }
        public string? Href { get; set; }
        public string? Style { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class GalleryImageModel
    {
        public string Id { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? Caption { get; set; }

        [JsonIgnore]
        public string CaptionText
        {
            get => Caption is not null && Caption.TryGetValue("en", out var value) ? value : string.Empty;
            set => Caption = new Dictionary<string, string> { ["en"] = value };
        }

        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class ListItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class StatItemModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string>? Label { get; set; }
        public decimal Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class CarouselItemModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string>? Tag { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public List<CarouselMetricModel>? Metrics { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class CarouselMetricModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string>? Value { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public string? Tone { get; set; } = "positive";
        public int Order { get; set; }
    }

    public class NetworkMapPinModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class TestimonialItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    // -- Blocks ------------------------------------------------
    public class BlockModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public string? ColumnSlotId { get; set; }
        public string? BlockZone { get; set; }
        public string? ParentBlockId { get; set; }
        public BlockLayoutModel? Layout { get; set; }


        // Text
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Content { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public Dictionary<string, string>? StepLabel { get; set; }
        public string? Icon { get; set; }
        public string? Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Href { get; set; }
        public string? Style { get; set; }
        public string? LayoutMode { get; set; }
        public int? Columns { get; set; }
        public string? Gap { get; set; }

        // Image
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? AltText { get; set; }
        public List<BulletListItemModel>? BulletItems { get; set; }

        [JsonIgnore]
        public string AltTextText
        {
            get => AltText is not null && AltText.TryGetValue("en", out var value) ? value : string.Empty;
            set => AltText = new Dictionary<string, string> { ["en"] = value };
        }

        // Video
        public string? EmbedUrl { get; set; }

        // File
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        [JsonIgnore]
        public string? Filename
        {
            get => FileName;
            set => FileName = value;
        }
        public string? FileType { get; set; }

        // Map
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<MapPinModel>? Pins { get; set; }

        // Form
        public Dictionary<string, string>? FormTitle { get; set; }
        public Dictionary<string, string>? SubmitButtonLabel
        {
            get => FormTitle;
            set => FormTitle = value;
        }
        public List<FormFieldModel>? Fields { get; set; }
    }

    public class BulletListItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string>? Text { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class MapPinModel
    {
        public string Id { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Label { get; set; }
        public string? Notes { get; set; }
        public string? Href { get; set; }
        public int Order { get; set; }
    }

    public class FormFieldModel
    {
        public string Id { get; set; } = string.Empty;
       
        [JsonPropertyName("type")]
        public string? FieldType { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public bool Required { get; set; }
        public List<string>? Options { get; set; }
        public int Order { get; set; }
    }

    // -- Settings ----------------------------------------------
    public class LanguageModel
    {
        public string Slug { get; set; } = string.Empty;
        [JsonPropertyName("label")]
        public string? Name { get; set; }
        public string? NativeName { get; set; }
        public bool Active { get; set; }
        public bool AdminEnabled { get; set; } = true;
        public bool UserEnabled { get; set; } = true;
        public bool IsFallback { get; set; }
        public bool Protected { get; set; }
        public string Direction { get; set; } = "ltr";
        public int Order { get; set; }
    }

    public class SiteSettingsModel
    {
        public List<LanguageModel> Languages { get; set; } = new();
        public string DefaultLanguage { get; set; } = "en";
    }

    public class SiteSettingsUpdateModel
    {
        public List<LanguageModel> Languages { get; set; } = new();
        public string DefaultLanguage { get; set; } = "en";
    }

    public class AdminAppearanceModel
    {
        public string Preset { get; set; } = "navy-gold";
    }

    public class AdminAppearancePresetModel
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sidebar { get; set; } = string.Empty;
        public string Primary { get; set; } = string.Empty;
        public string Accent { get; set; } = string.Empty;
        public string Background { get; set; } = string.Empty;
        public string Surface { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Muted { get; set; } = string.Empty;
        public string Border { get; set; } = string.Empty;
        public string Danger { get; set; } = string.Empty;
        public string Success { get; set; } = string.Empty;
    }

    public class GlossaryTermModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Term { get; set; }
        public Dictionary<string, string>? Translations { get; set; }
    }

    public class GlossaryTermRequest
    {
        public string? Term { get; set; }
        public Dictionary<string, string> Translations { get; set; } = new();
    }

    // -- Form Submissions --------------------------------------
    public class FormSubmissionModel
    {
        public string Id { get; set; } = string.Empty;
        public string? PageId { get; set; }
        public string? SectionId { get; set; }
        public string? BlockId { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
        public DateTime SubmittedAt { get; set; }
    }

    // -- Content Management -----------------------------------
    public class ContentTypeModel
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public bool RequiresBody { get; set; } = true;
        public bool RequiresHeroImage { get; set; }
        public bool RequiresFile { get; set; }
        public bool RequiresVideoUrl { get; set; }
        public bool AllowsAttachments { get; set; } = true;
        public string ClickBehavior { get; set; } = "detail";
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ContentTypeRequest
    {
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public bool RequiresBody { get; set; } = true;
        public bool RequiresHeroImage { get; set; }
        public bool RequiresFile { get; set; }
        public bool RequiresVideoUrl { get; set; }
        public bool AllowsAttachments { get; set; } = true;
        public string ClickBehavior { get; set; } = "detail";
        public bool Visible { get; set; } = true;
        public int? Order { get; set; }
    }

    public class ContentAttachmentModel
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public class ContentBodyItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public Dictionary<string, string> Content { get; set; } = new();
        public Dictionary<string, string> Caption { get; set; } = new();
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? Style { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class ContentItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string ContentTypeKey { get; set; } = "article";
        public string Slug { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public Dictionary<string, string> BodyHtml { get; set; } = new();
        public List<ContentBodyItemModel> BodyItems { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ContentAttachmentModel> Attachments { get; set; } = new();
        public string Status { get; set; } = "Draft";
        public bool Visible { get; set; } = true;
        public string AuthorId { get; set; } = string.Empty;
        public string? UpdatedById { get; set; }
        public string? PublishedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class ContentItemRequest
    {
        public string ContentTypeKey { get; set; } = "article";
        public string? Slug { get; set; }
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public Dictionary<string, string> BodyHtml { get; set; } = new();
        public List<ContentBodyItemModel> BodyItems { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ContentAttachmentModel> Attachments { get; set; } = new();
        public bool Visible { get; set; } = true;
    }

    public class ContentStatusRequest
    {
        public string Status { get; set; } = "Submitted";
        public string? Message { get; set; }
    }

    public class ContentAuditLogModel
    {
        public string Id { get; set; } = string.Empty;
        public string ContentStableId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CanvasSectionPresetModel
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public int BlockCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CanvasSectionPresetCreateRequest
    {
        public string PageId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
    }

    public class CanvasSectionPresetApplyRequest
    {
        public string PageId { get; set; } = string.Empty;
    }
}



namespace AdminSite.Models
{
    public class AssetUploadModel
    {
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}


