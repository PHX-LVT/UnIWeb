using FullProject.Models;
using System.Text.Json.Serialization;

namespace FullProject.DTOs
{

    // ---------------------------------------------------------------------------------------------------------------------
    // AUTH
    // ---------------------------------------------------------------------------------------------------------------------

    public class SessionResponseDto
    {
        public bool Valid { get; set; }
        public string AdminId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AdminRole Role { get; set; } = AdminRole.Viewer;
        public AdminUserStatus Status { get; set; } = AdminUserStatus.Active;
        public List<string> Permissions { get; set; } = new();
    }



    // ---------------------------------------------------------------------------------------------------------------------
    // BRANDING
    // ---------------------------------------------------------------------------------------------------------------------
    public class BrandingCreateDto
    {
        public string? CompanyName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Href { get; set; }
    }
    public class BrandingUpdateDto
    {
        public string? CompanyName { get; set; }
        public string? LogoUrl { get; set; }
        public string? Href { get; set; }
    }

    public class BrandingResponseDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string Href { get; set; } = "/";
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // THEME
    // ---------------------------------------------------------------------------------------------------------------------

    public class ThemeUpdateDto
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

    public class ThemeResponseDto
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

    // ---------------------------------------------------------------------------------------------------------------------
    // GLOBAL BUTTONS
    // ---------------------------------------------------------------------------------------------------------------------

    public class GlobalButtonCreateDto
    {
        public Dictionary<string, string>? LabelText { get; set; }
        public GlobalButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public GlobalButtonPosition Position { get; set; }
    }

    public class GlobalButtonUpdateDto
    {
        public Dictionary<string, string>? LabelText { get; set; }
        public GlobalButtonAction? Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public GlobalButtonPosition? Position { get; set; }
    }

    public class GlobalButtonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> LabelText { get; set; } = new();
        public GlobalButtonAction Action { get; set; }
        public string? Href { get; set; }
        public string? FormDefinitionId { get; set; }
        public GlobalButtonPosition Position { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // FOOTER
    // ---------------------------------------------------------------------------------------------------------------------

    public class FooterUpdateDto
    {
        public string? CompanyName { get; set; }
    }

    public class FooterGroupCreateDto
    {
        public string Label { get; set; } = string.Empty;
    }

    public class FooterGroupUpdateDto
    {
        public string? Label { get; set; }
    }

    public class FooterLinkCreateDto
    {
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
    }

    public class FooterLinkUpdateDto
    {
        public string? Label { get; set; }
        public string? Href { get; set; }
    }

    public class FooterLinkResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class FooterGroupResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
        public List<FooterLinkResponseDto> Links { get; set; } = new();
    }

    public class FooterResponseDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public List<FooterGroupResponseDto> Groups { get; set; } = new();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // SOCIAL BUTTONS
    // ---------------------------------------------------------------------------------------------------------------------

    public class SocialButtonCreateDto
    {
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
    }

    public class SocialButtonUpdateDto
    {
        public string? Label { get; set; }
        public string? Icon { get; set; }
        public string? Href { get; set; }
    }

    public class SocialButtonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    public class SocialButtonGroupResponseDto
    {
        public bool GroupVisible { get; set; }
        public List<SocialButtonResponseDto> Buttons { get; set; } = new();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // PAGES
    // ---------------------------------------------------------------------------------------------------------------------

    public class PageCardDto
    {
        public Dictionary<string, string>? CardTitle { get; set; }
        public Dictionary<string, string>? CardContent { get; set; }
        public string? CardBackgroundType { get; set; }
        public string? CardBackgroundColor { get; set; }
        public string? CardImageUrl { get; set; }
    }

    

    public class ChildPageCreateDto
    {
        public Dictionary<string, string> Name { get; set; } = new();
        public bool Access { get; set; } = true;
        public bool Visible { get; set; } = true;
    }

    public class ChildPageCardResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullSlug { get; set; } = string.Empty;
        public PageCardResponseDto? Card { get; set; }
    }

    public class PageSeoDto
    {
        public Dictionary<string, string>? MetaTitle { get; set; }
        public Dictionary<string, string>? MetaDescription { get; set; }
    }

    public class PageSeoResponseDto
    {
        public Dictionary<string, string> MetaTitle { get; set; } = new();
        public Dictionary<string, string> MetaDescription { get; set; } = new();
    }

    public class PageCreateDto
    {
        public Dictionary<string, string> Name { get; set; } = new();
        public bool Access { get; set; } = true;
        public bool Visible { get; set; } = true;
        public PageSeoDto? Seo { get; set; }
    }

    public class PageUpdateDto
    {
        public Dictionary<string, string>? Name { get; set; }
        public string? Slug { get; set; }
        public bool? Access { get; set; }
        public bool? Visible { get; set; }
        public PageStatus? Status { get; set; }
        public PageSeoDto? Seo { get; set; }
    }

    public class PageResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public string Slug { get; set; } = string.Empty;
        public bool Access { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
        public PageStatus Status { get; set; }
        public PageSeoResponseDto Seo { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ParentPageId { get; set; }
        public string? ParentSlug { get; set; }
        public string? FullSlug { get; set; }
        public PageCardResponseDto? Card { get; set; }
    }
    public class PublicDownloadMetricDto
    {
        public string? Url { get; set; }
        public string? SourcePage { get; set; }
    }
    public class RevisionResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public int? SourceVersion { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public string ActorId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
    // ---------------------------------------------------------------------------------------------------------------------
    // PAGE-LEVEL BUTTONS
    // ---------------------------------------------------------------------------------------------------------------------

    public class PageButtonCreateDto
    {
        public Dictionary<string, string> Label { get; set; } = new();
        public PageButtonAction Action { get; set; }
        public string? Href { get; set; }
        public PageButtonPosition Position { get; set; }
    }

    public class PageButtonUpdateDto
    {
        public Dictionary<string, string>? Label { get; set; }
        public PageButtonAction? Action { get; set; }
        public string? Href { get; set; }
        public PageButtonPosition? Position { get; set; }
    }

    public class PageButtonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public Dictionary<string, string> Label { get; set; } = new();
        public PageButtonAction Action { get; set; }
        public string? Href { get; set; }
        public PageButtonPosition Position { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // CONTENT MANAGEMENT
    // ---------------------------------------------------------------------------------------------------------------------

    public class ContentTypeCreateDto
    {
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Behavior { get; set; } = "page";
        public bool RequiresBody { get; set; } = true;
        public bool RequiresHeroImage { get; set; }
        public bool RequiresFile { get; set; }
        public bool RequiresVideoUrl { get; set; }
        public bool AllowsAttachments { get; set; } = true;
        public string ClickBehavior { get; set; } = "detail";
        public bool Visible { get; set; } = true;
    }

    public class ContentTypeUpdateDto
    {
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? Behavior { get; set; }
        public bool? RequiresBody { get; set; }
        public bool? RequiresHeroImage { get; set; }
        public bool? RequiresFile { get; set; }
        public bool? RequiresVideoUrl { get; set; }
        public bool? AllowsAttachments { get; set; }
        public string? ClickBehavior { get; set; }
        public bool? Visible { get; set; }
        public int? Order { get; set; }
    }

    public class ContentTypeResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Behavior { get; set; } = "page";
        public bool RequiresBody { get; set; }
        public bool RequiresHeroImage { get; set; }
        public bool RequiresFile { get; set; }
        public bool RequiresVideoUrl { get; set; }
        public bool AllowsAttachments { get; set; }
        public string ClickBehavior { get; set; } = "detail";
        public bool Visible { get; set; }
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ContentAttachmentDto
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public class ContentBodyItemDto
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

    public class ContentCreateDto
    {
        public string ContentTypeKey { get; set; } = "article";
        public string? Slug { get; set; }
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public Dictionary<string, string> BodyHtml { get; set; } = new();
        public List<ContentBodyItemDto> BodyItems { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ContentAttachmentDto> Attachments { get; set; } = new();
        public bool Visible { get; set; } = true;
    }

    public class ContentUpdateDto
    {
        public string? ContentTypeKey { get; set; }
        public string? Slug { get; set; }
        public Dictionary<string, string>? Title { get; set; }
        public Dictionary<string, string>? Summary { get; set; }
        public Dictionary<string, string>? BodyHtml { get; set; }
        public List<ContentBodyItemDto>? BodyItems { get; set; }
        public string? HeroImageUrl { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string>? Tags { get; set; }
        public List<ContentAttachmentDto>? Attachments { get; set; }
        public bool? Visible { get; set; }
    }

    public class ContentStatusUpdateDto
    {
        public ContentStatus Status { get; set; }
        public string? Message { get; set; }
    }

    public class ContentPermanentDeleteDto
    {
        public List<string> Ids { get; set; } = new();
    }

    public class ContentResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string ContentTypeKey { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new();
        public Dictionary<string, string> Summary { get; set; } = new();
        public Dictionary<string, string> BodyHtml { get; set; } = new();
        public List<ContentBodyItemDto> BodyItems { get; set; } = new();
        public string? HeroImageUrl { get; set; }
        public string? HeroImageAlt { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public string? TemplateKey { get; set; }
        public List<string> Tags { get; set; } = new();
        public List<ContentAttachmentDto> Attachments { get; set; } = new();
        public ContentStatus Status { get; set; }
        public bool Visible { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string? UpdatedById { get; set; }
        public string? PublishedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class ContentAuditLogResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ContentStableId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }


    public class ManagedResourceCreateDto
    {
        public string Kind { get; set; } = "file";
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? SizeBytes { get; set; }
        public string? Source { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool Active { get; set; } = true;
    }

    public class ManagedResourceUpdateDto
    {
        public string? Kind { get; set; }
        public Dictionary<string, string>? Name { get; set; }
        public Dictionary<string, string>? Description { get; set; }
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? SizeBytes { get; set; }
        public string? Source { get; set; }
        public List<string>? Tags { get; set; }
        public bool? Active { get; set; }
    }

    public class ManagedResourceResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "file";
        public Dictionary<string, string> Name { get; set; } = new();
        public Dictionary<string, string> Description { get; set; } = new();
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Source { get; set; } = "managed-upload";
        public List<string> Tags { get; set; } = new();
        public bool Active { get; set; }
        public string CreatedById { get; set; } = string.Empty;
        public string? UpdatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    // ---------------------------------------------------------------------------------------------------------------------
    // FORM SUBMISSIONS
    // ---------------------------------------------------------------------------------------------------------------------

    public class FormSubmitDto
    {
        public Dictionary<string, string> Data { get; set; } = new();
        public string Language { get; set; } = "en";
        public string Honeypot { get; set; } = string.Empty;
        public string? CaptchaToken { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // SETTINGS
    // ---------------------------------------------------------------------------------------------------------------------

    public class LanguageResponseDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool AdminEnabled { get; set; }
        public bool UserEnabled { get; set; }
        public bool IsFallback { get; set; }
        public bool Protected { get; set; }
        public string Direction { get; set; } = "ltr";
        public int Order { get; set; }
    }

    public class LanguageCreateDto
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

    public class SiteSettingsResponseDto
    {
        public List<LanguageResponseDto> Languages { get; set; } = new();
        public string DefaultLanguage { get; set; } = "en";
    }

    public class SiteSettingsUpdateDto
    {
        public List<LanguageCreateDto>? Languages { get; set; }
        public string? DefaultLanguage { get; set; }
    }

    public class AdminAppearanceResponseDto
    {
        public string Preset { get; set; } = "navy-gold";
    }

    public class AdminAppearanceUpdateDto
    {
        public string Preset { get; set; } = "navy-gold";
    }

    public class GlossaryTermResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class GlossaryTermCreateDto
    {
        public string Term { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class GlossaryTermUpdateDto
    {
        public string? Term { get; set; }
        public string? Description { get; set; }
    }
}

