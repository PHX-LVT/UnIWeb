using FullProject.DTOs;
using FullProject.Models;
using FullProject.Security;
using MongoDB.Bson;
using System.Net;
using System.Text.RegularExpressions;
using static FullProject.Security.ContentSecurityPolicy;

namespace FullProject.Services
{
    public class ContentAssetMetadataService
    {
        public static readonly string[] RequiredLanguages = ["en", "vi", "cn"];
        public const string DirectUploadSource = "DirectUpload";
        public const string ManagedResourceSource = "ManagedResource";
        private const int MaxGalleryItems = 30;

        private readonly ContentSanitizer _contentSanitizer = new();

        public Dictionary<string, string> SanitizeLang(Dictionary<string, string> values) =>
            NormalizeLang(values, false).ToDictionary(kv => kv.Key, kv => _contentSanitizer.SanitizeHtml(kv.Value));

        public List<ContentBodyItem> NormalizeBodyItems(IEnumerable<ContentBodyItemDto>? items, Dictionary<string, string>? fallbackBodyHtml)
        {
            var normalized = (items ?? [])
                .OrderBy(i => i.Order)
                .Take(MaxBodyItems)
                .Select((item, index) => NormalizeBodyItem(item, index))
                .Where(BodyItemHasContent)
                .ToList();

            if (normalized.Count == 0 && fallbackBodyHtml is not null && fallbackBodyHtml.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                normalized.Add(new ContentBodyItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = "text",
                    Content = SanitizeLang(fallbackBodyHtml),
                    Visible = true,
                    Order = 0
                });
            }

            return normalized;
        }

        public ContentBodyItem NormalizeBodyItem(ContentBodyItemDto dto, int index)
        {
            var type = NormalizeBodyType(dto.Type);
            return new ContentBodyItem
            {
                Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id.Trim(),
                Type = type,
                Content = type == "text" ? SanitizeLang(dto.Content) : NormalizeLang(dto.Content, false),
                Caption = NormalizeLang(dto.Caption, false),
                Url = CleanUrl(dto.Url),
                ResourceId = CleanResourceId(dto.ResourceId),
                ResourceSource = NormalizeResourceSource(dto.ResourceSource, dto.ResourceId),
                StorageKey = CleanStorageKey(dto.StorageKey),
                FileName = dto.FileName?.Trim(),
                ContentType = dto.ContentType?.Trim(),
                SizeBytes = Math.Max(0, dto.SizeBytes),
                Style = string.IsNullOrWhiteSpace(dto.Style) ? null : dto.Style.Trim(),
                Visible = dto.Visible,
                Order = index
            };
        }

        public List<ContentGalleryItem> NormalizeGalleryItems(IEnumerable<ContentGalleryItemDto>? items) =>
            (items ?? [])
                .OrderBy(i => i.Order)
                .Take(MaxGalleryItems)
                .Select((item, index) => NormalizeGalleryItem(item, index))
                .Where(GalleryItemHasContent)
                .ToList();

        public ContentGalleryItem NormalizeGalleryItem(ContentGalleryItemDto dto, int index) => new()
        {
            Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id.Trim(),
            Kind = NormalizeGalleryKind(dto.Kind),
            Url = CleanUrl(dto.Url),
            ThumbnailUrl = CleanUrl(dto.ThumbnailUrl),
            ResourceId = CleanResourceId(dto.ResourceId),
            ResourceSource = NormalizeResourceSource(dto.ResourceSource, dto.ResourceId),
            StorageKey = CleanStorageKey(dto.StorageKey),
            Caption = NormalizeLang(dto.Caption, false),
            Visible = dto.Visible,
            Order = index
        };

        public Dictionary<string, string> BuildBodyHtmlMirror(List<ContentBodyItem> items, Dictionary<string, string>? fallbackBodyHtml)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in RequiredLanguages)
            {
                var html = string.Join(Environment.NewLine, items
                    .Where(i => i.Visible)
                    .OrderBy(i => i.Order)
                    .Select(i => RenderBodyItemHtml(i, lang))
                    .Where(h => !string.IsNullOrWhiteSpace(h)));

                result[lang] = string.IsNullOrWhiteSpace(html)
                    ? fallbackBodyHtml?.GetValueOrDefault(lang, string.Empty) ?? string.Empty
                    : html;
            }

            return result;
        }

        public List<ContentAttachment> NormalizeAttachments(IEnumerable<ContentAttachmentDto> attachments) =>
            attachments
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .Take(MaxAttachments)
                .Select(a => new ContentAttachment
                {
                    Id = string.IsNullOrWhiteSpace(a.Id) ? Guid.NewGuid().ToString("N") : a.Id,
                    FileName = a.FileName.Trim(),
                    Url = CleanUrl(a.Url) ?? string.Empty,
                    ResourceId = CleanResourceId(a.ResourceId),
                    ResourceSource = NormalizeResourceSource(a.ResourceSource, a.ResourceId),
                    StorageKey = CleanStorageKey(a.StorageKey),
                    ContentType = a.ContentType.Trim(),
                    SizeBytes = Math.Max(0, a.SizeBytes)
                })
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .ToList();

        public static string NormalizeBodyType(string? type)
        {
            var value = (type ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                "image" => "image",
                "video" => "video",
                "file" => "file",
                "quote" => "quote",
                "cta" => "cta",
                "divider" => "divider",
                _ => "text"
            };
        }

        public static string NormalizeGalleryKind(string? kind)
        {
            var value = (kind ?? string.Empty).Trim().ToLowerInvariant();
            return value is "video" or "webinar" ? "video" : "image";
        }

        public static Dictionary<string, string> NormalizeLang(Dictionary<string, string> values, bool requireFallback = true)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var en = values.GetValueOrDefault("en", string.Empty).Trim();
            foreach (var lang in RequiredLanguages)
            {
                var value = values.GetValueOrDefault(lang, string.Empty).Trim();
                result[lang] = requireFallback && string.IsNullOrWhiteSpace(value) ? en : value;
            }
            return result;
        }

        public static List<string> NormalizeTags(IEnumerable<string> tags) =>
            tags.Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

        public static string NormalizeKey(string value) =>
            NormalizeSlug(value, "content-type");

        public static string NormalizeContentTypeKey(string? key, string? name) =>
            NormalizeSlug(string.IsNullOrWhiteSpace(key) ? name : key, "content-type");

        public static string NormalizeRouteType(string value)
        {
            var normalized = NormalizeKey(value);
            return normalized switch
            {
                "articles" => "article",
                "case-studies" => "case-study",
                "whitepapers" => "whitepaper",
                "reports" => "whitepaper",
                "videos" => "video",
                "webinars" => "video",
                "tools" => "tool",
                _ => normalized
            };
        }

        public static string NormalizeSlug(string? value, string fallback)
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
            var slug = Regex.Replace(source.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "content" : slug;
        }

        public static string? CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("/", StringComparison.Ordinal)
                ? trimmed
                : null;
        }

        public static string? CleanResourceId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return ObjectId.TryParse(trimmed, out _) ? trimmed : null;
        }

        public static string? CleanStorageKey(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string NormalizeResourceSource(string? source, string? resourceId)
        {
            if (!string.IsNullOrWhiteSpace(resourceId) && ObjectId.TryParse(resourceId.Trim(), out _))
                return ManagedResourceSource;

            var normalized = (source ?? string.Empty).Trim();
            return string.Equals(normalized, ManagedResourceSource, StringComparison.OrdinalIgnoreCase)
                ? ManagedResourceSource
                : DirectUploadSource;
        }

        public static string NormalizeClickBehavior(string? value)
        {
            var normalized = (value ?? "detail").Trim().ToLowerInvariant();
            return normalized is "detail" or "download" or "video" or "external" or "image" ? normalized : "detail";
        }

        public static string NormalizeContentTypeBehavior(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
            return normalized switch
            {
                "page" or "detail" or "article" => "page",
                "file" or "fileresource" or "file-resource" or "download" or "resource" => "file-resource",
                "video" or "videoresource" or "video-resource" or "webinar" => "video-resource",
                "image" or "imageresource" or "image-resource" or "photo" or "picture" => "image-resource",
                "gallery" or "imagegallery" or "image-gallery" or "photogallery" or "photo-gallery" or "mediagallery" or "media-gallery" => "gallery",
                _ => string.Empty
            };
        }

        public static string ResolveContentBehavior(
            string? behavior,
            string? key,
            bool requiresBody,
            bool requiresFile,
            bool requiresVideoUrl,
            string? clickBehavior)
        {
            var normalizedBehavior = NormalizeContentTypeBehavior(behavior);
            if (!string.IsNullOrWhiteSpace(normalizedBehavior))
                return normalizedBehavior;

            var normalizedKey = NormalizeKey(key ?? string.Empty);
            if (normalizedKey is "article" or "articles" or "case-study" or "case-studies")
                return "page";
            if (normalizedKey.Contains("gallery", StringComparison.OrdinalIgnoreCase))
                return "gallery";
            if (normalizedKey.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("photo", StringComparison.OrdinalIgnoreCase))
                return "image-resource";
            if (normalizedKey.Contains("video", StringComparison.OrdinalIgnoreCase) || normalizedKey.Contains("webinar", StringComparison.OrdinalIgnoreCase))
                return "video-resource";
            if (normalizedKey.Contains("whitepaper", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("report", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("template", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("disclosure", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("charter", StringComparison.OrdinalIgnoreCase) ||
                normalizedKey.Contains("regulation", StringComparison.OrdinalIgnoreCase))
                return "file-resource";

            var click = NormalizeClickBehavior(clickBehavior);
            if (click == "image") return "image-resource";
            if (requiresVideoUrl || click == "video") return "video-resource";
            if (requiresFile || click == "download") return "file-resource";
            return requiresBody ? "page" : "file-resource";
        }

        public static ContentTypeWorkflow NormalizeContentTypeWorkflow(string behavior, bool requiresHeroImage)
        {
            return NormalizeContentTypeBehavior(behavior) switch
            {
                "video-resource" => new ContentTypeWorkflow(false, false, false, true, false, "video"),
                "image-resource" or "gallery" => new ContentTypeWorkflow(false, false, false, false, false, "image"),
                "file-resource" => new ContentTypeWorkflow(false, false, true, false, true, "download"),
                _ => new ContentTypeWorkflow(true, requiresHeroImage, false, false, true, "detail")
            };
        }

        public static string? CleanTemplateKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return NormalizeSlug(value, "default");
        }

        public static Dictionary<string, string> Lang(string value) => new()
        {
            ["en"] = value,
            ["vi"] = value,
            ["cn"] = value
        };

        public static ContentBodyItemDto ToDto(ContentBodyItem item) => new()
        {
            Id = item.Id,
            Type = item.Type,
            Content = new(item.Content),
            Caption = new(item.Caption),
            Url = item.Url,
            ResourceId = item.ResourceId,
            ResourceSource = item.ResourceSource,
            StorageKey = item.StorageKey,
            FileName = item.FileName,
            ContentType = item.ContentType,
            SizeBytes = item.SizeBytes,
            Style = item.Style,
            Visible = item.Visible,
            Order = item.Order
        };

        public static ContentGalleryItemDto ToDto(ContentGalleryItem item) => new()
        {
            Id = item.Id,
            Kind = item.Kind,
            Url = item.Url,
            ThumbnailUrl = item.ThumbnailUrl,
            ResourceId = item.ResourceId,
            ResourceSource = item.ResourceSource,
            StorageKey = item.StorageKey,
            Caption = new(item.Caption),
            Visible = item.Visible,
            Order = item.Order
        };

        public static ContentAttachmentDto ToDto(ContentAttachment attachment) => new()
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            Url = attachment.Url,
            ResourceId = attachment.ResourceId,
            ResourceSource = attachment.ResourceSource,
            StorageKey = attachment.StorageKey,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes
        };

        private static bool BodyItemHasContent(ContentBodyItem item) =>
            item.Type == "divider" ||
            item.Content.Values.Any(v => !string.IsNullOrWhiteSpace(v)) ||
            item.Caption.Values.Any(v => !string.IsNullOrWhiteSpace(v)) ||
            !string.IsNullOrWhiteSpace(item.Url);

        private static bool GalleryItemHasContent(ContentGalleryItem item) =>
            !string.IsNullOrWhiteSpace(item.Url);

        private static string RenderBodyItemHtml(ContentBodyItem item, string lang)
        {
            var content = LangValue(item.Content, lang, string.Empty);
            var caption = LangValue(item.Caption, lang, string.Empty);
            var url = item.Url ?? string.Empty;
            var fileName = !string.IsNullOrWhiteSpace(item.FileName) ? item.FileName! : "Download";

            return item.Type switch
            {
                "image" when !string.IsNullOrWhiteSpace(url) =>
                    $"<figure class=\"sc-content-body-image\"><img src=\"{H(url)}\" alt=\"{H(caption)}\" />{RenderCaption(caption)}</figure>",
                "video" when !string.IsNullOrWhiteSpace(url) =>
                    $"<div class=\"sc-content-body-video\"><iframe src=\"{H(ToEmbedVideoUrl(url))}\" title=\"{H(caption)}\" loading=\"lazy\" allowfullscreen></iframe>{RenderCaption(caption)}</div>",
                "file" when !string.IsNullOrWhiteSpace(url) =>
                    $"<p><a class=\"sc-insight-download\" href=\"{H(url)}\" target=\"_blank\" rel=\"noopener\"><span><strong>{H(fileName)}</strong>{RenderFileMeta(item)}</span><b>Download</b></a></p>",
                "quote" when !string.IsNullOrWhiteSpace(content) =>
                    $"<blockquote class=\"sc-content-body-quote\">{H(content)}</blockquote>",
                "cta" when !string.IsNullOrWhiteSpace(content) =>
                    $"<div class=\"sc-content-body-cta\">{content}</div>",
                "divider" => "<hr class=\"sc-content-body-divider\" />",
                _ => PlainTextToParagraphHtml(content)
            };
        }

        private static string RenderCaption(string caption) =>
            string.IsNullOrWhiteSpace(caption) ? string.Empty : $"<figcaption>{H(caption)}</figcaption>";

        private static string RenderFileMeta(ContentBodyItem item)
        {
            var parts = new[] { FormatBytes(item.SizeBytes), item.ContentType }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            var meta = string.Join(" / ", parts);
            return string.IsNullOrWhiteSpace(meta) ? string.Empty : $"<small>{H(meta)}</small>";
        }

        private static string ToEmbedVideoUrl(string url)
        {
            if (url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase))
                return url;

            var match = Regex.Match(url, @"(?:youtube\.com/watch\?v=|youtu\.be/)([A-Za-z0-9_-]{6,})", RegexOptions.IgnoreCase);
            return match.Success ? $"https://www.youtube.com/embed/{match.Groups[1].Value}" : url;
        }

        private static string PlainTextToParagraphHtml(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (ContainsHtmlMarkup(value))
                return value;

            var paragraphs = Regex.Split(value.Trim(), @"(?:\r?\n){2,}")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => $"<p>{H(p).Replace("\n", "<br />")}</p>");

            return string.Join(Environment.NewLine, paragraphs);
        }

        private static bool ContainsHtmlMarkup(string value) =>
            Regex.IsMatch(
                value,
                @"<\s*(p|h[1-6]|ul|ol|li|blockquote|div|figure|figcaption|table|thead|tbody|tr|td|th|br|span|strong|b|em|i|u|a)\b",
                RegexOptions.IgnoreCase);

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return string.Empty;
            if (bytes < 1024 * 1024) return $"{Math.Max(1, bytes / 1024)} KB";
            return $"{bytes / 1024d / 1024d:0.0} MB";
        }

        private static string LangValue(Dictionary<string, string> values, string lang, string fallback)
        {
            if (values.TryGetValue(lang, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
            if (values.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
                return en;
            return values.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? fallback;
        }

        private static string H(string value) => WebUtility.HtmlEncode(value);
    }
}
