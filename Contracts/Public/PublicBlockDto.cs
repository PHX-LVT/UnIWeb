using System.Text.Json.Serialization;
using Contracts.Admin;
using Contracts.Forms;
using Contracts.Icons;

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
        public string StableId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Visible { get; set; }
        public List<PublicBlockButtonDto>? Buttons { get; set; }
        public string? BlockZone { get; set; }
        public string? ZoneId { get; set; }
        public string PositionMode { get; set; } = "flow";
        public string? ParentBlockId { get; set; }
        public string? PresetSlotName { get; set; }
        public PublicBlockLayoutDto Layout { get; set; } = new();
        public PublicBlockAppearanceDto Appearance { get; set; } = new();
        public PublicBlockResponsiveSettingsDto Responsive { get; set; } = new();
        public PublicBlockAnimationSettingsDto Animation { get; set; } = new();
    }

    public class PublicTextBlockDto : PublicBlockDto
    {
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Content { get; set; }
    }

    public class PublicImageBlockDto : PublicBlockDto
    {
        public PublicBlockAssetReferenceDto Asset { get; set; } = new();
        public string? ImageUrl { get; set; }
        public Dictionary<string, string>? AltText { get; set; }
        public Dictionary<string, string>? Caption { get; set; }
        public bool OpenInLightbox { get; set; }
        public double FocalPointX { get; set; } = 50;
        public double FocalPointY { get; set; } = 50;
    }

    public class PublicVideoBlockDto : PublicBlockDto
    {
        public PublicBlockAssetReferenceDto Asset { get; set; } = new();
        public string? EmbedUrl { get; set; }
        public string SourceType { get; set; } = "youtube";
        public Dictionary<string, string>? Title { get; set; }
        public bool ShowControls { get; set; } = true;
        public bool Autoplay { get; set; }
        public bool Muted { get; set; }
        public bool Loop { get; set; }
    }

    public class PublicFileBlockDto : PublicBlockDto
    {
        public PublicBlockAssetReferenceDto Asset { get; set; } = new();
        public string? FileUrl { get; set; }
        public string? Filename { get; set; }
        public Dictionary<string, string>? DisplayName { get; set; }
        public string? FileType { get; set; }
        public string OpenBehavior { get; set; } = "open";
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
        public string? FormDefinitionId { get; set; }
        public bool FormDefinitionAvailable { get; set; } = true;
        public double FormScale { get; set; } = 1d;
        public int? DefaultWidthPx { get; set; }
        public double? DefaultWidthPercent { get; set; }
        public double? DefaultHeightPx { get; set; }
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Introduction { get; set; }
        public List<FormInformationItemDto> InformationItems { get; set; } = new();
        public List<FormAuxiliaryActionDto> AuxiliaryActions { get; set; } = new();
        public FormDesignSettingsDto Design { get; set; } = new();
        public Dictionary<string, string>? SubmitButtonLabel { get; set; }
        public List<PublicFormFieldDto>? Fields { get; set; }
    }

    public class PublicCardBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public PublicIconReferenceDto? IconVisual { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? ImageUrl { get; set; }
        public PublicBlockAssetReferenceDto Asset { get; set; } = new();
        public Dictionary<string, string>? ImageAltText { get; set; }
        public Dictionary<string, string>? ButtonLabel { get; set; }
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string ButtonStyle { get; set; } = "outline";
    }

    public class PublicButtonBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public PublicIconReferenceDto? IconVisual { get; set; }
        public string IconPosition { get; set; } = "left";
        public Dictionary<string, string>? Label { get; set; }
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
        public string? Style { get; set; }
    }

    public class PublicMetricBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public PublicIconReferenceDto? IconVisual { get; set; }
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
        public PublicIconReferenceDto? IconVisual { get; set; }
        public bool AutoNumber { get; set; }
        public int? SequenceNumber { get; set; }
        public Dictionary<string, string>? StepLabel { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Description { get; set; }
    }

    public class PublicIconBlockDto : PublicBlockDto
    {
        public string? Icon { get; set; }
        public PublicIconReferenceDto? IconVisual { get; set; }
        public Dictionary<string, string>? Label { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public bool ActionEnabled { get; set; }
        public string? Href { get; set; }
        public string Action { get; set; } = "linkToPage";
        public string? FormDefinitionId { get; set; }
    }

    public class PublicContainerBlockDto : PublicBlockDto
    {
        public string PresetKey { get; set; } = ContainerPresetCatalog.CircleSixKey;
        public Dictionary<string, string>? Title { get; set; }
        public PublicContainerLayoutSettingsDto ContainerLayout { get; set; } = new();
        public List<PublicBlockDto>? Children { get; set; }
    }

    public class PublicBulletListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public PublicIconReferenceDto? IconVisual { get; set; }
        public Dictionary<string, string>? Text { get; set; }
        public bool Visible { get; set; } = true;
        public int Order { get; set; }
    }
}
