using System.Text.Json.Serialization;

namespace Contracts.Public
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PublicTextBlockDto), "text")]
    [JsonDerivedType(typeof(PublicImageBlockDto), "image")]
    [JsonDerivedType(typeof(PublicVideoBlockDto), "video")]
    [JsonDerivedType(typeof(PublicFileBlockDto), "file")]
    [JsonDerivedType(typeof(PublicMapBlockDto), "map")]
    [JsonDerivedType(typeof(PublicFormBlockDto), "form")]
    [JsonDerivedType(typeof(PublicCardBlockDto), "card")]
    [JsonDerivedType(typeof(PublicButtonBlockDto), "button")]
    [JsonDerivedType(typeof(PublicMetricBlockDto), "metric")]
    [JsonDerivedType(typeof(PublicBulletListBlockDto), "bullet-list")]
    [JsonDerivedType(typeof(PublicStepBlockDto), "step")]
    [JsonDerivedType(typeof(PublicIconBlockDto), "icon")]
    [JsonDerivedType(typeof(PublicContainerBlockDto), "container")]
    public abstract class PublicBlockDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Visible { get; set; }
        public List<PublicBlockButtonDto>? Buttons { get; set; }
        public string? BlockZone { get; set; }
        public string? ParentBlockId { get; set; }
        public PublicBlockLayoutDto Layout { get; set; } = new();
    }

    public class PublicTextBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Content { get; set; }
    }

    public class PublicImageBlockDto : PublicBlockDto
    {
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? AltText { get; set; }
    }

    public class PublicVideoBlockDto : PublicBlockDto
    {
        public string? EmbedUrl { get; set; }
    }

    public class PublicFileBlockDto : PublicBlockDto
    {
        public string? FileUrl { get; set; }
        public string? Filename { get; set; }
        public string? FileType { get; set; }
    }

    public class PublicMapBlockDto : PublicBlockDto
    {
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? DefaultZoom { get; set; }
        public List<PublicMapPinDto>? Pins { get; set; }
    }

    public class PublicFormBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? SubmitButtonLabel { get; set; }
        public List<PublicFormFieldDto>? Fields { get; set; }
    }

    public class PublicCardBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
    }

    public class PublicButtonBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? Label { get; set; }
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string? Style { get; set; }
    }

    public class PublicMetricBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public string? Value { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public Dictionary<string, string>? Description { get; set; }
    }

    public class PublicBulletListBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? Title { get; set; }
        public List<PublicBulletListItemDto>? Items { get; set; }
    }

    public class PublicStepBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public Dictionary<string, string>? StepLabel { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
    }

    public class PublicIconBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public Dictionary<string, string>? Description { get; set; }
    }

    public class PublicContainerBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? Title { get; set; }
        public string? LayoutMode { get; set; }
        public int? Columns { get; set; }
        public string? Gap { get; set; }
        public List<PublicBlockDto>? Children { get; set; }
    }

    public class PublicBulletListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Dictionary<string, string>? Text { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }
}
