using System.Text.Json.Serialization;

namespace Contracts.Global
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GlobalButtonPosition { HeaderLeft, HeaderRight, Floating }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GlobalButtonAction { OpenModal, LinkToPage, ExternalUrl }

    public class PublicTheme
    {
        public string FontBody { get; set; } = "Inter";
        public string FontHeading { get; set; } = "Inter";
        public string TextSizeBase { get; set; } = "16px";
        public string TextSizeEyebrow { get; set; } = "13px";
        public string TextSizeHeading { get; set; } = "40px";
        public string TextSizeSubheading { get; set; } = "17px";
        public string TextSizeBody { get; set; } = "16px";
        public string TextSizeSmall { get; set; } = "13px";
        public string TextSizeItemTitle { get; set; } = "20px";
        public string ColorPrimary { get; set; } = "#001a33";
        public string ColorAccent { get; set; } = "#e5c076";
        public string ColorBackground { get; set; } = "#ffffff";
        public string ColorText { get; set; } = "#111827";
        public string BorderRadius { get; set; } = "10px";
        public string ButtonSizeScale { get; set; } = "1";
        public string ButtonTextSize { get; set; } = "15px";
        public bool AnimationsEnabled { get; set; } = true;
        public string AnimationSpeed { get; set; } = "normal";
        public string SpacingScale { get; set; } = "1";
    }

    public class PublicBranding
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string Href { get; set; } = "/";
    }

    public class PublicNavItem
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public string Slug { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class PublicGlobalButton
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> LabelText { get; set; } = new();
        public GlobalButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public GlobalButtonPosition Position { get; set; }
        public int Order { get; set; }
    }

    public class PublicFooter
    {
        public string CompanyName { get; set; } = string.Empty;
        public List<PublicFooterGroup> Groups { get; set; } = new();
    }

    public class PublicFooterGroup
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<PublicFooterLink> Links { get; set; } = new();
    }

    public class PublicFooterLink
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
    }

    public class PublicSocialButton
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}

