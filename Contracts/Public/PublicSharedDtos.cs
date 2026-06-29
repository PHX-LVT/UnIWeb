namespace Contracts.Public
{
    public class PublicSectionStyleDto
    {
        public string BackgroundType { get; set; } = "color";
        public string BackgroundColor { get; set; } = "#ffffff";
        public string? BackgroundImageUrl { get; set; }
        public string? BackgroundVideoUrl { get; set; }
        public string BackgroundImageFit { get; set; } = "cover";
        public string BackgroundImagePosition { get; set; } = "center";
        public string? GradientFrom { get; set; }
        public string? GradientTo { get; set; }
        public string GradientDirection { get; set; } = "top";
        public string? OverlayColor { get; set; }
        public double OverlayOpacity { get; set; } = 0;
        public string Height { get; set; } = "auto";
        public int? CustomMinHeightPx { get; set; }
        public string Padding { get; set; } = "medium";
        public string ContentWidth { get; set; } = "normal";
        public string TextColor { get; set; } = "dark";
        public string MobileLayout { get; set; } = "stack";
        public string BlockLayoutMode { get; set; } = "stack";
        public int BlockGridColumns { get; set; } = 12;
        public string BlockGap { get; set; } = "medium";
    }

    public class PublicBlockLayoutDto
    {
        public string Width { get; set; } = "auto";
        public int ColumnSpan { get; set; } = 12;
        public string Align { get; set; } = "stretch";
        public string Justify { get; set; } = "start";
        public string Padding { get; set; } = "none";
        public string Margin { get; set; } = "none";
        public string? BackgroundColor { get; set; }
        public string BorderRadius { get; set; } = "none";
        public int ZIndex { get; set; } = 1;
        public int ZOrder { get; set; } = 1;
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int W { get; set; } = 4;
        public int H { get; set; } = 2;
        public double? LeftPercent { get; set; }
        public double? TopPx { get; set; }
        public double? WidthPercent { get; set; }
        public double? HeightPx { get; set; }
    }

    public class PublicSectionButtonDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Action { get; set; } = string.Empty;
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class PublicBlockButtonDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Action { get; set; } = "linkToPage";
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class PublicListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public int Order { get; set; }
    }

    public class PublicStatItemDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public decimal Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int Order { get; set; }
    }

    public class PublicCarouselItemDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Tag { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public List<PublicCarouselMetricDto> Metrics { get; set; } = new();
        public int Order { get; set; }
    }

    public class PublicCarouselMetricDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Value { get; set; } = new();
        public Dictionary<string, string> Label { get; set; } = new();
        public string Tone { get; set; } = "positive";
        public int Order { get; set; }
    }

    public class PublicTestimonialItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public int Order { get; set; }
    }

    public class PublicColumnSlotDto
    {
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<PublicBlockDto> Blocks { get; set; } = new();
    }

    public class PublicChildCardDto
    {
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string? SourceId { get; set; }
        public string FullSlug { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public PublicChildCardContentDto? Card { get; set; }
    }

    public class PublicChildCardContentDto
    {
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
    }

    public class PublicLibraryAttachmentDto
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public class PublicLibraryGalleryItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "image";
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public Dictionary<string, string> Caption { get; set; } = new();
        public int Order { get; set; }
    }

    public class PublicLibraryItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string ContentTypeKey { get; set; } = string.Empty;
        public string ContentBehavior { get; set; } = "page";
        public string Slug { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string ClickBehavior { get; set; } = "detail";
        public List<string> Tags { get; set; } = new();
        public List<PublicLibraryAttachmentDto> Attachments { get; set; } = new();
        public List<PublicLibraryGalleryItemDto> GalleryItems { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class PublicMapPinDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
        public int Order { get; set; }
    }

    public class PublicFormFieldDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public Dictionary<string, string> Placeholder { get; set; } = new();
        public bool Required { get; set; }
        public int MaxLength { get; set; }
        public int InputBoxSize { get; set; } = 1;
        public List<PublicFormFieldOptionDto> Options { get; set; } = new();
        public int Order { get; set; }
    }

    public class PublicFormFieldOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public int Order { get; set; }
    }

    public class PublicPageDto
    {
        public string Id { get; set; } = string.Empty;
        public string? FullSlug { get; set; }
        public Dictionary<string, string> Name { get; set; } = new();
        public string Slug { get; set; } = string.Empty;
        public List<PublicSectionDto> Sections { get; set; } = new();
    }

    public class PublicNavItemDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public string Slug { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}


