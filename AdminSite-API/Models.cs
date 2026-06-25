using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json.Serialization;
using Contracts.Forms;

namespace FullProject.Models
{
    // ----------------------------------------------------------------
    // BRANDING
    // ----------------------------------------------------------------

    public class Branding
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string Href { get; set; } = "/";

    }

    // ----------------------------------------------------------------
    // THEME
    // ----------------------------------------------------------------

    public class SiteTheme
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Typography
        public string FontBody { get; set; } = "Inter";
        public string FontHeading { get; set; } = "Inter";
        public string TextSizeBase { get; set; } = "16px";     // 14px | 16px | 18px | 20px
        public string TextSizeEyebrow { get; set; } = "13px";
        public string TextSizeHeading { get; set; } = "40px";
        public string TextSizeSubheading { get; set; } = "17px";
        public string TextSizeBody { get; set; } = "16px";
        public string TextSizeSmall { get; set; } = "13px";
        public string TextSizeItemTitle { get; set; } = "20px";

        // Colors
        public string ColorPrimary { get; set; } = "#001a33";
        public string ColorAccent { get; set; } = "#e5c076";
        public string ColorBackground { get; set; } = "#ffffff";
        public string ColorText { get; set; } = "#111827";

        // Shape
        public string BorderRadius { get; set; } = "10px";
        public string ButtonSizeScale { get; set; } = "1";
        public string ButtonTextSize { get; set; } = "15px";

        // Motion
        public bool AnimationsEnabled { get; set; } = true;
        public string AnimationSpeed { get; set; } = "normal"; // subtle | normal | expressive

        // Spacing
        public string SpacingScale { get; set; } = "1";
    }

    // ----------------------------------------------------------------
    // GLOBAL BUTTONS
    // ----------------------------------------------------------------

    public enum GlobalButtonAction { OpenModal, LinkToPage, ExternalUrl }
    public enum GlobalButtonPosition { HeaderLeft, HeaderRight, Floating }

    public class GlobalButton
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> LabelText { get; set; } = new();
        public GlobalButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public GlobalButtonPosition Position { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    // ----------------------------------------------------------------
    // FOOTER - renamed SubFooter -> FooterLink
    // ----------------------------------------------------------------

    public class FooterLink
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class FooterGroup
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
        public List<FooterLink> Links { get; set; } = new();
    }

    public class Footer
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public List<FooterGroup> Groups { get; set; } = new();
    }

    // ----------------------------------------------------------------
    // SOCIAL BUTTONS
    // ----------------------------------------------------------------

    public class SocialButton
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class SocialButtonGroup
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public bool GroupVisible { get; set; } = true;
        public List<SocialButton> Buttons { get; set; } = new();
    }

    // ----------------------------------------------------------------
    // AUTH
    // ----------------------------------------------------------------

    [BsonIgnoreExtraElements]
    public class AdminUser
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = "Admin";
        public string PasswordHash { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)]
        public AdminRole Role { get; set; } = AdminRole.AdminAdmin;
        [BsonRepresentation(BsonType.String)]
        public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
        public List<string> Permissions { get; set; } = new();
        public int TokenVersion { get; set; } = 1;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIp { get; set; }
        public DateTime? DisabledAt { get; set; }
        public string? DisabledById { get; set; }
        public string? CreatedById { get; set; }
        public string? UpdatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    public class AdminSessionRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string AdminId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TokenId { get; set; } = string.Empty;
        public int TokenVersion { get; set; } = 1;
        public DateTime LoginAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string BrowserName { get; set; } = "Unknown";
        public string OperatingSystem { get; set; } = "Unknown";
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedById { get; set; }
        [BsonRepresentation(BsonType.String)]
        public AdminSessionRevokeReason? RevokeReason { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AdminLoginActivityRecord
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string? AdminId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string BrowserName { get; set; } = "Unknown";
        public string OperatingSystem { get; set; } = "Unknown";
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    public class AdminAuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.String)]
        public AdminAuditArea Area { get; set; } = AdminAuditArea.Auth;
        public string Action { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string ActorEmail { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? TargetEmail { get; set; }
        public string Message { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ----------------------------------------------------------------
    // PAGES
    // ----------------------------------------------------------------


    public class PageSeo
    {
        public Dictionary<string, string> MetaTitle { get; set; } = new();
        public Dictionary<string, string> MetaDescription { get; set; } = new();
    }
    [BsonIgnoreExtraElements]
    public class PageCard
    {
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string CardBackgroundType { get; set; } = "color";
        public string CardBackgroundColor { get; set; } = "#ffffff";
        public string? CardImageUrl { get; set; }
        public bool IsCustomized { get; set; } = false;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ShowcaseItemOverride
    {
        public string ChildPageId { get; set; } = string.Empty;
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string CardBackgroundType { get; set; } = "color";
        public string CardBackgroundColor { get; set; } = "#ffffff";
        public string? CardImageUrl { get; set; }
    }

        [BsonIgnoreExtraElements]
        public class Page
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string Id { get; set; } = string.Empty;


            // ---- METADATA TRACKING (for phase 3) ----
            public string StableId { get; set; } = Guid.NewGuid().ToString();
            public string? SourceId { get; set; }
            public int Version { get; set; } = 1;
            public DateTime? PublishedAt { get; set; }
            //----------------------------------------------------------------
            public Dictionary<string, string> Name { get; set; } = new();
            public string Slug { get; set; } = string.Empty;
            public bool Access { get; set; } = true;
            public bool Visible { get; set; } = true;
            public int Order { get; set; } = 0;
            public PageStatus Status { get; set; } = PageStatus.Draft;
            public PageSeo Seo { get; set; } = new();
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
           //----------------------------------------------------------------
            public string? ParentPageId { get; set; }
            public string? ParentSlug { get; set; }
            public string? FullSlug { get; set; }
            public PageCard? Card { get; set; }

        }

    // ----------------------------------------------------------------
    // PAGE-LEVEL BUTTONS
    // ----------------------------------------------------------------

    public enum PageButtonAction { LinkToPage, OpenForm, DownloadFile, ExternalUrl }
    public enum PageButtonPosition { Top, Bottom, Floating }

    public class PageButton
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public PageButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public PageButtonPosition Position { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    // ----------------------------------------------------------------
    // SECTIONS - Page -> Section -> Block hierarchy
    // ----------------------------------------------------------------

    [BsonIgnoreExtraElements]
    public class SectionStyle
    {
        public string BackgroundType { get; set; } = "color";       // color | image | gradient | video
        public string BackgroundColor { get; set; } = "#ffffff";
        public string? BackgroundImageUrl { get; set; }
        public string? BackgroundVideoUrl { get; set; }
        public string? GradientFrom { get; set; }
        public string? GradientTo { get; set; }
        public string GradientDirection { get; set; } = "top";      // top | left | diagonal
        public string? OverlayColor { get; set; }
        public double OverlayOpacity { get; set; } = 0;
        public string Height { get; set; } = "auto";                // auto | half | full
        public int? CustomMinHeightPx { get; set; }
        public string Padding { get; set; } = "medium";             // none | small | medium | large | xl
        public string ContentWidth { get; set; } = "normal";        // narrow | normal | full
        public string TextColor { get; set; } = "dark";             // dark | light
        public string MobileLayout { get; set; } = "stack";         // stack | scroll | hide
        public string BlockLayoutMode { get; set; } = "stack";      // stack | grid | split | freeform
        public int BlockGridColumns { get; set; } = 12;
        public string BlockGap { get; set; } = "medium";            // none | small | medium | large
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class BlockLayout
    {
        public string Width { get; set; } = "auto";                 // auto | full | half | third | custom
        public int ColumnSpan { get; set; } = 12;
        public string Align { get; set; } = "stretch";              // stretch | start | center | end
        public string Justify { get; set; } = "start";              // start | center | end
        public string Padding { get; set; } = "none";               // none | small | medium | large
        public string Margin { get; set; } = "none";                // none | small | medium | large
        public string? BackgroundColor { get; set; }
        public string BorderRadius { get; set; } = "none";          // none | small | medium | large
        public int ZIndex { get; set; } = 1;
        public int X { get; set; } = 0;                              // freeform grid column start, 0-11
        public int Y { get; set; } = 0;                              // freeform row start
        public int W { get; set; } = 4;                              // freeform width in 12-column units
        public int H { get; set; } = 2;                              // freeform height in row units
        public double? LeftPercent { get; set; }                     // precise freeform left, 0-100
        public double? TopPx { get; set; }                           // precise freeform top in px
        public double? WidthPercent { get; set; }                    // precise freeform width, 0-100
        public double? HeightPx { get; set; }                        // precise freeform height in px
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class SectionButton
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Action { get; set; } = string.Empty;          // linkToPage | openForm | externalUrl | download
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";               // filled | outline | ghost
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    // ----------------------------------------------------------------

    [SwaggerSubType(typeof(HeroSection))]
    [SwaggerSubType(typeof(CtaSection))]
    [SwaggerSubType(typeof(ListSection))]
    [SwaggerSubType(typeof(DynamicSection))]
    [SwaggerSubType(typeof(HtmlSection))]
    [SwaggerSubType(typeof(ColumnsSection))]
    [SwaggerSubType(typeof(ShowcaseSection))]
    [SwaggerSubType(typeof(LibrarySection))]
    [SwaggerSubType(typeof(StatsSection))]
    [SwaggerSubType(typeof(CarouselSection))]
    [SwaggerSubType(typeof(NetworkMapSection))]
    [SwaggerSubType(typeof(TestimonialSection))]
    [SwaggerSubType(typeof(CanvasSection))]
    [BsonIgnoreExtraElements]
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(
        typeof(HeroSection),
        typeof(CtaSection),
        typeof(ListSection),
        typeof(DynamicSection),
        typeof(HtmlSection),
        typeof(ColumnsSection),
        typeof(ShowcaseSection),
        typeof(LibrarySection),
        typeof(StatsSection),
        typeof(CarouselSection),
        typeof(NetworkMapSection),
        typeof(TestimonialSection),
        typeof(CanvasSection))]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(HeroSection), "hero")]
    [JsonDerivedType(typeof(CtaSection), "cta")]
    [JsonDerivedType(typeof(ListSection), "list")]
    [JsonDerivedType(typeof(DynamicSection), "dynamic")]
    [JsonDerivedType(typeof(HtmlSection), "html")]
    [JsonDerivedType(typeof(ColumnsSection), "columns")]
    [JsonDerivedType(typeof(ShowcaseSection), "showcase")]
    [JsonDerivedType(typeof(LibrarySection), "library")]
    [JsonDerivedType(typeof(StatsSection), "stats")]
    [JsonDerivedType(typeof(CarouselSection), "carousel")]
    [JsonDerivedType(typeof(NetworkMapSection), "network-map")]
    [JsonDerivedType(typeof(TestimonialSection), "testimonial")]
    [JsonDerivedType(typeof(CanvasSection), "canvas")]
    public abstract class Section
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        // ----------------------------------------------------------------
        public string StableId { get; set; } = Guid.NewGuid().ToString();
        public string? SourceId { get; set; }
        public int Version { get; set; } = 1;
        public DateTime? PublishedAt { get; set; }
        public string PageStableId { get; set; } = string.Empty; // Fixed target parent alignment
        // ----------------------------------------------------------------
        public string PageId => PageStableId;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
        public SectionStyle Style { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonDiscriminator("hero")]
    public class HeroSection : Section
    {
        public string Layout { get; set; } = "centered";           // centered | split-left | split-right
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> Heading { get; set; } = new();
        public Dictionary<string, string> Subheading { get; set; } = new();
        public string HeadingSize { get; set; } = "medium";        // small | medium | large
        public string ContentAlignment { get; set; } = "center";   // left | center | right
        public string? ImageUrl { get; set; }
        public List<SectionButton> Buttons { get; set; } = new();
    }

    [BsonDiscriminator("cta")]
    public class CtaSection : Section
    {
        public string Layout { get; set; } = "stacked";            // stacked | inline | withSubtext
        public Dictionary<string, string> Heading { get; set; } = new();
        public Dictionary<string, string> Subtext { get; set; } = new();
        public SectionButton? Button { get; set; }
        public List<SectionButton> Buttons { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ListItem
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class StatItem
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public decimal Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class CarouselMetric
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Value { get; set; } = new();
        public Dictionary<string, string> Label { get; set; } = new();
        public string Tone { get; set; } = "positive";
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class CarouselItem
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Tag { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public List<CarouselMetric> Metrics { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class NetworkMapPin
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class TestimonialItem
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    [BsonDiscriminator("list")]
    public class ListSection : Section
    {
        public string Layout { get; set; } = "cards";              // cards | numbered | rows
        public int Columns { get; set; } = 3;
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public bool ShowIcon { get; set; } = true;
        public List<ListItem> Items { get; set; } = new();
    }

    [BsonDiscriminator("dynamic")]
    public class DynamicSection : Section
    {
        // Section IDs this dynamic section will search/filter through
        public List<string> ScopeSectionIds { get; set; } = new();
        public string SearchBy { get; set; } = "title";            // title | description | tags
        public string Display { get; set; } = "list";              // list | grid | cards
        public Dictionary<string, string> Placeholder { get; set; } = new();
        public string DefaultSort { get; set; } = "manual";        // manual | az | za | newest | oldest
        public bool ShowSearchBar { get; set; } = true;
    }

    [BsonDiscriminator("html")]
    public class HtmlSection : Section
    {
        // Sanitized on save - no separate endpoint needed
        public Dictionary<string, string> Content { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ColumnSlot
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        // Blocks inside this column slot - no separate column endpoints
        public List<Block> Blocks { get; set; } = new();
    }

    [BsonDiscriminator("columns")]
    public class ColumnsSection : Section
    {
        public int ColumnCount { get; set; } = 2;
        public string ColumnRatio { get; set; } = "equal";         // equal | 1-2 | 2-1 | 1-3 | 3-1
        public string Gap { get; set; } = "medium";                // none | small | medium | large
        public bool StackOnMobile { get; set; } = true;
        public List<ColumnSlot> Columns { get; set; } = new();
    }

    [BsonDiscriminator("showcase")]
    public class ShowcaseSection : Section
    {
        public string SourcePageId { get; set; } = string.Empty;
        public string Layout { get; set; } = "card-grid";
        public int Columns { get; set; } =  4;
        public int Limit { get; set; } = 0;
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public bool ShowImage { get; set; } = true;
        public bool ShowContent { get; set; } = true;
        public bool ShowItemButton { get; set; } = true;
        public Dictionary<string, string> ButtonLabelText { get; set; } = new() { ["en"] = "Learn More" };
        public SectionButton? ActionButton { get; set; }
        public string ActionButtonPosition { get; set; } = "bottom-center";
        public bool ShowSearchBar { get; set; } = false;
        public Dictionary<string, string> SearchPlaceholder { get; set; } = new();
        public List<ShowcaseItemOverride> ItemOverrides { get; set; } = new();
    }

    [BsonDiscriminator("library")]
    public class LibrarySection : Section
    {
        public List<string> ContentTypes { get; set; } = new();
        public string Layout { get; set; } = "featured-grid";
        public int Columns { get; set; } = 3;
        public int Rows { get; set; } = 3;
        public int Limit { get; set; } = 6;
        public bool EnableTabs { get; set; } = false;
        public bool EnablePagination { get; set; } = false;
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public Dictionary<string, string> Subheading { get; set; } = new();
        public bool ShowImage { get; set; } = true;
        public bool ShowSummary { get; set; } = true;
        public bool ShowButton { get; set; } = true;
        public bool ShowTime { get; set; } = true;
        public Dictionary<string, string> ButtonLabel { get; set; } = new() { ["en"] = "Read More" };
        public string ButtonStyle { get; set; } = "filled";
        public bool ShowSearchBar { get; set; } = false;
        public bool ShowFilters { get; set; } = false;
        public Dictionary<string, string> SearchPlaceholder { get; set; } = new();
        public string SortMode { get; set; } = "newest";
    }

    [BsonDiscriminator("stats")]
    public class StatsSection : Section
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public int Columns { get; set; } = 4;
        public int DurationMs { get; set; } = 1200;
        public List<StatItem> Items { get; set; } = new();
    }

    [BsonDiscriminator("carousel")]
    public class CarouselSection : Section
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public string Layout { get; set; } = "cards";
        public int Columns { get; set; } = 3;
        public bool Autoplay { get; set; }
        public bool ShowDots { get; set; } = true;
        public bool ShowArrows { get; set; } = true;
        public List<CarouselItem> Items { get; set; } = new();
    }

    [BsonDiscriminator("network-map")]
    public class NetworkMapSection : Section
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public double CenterLat { get; set; } = 15.87;
        public double CenterLng { get; set; } = 100.99;
        public int DefaultZoom { get; set; } = 4;
        public List<NetworkMapPin> Pins { get; set; } = new();
    }

    [BsonDiscriminator("testimonial")]
    public class TestimonialSection : Section
    {
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public Dictionary<string, string> Subheading { get; set; } = new();
        public string Layout { get; set; } = "cards";
        public string HeaderAlignment { get; set; } = "center";
        public int Columns { get; set; } = 4;
        public List<TestimonialItem> Items { get; set; } = new();
    }

    [BsonDiscriminator("canvas")]
    public class CanvasSection : Section
    {
        public Dictionary<string, string> AdminLabel { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    public class CanvasSectionPreset
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public SectionStyle Style { get; set; } = new();
        public List<Block> Blocks { get; set; } = new();
        public int SchemaVersion { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    // ----------------------------------------------------------------
    // BLOCKS - Page -> Section -> Block hierarchy
    // ----------------------------------------------------------------


    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class BlockButton
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public  BlockButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
        public string? ColumnSlotId { get; set; }

    }

    public class FormField
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;           // text | email | textarea | select | radio | checkbox | date | number
        public Dictionary<string, string> Label { get; set; } = new();
        public bool Required { get; set; } = false;
        public List<string>? Options { get; set; }
        public int Order { get; set; } = 0;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class MapPin
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
    }

    [SwaggerSubType(typeof(TextBlock))]
    [SwaggerSubType(typeof(ImageBlock))]
    [SwaggerSubType(typeof(VideoBlock))]
    [SwaggerSubType(typeof(FileBlock))]
    [SwaggerSubType(typeof(MapBlock))]
    [SwaggerSubType(typeof(FormBlock))]
    [SwaggerSubType(typeof(CardBlock))]
    [SwaggerSubType(typeof(ButtonBlock))]
    [SwaggerSubType(typeof(MetricBlock))]
    [SwaggerSubType(typeof(BulletListBlock))]
    [SwaggerSubType(typeof(StepBlock))]
    [SwaggerSubType(typeof(IconBlock))]
    [SwaggerSubType(typeof(ContainerBlock))]
    [BsonIgnoreExtraElements]
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(
        typeof(TextBlock),
        typeof(ImageBlock),
        typeof(VideoBlock),
        typeof(FileBlock),
        typeof(MapBlock),
        typeof(FormBlock),
        typeof(CardBlock),
        typeof(ButtonBlock),
        typeof(MetricBlock),
        typeof(BulletListBlock),
        typeof(StepBlock),
        typeof(IconBlock),
        typeof(ContainerBlock))]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(TextBlock), "text")]
    [JsonDerivedType(typeof(ImageBlock), "image")]
    [JsonDerivedType(typeof(VideoBlock), "video")]
    [JsonDerivedType(typeof(FileBlock), "file")]
    [JsonDerivedType(typeof(MapBlock), "map")]
    [JsonDerivedType(typeof(FormBlock), "form")]
    [JsonDerivedType(typeof(CardBlock), "card")]
    [JsonDerivedType(typeof(ButtonBlock), "button")]
    [JsonDerivedType(typeof(MetricBlock), "metric")]
    [JsonDerivedType(typeof(BulletListBlock), "bullet-list")]
    [JsonDerivedType(typeof(StepBlock), "step")]
    [JsonDerivedType(typeof(IconBlock), "icon")]
    [JsonDerivedType(typeof(ContainerBlock), "container")]
    public abstract class Block
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        // ----------------------------------------------------------------
        public string StableId { get; set; } = Guid.NewGuid().ToString();
        public string? SourceId { get; set; }
        public int Version { get; set; } = 1;
        public DateTime? PublishedAt { get; set; }
        public string PageStableId { get; set; } = string.Empty;    // Fixed target parent page alignment
        public string SectionStableId { get; set; } = string.Empty; // Fixed target parent section alignment
        // ----------------------------------------------------------------

        public string PageId => PageStableId;
        public string SectionId => SectionStableId;
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
        public List<BlockButton> Buttons { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? ColumnSlotId { get; set; }
        public string BlockZone { get; set; } = "default";
        public string? PositionMode { get; set; }
        [BsonIgnore]
        [JsonIgnore]
        public string ZoneId
        {
            get => BlockZone;
            set => BlockZone = string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();
        }
        public string? ParentBlockId { get; set; }
        public BlockLayout Layout { get; set; } = new();
        [BsonIgnore]
        [JsonIgnore]
        public int ZOrder
        {
            get => Layout.ZIndex;
            set => Layout.ZIndex = Math.Clamp(value, 0, 1000);
        }

    }

    [BsonDiscriminator("text")]
    public class TextBlock : Block
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Content { get; set; } = new();
    }

    [BsonDiscriminator("image")]
    public class ImageBlock : Block
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> AltText { get; set; } = new();
    }

    [BsonDiscriminator("video")]
    public class VideoBlock : Block
    {
        public string EmbedUrl { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
    }

    [BsonDiscriminator("file")]
    public class FileBlock : Block
    {
        public string? FileUrl { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }

    // Map: pins are stored as embedded array, managed via block PUT
    [BsonDiscriminator("map")]
    public class MapBlock : Block
    {
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public int DefaultZoom { get; set; } = 12;
        public List<MapPin> Pins { get; set; } = new();
    }

    // Form: fields stored as embedded array, managed via block PUT
    [BsonDiscriminator("form")]
    public class FormBlock : Block
    {
        public string? FormDefinitionId { get; set; }
        public List<FormField> Fields { get; set; } = new();
        public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class BulletListItem
    {
        [BsonElement("Id")]
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Text { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonDiscriminator("card")]
    public class CardBlock : Block
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> ButtonLabel { get; set; } = new();
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
    }

    [BsonDiscriminator("button")]
    public class ButtonBlock : Block
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
    }

    [BsonDiscriminator("metric")]
    public class MetricBlock : Block
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Value { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public Dictionary<string, string> Description { get; set; } = new();
    }

    [BsonDiscriminator("bullet-list")]
    public class BulletListBlock : Block
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public List<BulletListItem> Items { get; set; } = new();
    }

    [BsonDiscriminator("step")]
    public class StepBlock : Block
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> StepLabel { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    [BsonDiscriminator("icon")]
    public class IconBlock : Block
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    [BsonDiscriminator("container")]
    public class ContainerBlock : Block
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public string LayoutMode { get; set; } = "stack";
        public int Columns { get; set; } = 2;
        public string Gap { get; set; } = "medium";
        public int OrbitRadius { get; set; } = 180;
        public int OrbitStartAngle { get; set; } = -90;
        public int SemicircleRadius { get; set; } = 180;
        public int SemicircleStartAngle { get; set; } = 180;
        public int SemicircleEndAngle { get; set; } = 360;
    }

    // ----------------------------------------------------------------
    // FORM SUBMISSIONS
    // ----------------------------------------------------------------

    [BsonIgnoreExtraElements]
    public class FormDefinition
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Introduction { get; set; } = new();
        public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
        public FormDisplayMode DisplayMode { get; set; } = FormDisplayMode.Embedded;
        public FormLayout Layout { get; set; } = FormLayout.Stacked;
        public bool Active { get; set; } = true;
        public List<FormDefinitionField> Fields { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class FormDefinitionField
    {
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public Dictionary<string, string> Label { get; set; } = new();
        public Dictionary<string, string> Placeholder { get; set; } = new();
        public bool Required { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; } = 500;
        public List<FormDefinitionFieldOption> Options { get; set; } = new();
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class FormDefinitionFieldOption
    {
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class FormSubmission
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public string BlockId { get; set; } = string.Empty;
        public Dictionary<string, string> Data { get; set; } = new();
        public string FormId { get; set; } = string.Empty;
        public string FormKey { get; set; } = string.Empty;
        public string FormName { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
        public string SourcePage { get; set; } = string.Empty;
        public FormSubmissionStatus Status { get; set; } = FormSubmissionStatus.New;
        public List<FormSubmissionFieldSnapshot> Fields { get; set; } = new();
        public string? InternalNotes { get; set; }
        public FormSubmissionSecurity Security { get; set; } = new();
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class FormSubmissionFieldSnapshot
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Value { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class FormSubmissionSecurity
    {
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
    }

    // ----------------------------------------------------------------
    // SETTINGS
    // ----------------------------------------------------------------

    [BsonIgnoreExtraElements]
    public class ManagedResource
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "file"; // image | file | video
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Url { get; set; } = string.Empty;
        public string? StorageKey { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Source { get; set; } = "managed-upload";
        public List<string> Tags { get; set; } = new();
        public bool Active { get; set; } = true;
        public string CreatedById { get; set; } = string.Empty;
        public string? UpdatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    public enum ContentStatus
    {
        Draft,
        Submitted,
        Rejected,
        Published,
        Archived,
        Deleted
    }

    [BsonIgnoreExtraElements]
    public class ContentType
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Behavior { get; set; } = "page";
        public bool RequiresBody { get; set; } = true;
        public bool RequiresHeroImage { get; set; } = false;
        public bool RequiresFile { get; set; } = false;
        public bool RequiresVideoUrl { get; set; } = false;
        public bool AllowsAttachments { get; set; } = true;
        public string ClickBehavior { get; set; } = "detail";
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ContentAttachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        public string ResourceSource { get; set; } = "DirectUpload";
        public string? StorageKey { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ContentBodyItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = "text";
        public Dictionary<string, string> Content { get; set; } = new();
        public Dictionary<string, string> Caption { get; set; } = new();
        public string? Url { get; set; }
        public string? ResourceId { get; set; }
        public string ResourceSource { get; set; } = "DirectUpload";
        public string? StorageKey { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? Style { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class ContentGalleryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Kind { get; set; } = "image";
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? ResourceId { get; set; }
        public string ResourceSource { get; set; } = "DirectUpload";
        public string? StorageKey { get; set; }
        public Dictionary<string, string> Caption { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ContentItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = Guid.NewGuid().ToString("N");
        public string ContentTypeKey { get; set; } = "article";
        public string Slug { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public Dictionary<string, string> BodyHtml { get; set; } = new();
        public List<ContentBodyItem> BodyItems { get; set; } = new();
        public List<ContentGalleryItem> GalleryItems { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? HeroImageResourceId { get; set; }
        public string HeroImageResourceSource { get; set; } = "DirectUpload";
        public string? HeroImageStorageKey { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? ThumbnailResourceId { get; set; }
        public string ThumbnailResourceSource { get; set; } = "DirectUpload";
        public string? ThumbnailStorageKey { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoResourceId { get; set; }
        public string VideoResourceSource { get; set; } = "DirectUpload";
        public string? VideoStorageKey { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ContentAttachment> Attachments { get; set; } = new();
        public ContentStatus Status { get; set; } = ContentStatus.Draft;
        public bool Visible { get; set; } = true;
        public string AuthorId { get; set; } = string.Empty;
        public string? UpdatedById { get; set; }
        public string? PublishedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ContentAuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string ContentStableId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    [BsonIgnoreExtraElements]
    public class VisitorMetricCounter
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string MetricType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime Day { get; set; } = DateTime.UtcNow.Date;
        public long Count { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }



    [BsonIgnoreExtraElements]
    public class PageRevision
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string PageStableId { get; set; } = string.Empty;
        public int SourceVersion { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public BsonDocument Snapshot { get; set; } = new();
    }

    [BsonIgnoreExtraElements]
    public class ContentRevision
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string ContentStableId { get; set; } = string.Empty;
        public DateTime SourceUpdatedAt { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public BsonDocument Snapshot { get; set; } = new();
    }
    [BsonIgnoreExtraElements]
    public class LanguageSetting
    {
        public string Slug { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public bool Active { get; set; } = false;
        public bool AdminEnabled { get; set; } = true;
        public bool UserEnabled { get; set; } = true;
        public string Direction { get; set; } = "ltr";
        public int Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ResourceLibrarySettings
    {
        public long MaxImageBytes { get; set; } = 20L * 1024 * 1024;
        public long MaxFileBytes { get; set; } = 100L * 1024 * 1024;
        public long MaxVideoBytes { get; set; } = 250L * 1024 * 1024;
        public List<string> AllowedImageFormats { get; set; } = new() { "jpg", "jpeg", "png", "webp", "gif" };
        public List<string> AllowedFileFormats { get; set; } = new() { "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt" };
        public List<string> AllowedVideoFormats { get; set; } = new() { "mp4", "webm", "mov" };
    }

    [BsonIgnoreExtraElements]
    public class SiteSettings
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public List<LanguageSetting> Languages { get; set; } = new()
        {
            new LanguageSetting { Slug = "en", Label = "English",      NativeName = "English",    Active = true, AdminEnabled = true, UserEnabled = true, Direction = "ltr", Order = 1 },
            new LanguageSetting { Slug = "vi", Label = "Vietnamese",   NativeName = "TiÃƒÂ¡Ã‚ÂºÃ‚Â¿ng ViÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡t", Active = true, AdminEnabled = true, UserEnabled = true, Direction = "ltr", Order = 2 },
            new LanguageSetting { Slug = "cn", Label = "Chinese",      NativeName = "ÃƒÂ¤Ã‚Â¸Ã‚Â­ÃƒÂ¦Ã¢â‚¬â€œÃ¢â‚¬Â¡",        Active = true, AdminEnabled = true, UserEnabled = true, Direction = "ltr", Order = 3 }
        };

        public string DefaultLanguage { get; set; } = "en";
        public int LanguageRegistryVersion { get; set; } = 1;
        public string AdminAppearancePreset { get; set; } = "navy-gold";
        public ResourceLibrarySettings ResourceLibrary { get; set; } = new();
    }

    public class GlossaryTerm
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // ----------------------------------------------------------------
    // Publish/Reset
    // ----------------------------------------------------------------

    public class ResetResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public static ResetResult Ok() => new()
        {
            Success = true,
            Message = "Page reset to last published state."
        };

        public static ResetResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
    public class PublishResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime? PublishedAt { get; set; }

        public static PublishResult Ok(DateTime publishedAt) => new()
        {
            Success = true,
            Message = "Page published successfully.",
            PublishedAt = publishedAt
        };

        public static PublishResult Fail(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
