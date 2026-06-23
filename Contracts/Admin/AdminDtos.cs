

using System.Text.Json.Serialization;
using System.Text.Json;

namespace Contracts.Admin
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PageStatus { Draft, Published }



    // -----------------------------------------------------------
    // SECTIONS
    // -----------------------------------------------------------


    // -- Shared sub-DTOs ---------------------------------------

    public class SectionStyleDto
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

    public class SectionStyleResponseDto
    {
        public string BackgroundType { get; set; } = "color";
        public string BackgroundColor { get; set; } = "#ffffff";
        public string? BackgroundImageUrl { get; set; }
        public string? BackgroundVideoUrl { get; set; }
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

    public class BlockLayoutDto
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
        public int? ZOrder { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? W { get; set; }
        public int? H { get; set; }
        public double? LeftPercent { get; set; }
        public double? TopPx { get; set; }
        public double? WidthPercent { get; set; }
        public double? HeightPx { get; set; }
    }

    public class BlockLayoutResponseDto
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

    public class SectionButtonDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public string Action { get; set; } = string.Empty;
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class SectionButtonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Action { get; set; } = string.Empty;
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class GalleryImageDto
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> Caption { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class GalleryImageResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> Caption { get; set; } = new();
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class ListItemDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class ListItemResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class StatItemDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public decimal Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class StatItemResponseDto : StatItemDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class CarouselItemDto
    {
        public Dictionary<string, string> Tag { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string? LinkHref { get; set; }
        public List<CarouselMetricDto> Metrics { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class CarouselItemResponseDto : CarouselItemDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class CarouselMetricDto
    {
        public Dictionary<string, string> Value { get; set; } = new();
        public Dictionary<string, string> Label { get; set; } = new();
        public string Tone { get; set; } = "positive";
        public int Order { get; set; }
    }

    public class NetworkMapPinDto
    {
        public string Label { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class TestimonialItemDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class NetworkMapPinResponseDto : NetworkMapPinDto
    {
        public string Id { get; set; } = string.Empty;
    }

    public class TestimonialItemResponseDto : TestimonialItemDto
    {
        public string Id { get; set; } = string.Empty;
    }

    // -- Section Create DTOs -----------------------------------

    [JsonConverter(typeof(SectionCreateDtoJsonConverter))]
    public abstract class SectionCreateDto
    {
        [JsonPropertyName("type")]
        public string Type => SectionDtoJson.GetTypeDiscriminator(GetType());

        public bool Visible { get; set; } = true;
        public SectionStyleDto? Style { get; set; }
    }

    public class HeroSectionCreateDto : SectionCreateDto
    {
        public string Layout { get; set; } = "centered";
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> Heading { get; set; } = new();
        public Dictionary<string, string> Subheading { get; set; } = new();
        public string HeadingSize { get; set; } = "medium";
        public string ContentAlignment { get; set; } = "center";
        public string? ImageUrl { get; set; }
        public List<SectionButtonDto> Buttons { get; set; } = new();
    }

    public class CtaSectionCreateDto : SectionCreateDto
    {
        public string Layout { get; set; } = "stacked";
        public Dictionary<string, string> Heading { get; set; } = new();
        public Dictionary<string, string> Subtext { get; set; } = new();
        public SectionButtonDto? Button { get; set; }
        public List<SectionButtonDto> Buttons { get; set; } = new();
    }

    public class GallerySectionCreateDto : SectionCreateDto
    {
        public string Layout { get; set; } = "grid";
        public int Columns { get; set; } = 3;
        public string Gap { get; set; } = "small";
        public bool ShowCaptions { get; set; } = false;
        public List<GalleryImageDto> Images { get; set; } = new();
    }

    public class ListSectionCreateDto : SectionCreateDto
    {
        public string Layout { get; set; } = "cards";
        public int Columns { get; set; } = 3;
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public bool ShowIcon { get; set; } = true;
        public List<ListItemDto> Items { get; set; } = new();
    }

  /*  public class DynamicSectionCreateDto : SectionCreateDto
    {
        public List<string> ScopeSectionIds { get; set; } = new();
        public string SearchBy { get; set; } = "title";
        public string Display { get; set; } = "list";
        public Dictionary<string, string> Placeholder { get; set; } = new();
        public string DefaultSort { get; set; } = "manual";
        public bool ShowSearchBar { get; set; } = true;
    }
  */

    public class HtmlSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> Content { get; set; } = new();
    }

    public class ColumnsSectionCreateDto : SectionCreateDto
    {
        public int ColumnCount { get; set; } = 2;
        public string ColumnRatio { get; set; } = "equal";
        public string Gap { get; set; } = "medium";
        public bool StackOnMobile { get; set; } = true;
    }

    public class ShowcaseSectionCreateDto : SectionCreateDto
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
        public Dictionary<string, string> ButtonLabel { get; set; } = new() { ["en"] = "Learn More" };
        public SectionButtonDto? ActionButton { get; set; }
        public string ActionButtonPosition { get; set; } = "bottom-center";
        public bool ShowSearchBar { get; set; } = false;
        public Dictionary<string, string> SearchPlaceholder { get; set; } = new();
        public List<ShowcaseItemOverrideDto> Items { get; set; } = new();
    }

    public class LibrarySectionCreateDto : SectionCreateDto
    {
        public List<string> ContentTypes { get; set; } = new();
        public string Layout { get; set; } = "card";
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

    public class StatsSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public int Columns { get; set; } = 4;
        public int DurationMs { get; set; } = 1200;
        public List<StatItemDto> Items { get; set; } = new();
    }

    public class CarouselSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public string Layout { get; set; } = "cards";
        public int Columns { get; set; } = 3;
        public bool Autoplay { get; set; }
        public bool ShowDots { get; set; } = true;
        public bool ShowArrows { get; set; } = true;
        public List<CarouselItemDto> Items { get; set; } = new();
    }

    public class NetworkMapSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public double CenterLat { get; set; } = 15.87;
        public double CenterLng { get; set; } = 100.99;
        public int DefaultZoom { get; set; } = 4;
        public List<NetworkMapPinDto> Pins { get; set; } = new();
    }

    public class TestimonialSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> Eyebrow { get; set; } = new();
        public Dictionary<string, string> SectionTitle { get; set; } = new();
        public Dictionary<string, string> Subheading { get; set; } = new();
        public string Layout { get; set; } = "cards";
        public string HeaderAlignment { get; set; } = "center";
        public int Columns { get; set; } = 4;
        public List<TestimonialItemDto> Items { get; set; } = new();
    }

    public class CanvasSectionCreateDto : SectionCreateDto
    {
        public Dictionary<string, string> AdminLabel { get; set; } = new();
    }

    // -- Section Update DTOs -----------------------------------
    // All type-specific fields are nullable � only send what you want to change.
    // Visible is always applied (use the /visibility endpoint to toggle without a full update).

    [JsonConverter(typeof(SectionUpdateDtoJsonConverter))]
    public abstract class SectionUpdateDto
    {
        [JsonPropertyName("type")]
        public string Type => SectionDtoJson.GetTypeDiscriminator(GetType());

        public bool? Visible { get; set; }
    }

    public class HeroSectionUpdateDto : SectionUpdateDto
    {
        public string? Layout { get; set; }
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? HeadingSize { get; set; }
        public string? ContentAlignment { get; set; }
        public string? ImageUrl { get; set; }
        // Null = don't touch buttons; empty list = clear all buttons
        public List<SectionButtonDto>? Buttons { get; set; }
    }

    public class CtaSectionUpdateDto : SectionUpdateDto
    {
        public string? Layout { get; set; }
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subtext { get; set; }
        public SectionButtonDto? Button { get; set; }
        public List<SectionButtonDto>? Buttons { get; set; }
    }

    public class GallerySectionUpdateDto : SectionUpdateDto
    {
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public bool? ShowCaptions { get; set; }
        // Null = don't touch images; empty list = clear all images
        public List<GalleryImageDto>? Images { get; set; }
    }

    public class ListSectionUpdateDto : SectionUpdateDto
    {
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public bool? ShowIcon { get; set; }
        // Null = don't touch items; empty list = clear all items
        public List<ListItemDto>? Items { get; set; }
    }

    /*public class DynamicSectionUpdateDto : SectionUpdateDto
    {
        public List<string>? ScopeSectionIds { get; set; }
        public string? SearchBy { get; set; }
        public string? Display { get; set; }
        public Dictionary<string, string>? Placeholder { get; set; }
        public string? DefaultSort { get; set; }
        public bool? ShowSearchBar { get; set; }
    } */

    public class HtmlSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? Content { get; set; }
    }

    public class ColumnsSectionUpdateDto : SectionUpdateDto
    {
        public int? ColumnCount { get; set; }
        public string? ColumnRatio { get; set; }
        public string? Gap { get; set; }
        public bool? StackOnMobile { get; set; }
    }

    public class ShowcaseSectionUpdateDto : SectionUpdateDto
    {
        public string? SourcePageId { get; set; }
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public int? Limit { get; set; }
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowContent { get; set; }
        public bool? ShowItemButton { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public SectionButtonDto? ActionButton { get; set; }
        public string? ActionButtonPosition { get; set; }
        public bool? ShowSearchBar { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public List<ShowcaseItemOverrideDto>? Items { get; set; }
    }

    public class LibrarySectionUpdateDto : SectionUpdateDto
    {
        public List<string>? ContentTypes { get; set; }
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public int? Rows { get; set; }
        public int? Limit { get; set; }
        public bool? EnableTabs { get; set; }
        public bool? EnablePagination { get; set; }
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowSummary { get; set; }
        public bool? ShowButton { get; set; }
        public bool? ShowTime { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public string? ButtonStyle { get; set; }
        public bool? ShowSearchBar { get; set; }
        public bool? ShowFilters { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public string? SortMode { get; set; }
    }

    public class StatsSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public int? Columns { get; set; }
        public int? DurationMs { get; set; }
        public List<StatItemDto>? Items { get; set; }
    }

    public class CarouselSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public string? Layout { get; set; }
        public int? Columns { get; set; }
        public bool? Autoplay { get; set; }
        public bool? ShowDots { get; set; }
        public bool? ShowArrows { get; set; }
        public List<CarouselItemDto>? Items { get; set; }
    }

    public class NetworkMapSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? SectionTitle { get; set; }
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<NetworkMapPinDto>? Pins { get; set; }
    }

    public class TestimonialSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? Layout { get; set; }
        public string? HeaderAlignment { get; set; }
        public int? Columns { get; set; }
        public List<TestimonialItemDto>? Items { get; set; }
    }

    public class CanvasSectionUpdateDto : SectionUpdateDto
    {
        public Dictionary<string, string>? AdminLabel { get; set; }
    }

    public sealed class SectionCreateDtoJsonConverter : JsonConverter<SectionCreateDto>
    {
        public override SectionCreateDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var type = SectionDtoJson.ReadType(document.RootElement);
            return type switch
            {
                "hero" => document.RootElement.Deserialize<HeroSectionCreateDto>(options),
                "cta" => document.RootElement.Deserialize<CtaSectionCreateDto>(options),
                "gallery" => document.RootElement.Deserialize<GallerySectionCreateDto>(options),
                "list" => document.RootElement.Deserialize<ListSectionCreateDto>(options),
                "html" => document.RootElement.Deserialize<HtmlSectionCreateDto>(options),
                "columns" => document.RootElement.Deserialize<ColumnsSectionCreateDto>(options),
                "showcase" => document.RootElement.Deserialize<ShowcaseSectionCreateDto>(options),
                "library" => document.RootElement.Deserialize<LibrarySectionCreateDto>(options),
                "stats" => document.RootElement.Deserialize<StatsSectionCreateDto>(options),
                "carousel" => document.RootElement.Deserialize<CarouselSectionCreateDto>(options),
                "network-map" => document.RootElement.Deserialize<NetworkMapSectionCreateDto>(options),
                "testimonial" => document.RootElement.Deserialize<TestimonialSectionCreateDto>(options),
                "canvas" => document.RootElement.Deserialize<CanvasSectionCreateDto>(options),
                _ => throw new JsonException($"Unknown section type '{type}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SectionCreateDto value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    public sealed class SectionUpdateDtoJsonConverter : JsonConverter<SectionUpdateDto>
    {
        public override SectionUpdateDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var type = SectionDtoJson.ReadType(document.RootElement);
            return type switch
            {
                "hero" => document.RootElement.Deserialize<HeroSectionUpdateDto>(options),
                "cta" => document.RootElement.Deserialize<CtaSectionUpdateDto>(options),
                "gallery" => document.RootElement.Deserialize<GallerySectionUpdateDto>(options),
                "list" => document.RootElement.Deserialize<ListSectionUpdateDto>(options),
                "html" => document.RootElement.Deserialize<HtmlSectionUpdateDto>(options),
                "columns" => document.RootElement.Deserialize<ColumnsSectionUpdateDto>(options),
                "showcase" => document.RootElement.Deserialize<ShowcaseSectionUpdateDto>(options),
                "library" => document.RootElement.Deserialize<LibrarySectionUpdateDto>(options),
                "stats" => document.RootElement.Deserialize<StatsSectionUpdateDto>(options),
                "carousel" => document.RootElement.Deserialize<CarouselSectionUpdateDto>(options),
                "network-map" => document.RootElement.Deserialize<NetworkMapSectionUpdateDto>(options),
                "testimonial" => document.RootElement.Deserialize<TestimonialSectionUpdateDto>(options),
                "canvas" => document.RootElement.Deserialize<CanvasSectionUpdateDto>(options),
                _ => throw new JsonException($"Unknown section type '{type}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SectionUpdateDto value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    internal static class SectionDtoJson
    {
        public static string GetTypeDiscriminator(Type dtoType) => dtoType.Name switch
        {
            nameof(HeroSectionCreateDto) or nameof(HeroSectionUpdateDto) => "hero",
            nameof(CtaSectionCreateDto) or nameof(CtaSectionUpdateDto) => "cta",
            nameof(GallerySectionCreateDto) or nameof(GallerySectionUpdateDto) => "gallery",
            nameof(ListSectionCreateDto) or nameof(ListSectionUpdateDto) => "list",
            nameof(HtmlSectionCreateDto) or nameof(HtmlSectionUpdateDto) => "html",
            nameof(ColumnsSectionCreateDto) or nameof(ColumnsSectionUpdateDto) => "columns",
            nameof(ShowcaseSectionCreateDto) or nameof(ShowcaseSectionUpdateDto) => "showcase",
            nameof(LibrarySectionCreateDto) or nameof(LibrarySectionUpdateDto) => "library",
            nameof(StatsSectionCreateDto) or nameof(StatsSectionUpdateDto) => "stats",
            nameof(CarouselSectionCreateDto) or nameof(CarouselSectionUpdateDto) => "carousel",
            nameof(NetworkMapSectionCreateDto) or nameof(NetworkMapSectionUpdateDto) => "network-map",
            nameof(TestimonialSectionCreateDto) or nameof(TestimonialSectionUpdateDto) => "testimonial",
            nameof(CanvasSectionCreateDto) or nameof(CanvasSectionUpdateDto) => "canvas",
            _ => throw new JsonException($"Unsupported section DTO type '{dtoType.Name}'.")
        };

        public static string ReadType(JsonElement element)
        {
            if (!element.TryGetProperty("type", out var typeProperty) ||
                typeProperty.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Section payload must include a string 'type' discriminator.");
            }

            return typeProperty.GetString() ?? string.Empty;
        }
    }

    // -- Section Response DTO ----------------------------------
    // Flat response � type-specific fields null for irrelevant types

    public class SectionResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public SectionStyleResponseDto Style { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Hero
        public string? Layout { get; set; }
        public Dictionary<string, string>? Heading { get; set; }
        public Dictionary<string, string>? Subheading { get; set; }
        public string? HeadingSize { get; set; }
        public string? ContentAlignment { get; set; }
        public string? ImageUrl { get; set; }
        public List<SectionButtonResponseDto>? Buttons { get; set; }

        // CTA
        public Dictionary<string, string>? Subtext { get; set; }
        public SectionButtonResponseDto? Button { get; set; }

        // Gallery
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public bool? ShowCaptions { get; set; }
        public List<GalleryImageResponseDto>? Images { get; set; }

        // List
        public Dictionary<string, string>? Eyebrow { get; set; }
        public Dictionary<string, string>? SectionTitle { get; set; }
        public string? HeaderAlignment { get; set; }
        public bool? ShowIcon { get; set; }
        public List<ListItemResponseDto>? Items { get; set; }

        // Dynamic
        public List<string>? ScopeSectionIds { get; set; }
        public string? SearchBy { get; set; }
        public string? Display { get; set; }
        public Dictionary<string, string>? Placeholder { get; set; }
        public string? DefaultSort { get; set; }
        public bool? ShowSearchBar { get; set; }

        // HTML
        public Dictionary<string, string>? HtmlContent { get; set; }

        // Columns
        public int? ColumnCount { get; set; }
        public string? ColumnRatio { get; set; }
        public bool? StackOnMobile { get; set; }
        public List<ColumnSlotResponseDto>? ColumnSlots { get; set; }
        

        // Showcase
        public string? SourcePageId { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public SectionButtonResponseDto? ActionButton { get; set; }
        public string? ActionButtonPosition { get; set; }
        public bool? ShowImage { get; set; }
        public bool? ShowContent { get; set; }
        public bool? ShowItemButton { get; set; }
        public Dictionary<string, string>? SearchPlaceholder { get; set; }
        public List<ShowcaseItemOverrideDto>? ShowcaseItems { get; set; }
        // Children populated by public endpoint only
        public List<ChildPageCardResponseDto>? Children { get; set; }

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

        // Stats / carousel / network-map
        public int? DurationMs { get; set; }
        public bool? Autoplay { get; set; }
        public bool? ShowDots { get; set; }
        public bool? ShowArrows { get; set; }
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<StatItemResponseDto>? Stats { get; set; }
        public List<CarouselItemResponseDto>? CarouselItems { get; set; }
        public List<NetworkMapPinResponseDto>? MapPins { get; set; }
        public List<TestimonialItemResponseDto>? Testimonials { get; set; }

        // Canvas
        public Dictionary<string, string>? AdminLabel { get; set; }
    }
public class ColumnSlotResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class ChildPageCardResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string FullSlug { get; set; } = string.Empty;
    public PageCardResponseDto? Card { get; set; }
}


    public class PageCardResponseDto
    {
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
        public bool IsCustomized { get; set; }
    }

    public class ShowcaseItemOverrideDto
    {
        public string ChildPageId { get; set; } = string.Empty;
        public Dictionary<string, string> CardTitle { get; set; } = new();
        public Dictionary<string, string> CardContent { get; set; } = new();
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
    }

    public class AssetUploadResponseDto
    {
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public class CanvasSectionPresetCreateDto
    {
        public string PageId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
    }

    public class CanvasSectionPresetApplyDto
    {
        public string PageId { get; set; } = string.Empty;
    }

    public class CanvasSectionPresetResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public int BlockCount { get; set; }
        public int SchemaVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // -----------------------------------------------------------
    // BLOCKS
    // -----------------------------------------------------------

    public class BlockButtonDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public BlockButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 0;

    }

    public class BlockButtonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public BlockButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }
    public enum BlockButtonAction { LinkToPage, OpenForm, DownloadFile, ExternalUrl }


    public class FormFieldDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public bool Required { get; set; } = false;
        public List<string>? Options { get; set; }
        public int Order { get; set; } = 0;
    }

    public class MapPinDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Href { get; set; }
    }

    // -- Block Create DTOs -------------------------------------

    [JsonConverter(typeof(BlockCreateDtoJsonConverter))]
    public abstract class BlockCreateDto
    {
        [JsonPropertyName("type")]
        public string Type => BlockDtoJson.GetTypeDiscriminator(GetType());

        public bool Visible { get; set; } = true;
        public List<BlockButtonDto> Buttons { get; set; } = new();
        public string? ColumnSlotId { get; set; }
        public string? BlockZone { get; set; }
        public string? ZoneId { get; set; }
        public string? PositionMode { get; set; }
        public string? ParentBlockId { get; set; }
        public BlockLayoutDto? Layout { get; set; }

    }

    public class TextBlockCreateDto : BlockCreateDto
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Content { get; set; } = new();
    }

    public class ImageBlockCreateDto : BlockCreateDto
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> AltText { get; set; } = new();
    }

    public class VideoBlockCreateDto : BlockCreateDto
    {
        public string EmbedUrl { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
    }

    public class FileBlockCreateDto : BlockCreateDto
    {
        public string? FileUrl { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }

    public class MapBlockCreateDto : BlockCreateDto
    {
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public int DefaultZoom { get; set; } = 12;
        public List<MapPinDto> Pins { get; set; } = new();
    }

    public class FormBlockCreateDto : BlockCreateDto
    {
        public string? FormDefinitionId { get; set; }
        public List<FormFieldDto> Fields { get; set; } = new();
        public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
    }

    public class BulletListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Text { get; set; } = new();
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }

    public class CardBlockCreateDto : BlockCreateDto
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

    public class ButtonBlockCreateDto : BlockCreateDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
    }

    public class MetricBlockCreateDto : BlockCreateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Value { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class BulletListBlockCreateDto : BlockCreateDto
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public List<BulletListItemDto> Items { get; set; } = new();
    }

    public class StepBlockCreateDto : BlockCreateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> StepLabel { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class IconBlockCreateDto : BlockCreateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class ContainerBlockCreateDto : BlockCreateDto
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

    // -- Block Update DTOs -------------------------------------

    [JsonConverter(typeof(BlockUpdateDtoJsonConverter))]
    public abstract class BlockUpdateDto
    {
        [JsonPropertyName("type")]
        public string Type => BlockDtoJson.GetTypeDiscriminator(GetType());

        public bool? Visible { get; set; }
        public List<BlockButtonDto>? Buttons { get; set; }
        public string? BlockZone { get; set; }
        public string? ZoneId { get; set; }
        public string? PositionMode { get; set; }
        public string? ParentBlockId { get; set; }
        public BlockLayoutDto? Layout { get; set; }
    }

    public class TextBlockUpdateDto : BlockUpdateDto
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Content { get; set; } = new();
    }

    public class ImageBlockUpdateDto : BlockUpdateDto
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string> AltText { get; set; } = new();
    }

    public class VideoBlockUpdateDto : BlockUpdateDto
    {
        public string EmbedUrl { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
    }

    public class FileBlockUpdateDto : BlockUpdateDto
    {
        public string? FileUrl { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }

    public class MapBlockUpdateDto : BlockUpdateDto
    {
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public int DefaultZoom { get; set; } = 12;
        public List<MapPinDto> Pins { get; set; } = new();
    }

    public class FormBlockUpdateDto : BlockUpdateDto
    {
        public string? FormDefinitionId { get; set; }
        public List<FormFieldDto> Fields { get; set; } = new();
        public Dictionary<string, string> SubmitButtonLabel { get; set; } = new();
    }

    public class CardBlockUpdateDto : BlockUpdateDto
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

    public class ButtonBlockUpdateDto : BlockUpdateDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string Style { get; set; } = "filled";
    }

    public class MetricBlockUpdateDto : BlockUpdateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public string Value { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class BulletListBlockUpdateDto : BlockUpdateDto
    {
        public Dictionary<string, string> Title { get; set; } = new();
        public List<BulletListItemDto> Items { get; set; } = new();
    }

    public class StepBlockUpdateDto : BlockUpdateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> StepLabel { get; set; } = new();
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class IconBlockUpdateDto : BlockUpdateDto
    {
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
    }

    public class ContainerBlockUpdateDto : BlockUpdateDto
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

    public sealed class BlockCreateDtoJsonConverter : JsonConverter<BlockCreateDto>
    {
        public override BlockCreateDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var type = BlockDtoJson.ReadType(document.RootElement);
            return type switch
            {
                "text" => document.RootElement.Deserialize<TextBlockCreateDto>(options),
                "image" => document.RootElement.Deserialize<ImageBlockCreateDto>(options),
                "video" => document.RootElement.Deserialize<VideoBlockCreateDto>(options),
                "file" => document.RootElement.Deserialize<FileBlockCreateDto>(options),
                "map" => document.RootElement.Deserialize<MapBlockCreateDto>(options),
                "form" => document.RootElement.Deserialize<FormBlockCreateDto>(options),
                "card" => document.RootElement.Deserialize<CardBlockCreateDto>(options),
                "button" => document.RootElement.Deserialize<ButtonBlockCreateDto>(options),
                "metric" => document.RootElement.Deserialize<MetricBlockCreateDto>(options),
                "bullet-list" => document.RootElement.Deserialize<BulletListBlockCreateDto>(options),
                "step" => document.RootElement.Deserialize<StepBlockCreateDto>(options),
                "icon" => document.RootElement.Deserialize<IconBlockCreateDto>(options),
                "container" => document.RootElement.Deserialize<ContainerBlockCreateDto>(options),
                _ => throw new JsonException($"Unknown block type '{type}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, BlockCreateDto value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    public sealed class BlockUpdateDtoJsonConverter : JsonConverter<BlockUpdateDto>
    {
        public override BlockUpdateDto? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var type = BlockDtoJson.ReadType(document.RootElement);
            return type switch
            {
                "text" => document.RootElement.Deserialize<TextBlockUpdateDto>(options),
                "image" => document.RootElement.Deserialize<ImageBlockUpdateDto>(options),
                "video" => document.RootElement.Deserialize<VideoBlockUpdateDto>(options),
                "file" => document.RootElement.Deserialize<FileBlockUpdateDto>(options),
                "map" => document.RootElement.Deserialize<MapBlockUpdateDto>(options),
                "form" => document.RootElement.Deserialize<FormBlockUpdateDto>(options),
                "card" => document.RootElement.Deserialize<CardBlockUpdateDto>(options),
                "button" => document.RootElement.Deserialize<ButtonBlockUpdateDto>(options),
                "metric" => document.RootElement.Deserialize<MetricBlockUpdateDto>(options),
                "bullet-list" => document.RootElement.Deserialize<BulletListBlockUpdateDto>(options),
                "step" => document.RootElement.Deserialize<StepBlockUpdateDto>(options),
                "icon" => document.RootElement.Deserialize<IconBlockUpdateDto>(options),
                "container" => document.RootElement.Deserialize<ContainerBlockUpdateDto>(options),
                _ => throw new JsonException($"Unknown block type '{type}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, BlockUpdateDto value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
    }

    internal static class BlockDtoJson
    {
        public static string GetTypeDiscriminator(Type dtoType) => dtoType.Name switch
        {
            nameof(TextBlockCreateDto) or nameof(TextBlockUpdateDto) => "text",
            nameof(ImageBlockCreateDto) or nameof(ImageBlockUpdateDto) => "image",
            nameof(VideoBlockCreateDto) or nameof(VideoBlockUpdateDto) => "video",
            nameof(FileBlockCreateDto) or nameof(FileBlockUpdateDto) => "file",
            nameof(MapBlockCreateDto) or nameof(MapBlockUpdateDto) => "map",
            nameof(FormBlockCreateDto) or nameof(FormBlockUpdateDto) => "form",
            nameof(CardBlockCreateDto) or nameof(CardBlockUpdateDto) => "card",
            nameof(ButtonBlockCreateDto) or nameof(ButtonBlockUpdateDto) => "button",
            nameof(MetricBlockCreateDto) or nameof(MetricBlockUpdateDto) => "metric",
            nameof(BulletListBlockCreateDto) or nameof(BulletListBlockUpdateDto) => "bullet-list",
            nameof(StepBlockCreateDto) or nameof(StepBlockUpdateDto) => "step",
            nameof(IconBlockCreateDto) or nameof(IconBlockUpdateDto) => "icon",
            nameof(ContainerBlockCreateDto) or nameof(ContainerBlockUpdateDto) => "container",
            _ => throw new JsonException($"Unsupported block DTO type '{dtoType.Name}'.")
        };

        public static string ReadType(JsonElement element)
        {
            if (!element.TryGetProperty("type", out var typeProperty) ||
                typeProperty.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Block payload must include a string 'type' discriminator.");
            }

            return typeProperty.GetString() ?? string.Empty;
        }
    }

    // -- Block Response DTO ------------------------------------

    public class BlockResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public string? ColumnSlotId { get; set; }
        public string? BlockZone { get; set; }
        public string? ZoneId { get; set; }
        public string? PositionMode { get; set; }
        public string? ParentBlockId { get; set; }
        public BlockLayoutResponseDto Layout { get; set; } = new();
        public List<BlockButtonResponseDto> Buttons { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

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
        public string? Action { get; set; }
        public string? FormDefinitionId { get; set; }
        public string? Style { get; set; }
        public string? LayoutMode { get; set; }
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public int? OrbitRadius { get; set; }
        public int? OrbitStartAngle { get; set; }
        public int? SemicircleRadius { get; set; }
        public int? SemicircleStartAngle { get; set; }
        public int? SemicircleEndAngle { get; set; }

        // Image
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? AltText { get; set; }
        public List<BulletListItemDto>? BulletItems { get; set; }

        // Video
        public string? EmbedUrl { get; set; }

        // File
        public string? FileUrl { get; set; }
        public string? Filename { get; set; }
        public string? FileType { get; set; }

        // Map
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<MapPinDto>? Pins { get; set; }

        // Form
        public List<FormFieldDto>? Fields { get; set; }
        public Dictionary<string, string>? SubmitButtonLabel { get; set; }
    }
    // -- Reset/Public DTO ------------------------------------
   
}




