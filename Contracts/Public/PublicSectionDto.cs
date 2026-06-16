using System.Text.Json.Serialization;

namespace Contracts.Public
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PublicHeroSectionDto), "hero")]
    [JsonDerivedType(typeof(PublicCtaSectionDto), "cta")]
    [JsonDerivedType(typeof(PublicGallerySectionDto), "gallery")]
    [JsonDerivedType(typeof(PublicListSectionDto), "list")]
    [JsonDerivedType(typeof(PublicHtmlSectionDto), "html")]
    [JsonDerivedType(typeof(PublicColumnsSectionDto), "columns")]
    [JsonDerivedType(typeof(PublicShowcaseSectionDto), "showcase")]
    [JsonDerivedType(typeof(PublicLibrarySectionDto), "library")]
    [JsonDerivedType(typeof(PublicStatsSectionDto), "stats")]
    [JsonDerivedType(typeof(PublicCarouselSectionDto), "carousel")]
    [JsonDerivedType(typeof(PublicNetworkMapSectionDto), "network-map")]
    [JsonDerivedType(typeof(PublicTestimonialSectionDto), "testimonial")]
    [JsonDerivedType(typeof(PublicCanvasSectionDto), "canvas")]
    public abstract class PublicSectionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Visible { get; set; }
        public PublicSectionStyleDto Style { get; set; } = new();
        public List<PublicBlockDto>? Blocks { get; set; }
    }

    public class PublicHeroSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? Layout { get; set; }
        public string? HeadingSize { get; set; }
        public string? ContentAlignment { get; set; }
        public string? ImageUrl { get; set; }
        public List<PublicSectionButtonDto>? Buttons { get; set; }
    }

    public class PublicCtaSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subtext { get; set; }
        public string? Layout { get; set; }
        public PublicSectionButtonDto? Button { get; set; }
        public List<PublicSectionButtonDto>? Buttons { get; set; }
    }

    public class PublicGallerySectionDto : PublicSectionDto
    {
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public bool? ShowCaptions { get; set; }
        public List<PublicGalleryImageDto>? Images { get; set; }
    }

    public class PublicListSectionDto : PublicSectionDto
    {
        public string? Layout { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public bool? ShowIcon { get; set; }
        public int? Columns { get; set; }
        public List<PublicListItemDto>? Items { get; set; }
    }

    public class PublicHtmlSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? HtmlContent { get; set; }
    }

    public class PublicColumnsSectionDto : PublicSectionDto
    {
        public int? ColumnCount { get; set; }
        public string? ColumnRatio { get; set; }
        public bool? StackOnMobile { get; set; }
        public List<PublicColumnSlotDto>? ColumnSlots { get; set; }
    }

    public class PublicShowcaseSectionDto : PublicSectionDto
    {
        public string? SourcePageId { get; set; }
        public string? Layout { get; set; }
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public PublicSectionButtonDto? ActionButton { get; set; }
        public string? ActionButtonPosition { get; set; }
        public int? Columns { get; set; }
        public int? Limit { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowContent { get; set; }
        public bool? ShowItemButton { get; set; }
        public bool? ShowSearchBar { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public List<PublicChildCardDto>? Children { get; set; }
    }

    public class PublicLibrarySectionDto : PublicSectionDto
    {
        public List<string>? ContentTypes { get; set; }
        public string? Layout { get; set; }
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public int? Columns { get; set; }
        public int? Rows { get; set; }
        public int? Limit { get; set; }
        public bool? EnableTabs { get; set; }
        public bool? EnablePagination { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowSummary { get; set; }
        public bool? ShowButton { get; set; }
        public bool? ShowTime { get; set; }
        public string? ButtonStyle { get; set; }
        public bool? ShowSearchBar { get; set; }
        public bool? ShowFilters { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public string? SortMode { get; set; }
        public List<PublicLibraryItemDto>? Items { get; set; }
    }

    public class PublicStatsSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public int? Columns { get; set; }
        public int? DurationMs { get; set; }
        public List<PublicStatItemDto>? Items { get; set; }
    }

    public class PublicCarouselSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public bool? Autoplay { get; set; }
        public bool? ShowDots { get; set; }
        public bool? ShowArrows { get; set; }
        public List<PublicCarouselItemDto>? Items { get; set; }
    }

    public class PublicNetworkMapSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<PublicMapPinDto>? Pins { get; set; }
    }

    public class PublicTestimonialSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? Layout { get; set; }
        public string? HeaderAlignment { get; set; }
        public int? Columns { get; set; }
        public List<PublicTestimonialItemDto>? Items { get; set; }
    }

    public class PublicCanvasSectionDto : PublicSectionDto
    {
        public Dictionary<string, string>? AdminLabel { get; set; }
    }
}

