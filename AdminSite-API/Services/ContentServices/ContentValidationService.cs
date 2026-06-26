using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;
using static FullProject.Security.ContentSecurityPolicy;

namespace FullProject.Services
{
    public class ContentValidationService
    {
        private const int MaxGalleryItems = 30;
        private readonly ContentTypeService _contentTypes;

        public ContentValidationService(ContentTypeService contentTypes)
        {
            _contentTypes = contentTypes;
        }

        public async Task<List<string>> ValidateCreateAsync(ContentCreateDto dto)
        {
            var errors = new List<string>();
            if (!await _contentTypes.TypeExistsAsync(dto.ContentTypeKey)) errors.Add("Content type does not exist.");
            if (string.IsNullOrWhiteSpace(dto.Title.GetValueOrDefault("en"))) errors.Add("English title is required.");
            ValidateContentFields(
                dto.Title,
                dto.Summary,
                dto.BodyHtml,
                dto.BodyItems,
                dto.GalleryItems,
                dto.Tags,
                dto.Attachments,
                dto.HeroImageUrl,
                dto.HeroImageResourceId,
                dto.HeroImageResourceSource,
                dto.ThumbnailUrl,
                dto.ThumbnailResourceId,
                dto.ThumbnailResourceSource,
                dto.VideoUrl,
                dto.VideoResourceId,
                dto.VideoResourceSource,
                dto.ExternalUrl,
                errors);
            return errors;
        }

        public async Task<List<string>> ValidateUpdateAsync(ContentItem existing, ContentUpdateDto dto, string typeKey)
        {
            var errors = new List<string>();
            if (!await _contentTypes.TypeExistsAsync(typeKey)) errors.Add("Content type does not exist.");
            ValidateContentFields(
                dto.Title ?? existing.Title,
                dto.Summary ?? existing.Summary,
                dto.BodyHtml ?? existing.BodyHtml,
                dto.BodyItems ?? existing.BodyItems.Select(ContentAssetMetadataService.ToDto).ToList(),
                dto.GalleryItems ?? existing.GalleryItems.Select(ContentAssetMetadataService.ToDto).ToList(),
                dto.Tags ?? existing.Tags,
                dto.Attachments ?? existing.Attachments.Select(ContentAssetMetadataService.ToDto).ToList(),
                dto.HeroImageUrl ?? existing.HeroImageUrl,
                dto.HeroImageResourceId ?? existing.HeroImageResourceId,
                dto.HeroImageResourceSource ?? existing.HeroImageResourceSource,
                dto.ThumbnailUrl ?? existing.ThumbnailUrl,
                dto.ThumbnailResourceId ?? existing.ThumbnailResourceId,
                dto.ThumbnailResourceSource ?? existing.ThumbnailResourceSource,
                dto.VideoUrl ?? existing.VideoUrl,
                dto.VideoResourceId ?? existing.VideoResourceId,
                dto.VideoResourceSource ?? existing.VideoResourceSource,
                dto.ExternalUrl ?? existing.ExternalUrl,
                errors);
            return errors;
        }

        public async Task<List<string>> ValidatePublishAsync(ContentItem item)
        {
            var errors = new List<string>();
            var type = await _contentTypes.GetByKeyAsync(item.ContentTypeKey);
            if (type is null) errors.Add("Content type does not exist.");
            if (string.IsNullOrWhiteSpace(item.Title.GetValueOrDefault("en"))) errors.Add("English title is required.");
            if (type?.RequiresBody == true && string.IsNullOrWhiteSpace(item.BodyHtml.GetValueOrDefault("en"))) errors.Add("Body is required.");
            if (type?.RequiresHeroImage == true && string.IsNullOrWhiteSpace(item.HeroImageUrl)) errors.Add("Hero image is required.");
            if (type?.RequiresFile == true && item.Attachments.Count == 0) errors.Add("File attachment is required.");
            if (type?.RequiresVideoUrl == true && string.IsNullOrWhiteSpace(item.VideoUrl)) errors.Add("Video URL is required.");

            var behavior = ContentAssetMetadataService.NormalizeContentTypeBehavior(type?.Behavior);
            if (behavior == "image-resource" && string.IsNullOrWhiteSpace(item.ThumbnailUrl) && string.IsNullOrWhiteSpace(item.HeroImageUrl))
                errors.Add("Image resource requires an image.");
            if (behavior == "gallery" && !item.GalleryItems.Any(i => i.Visible && !string.IsNullOrWhiteSpace(i.Url)))
                errors.Add("Gallery requires at least one gallery item.");
            if (type?.AllowsAttachments == false && item.Attachments.Count > 0) errors.Add("This content type does not allow attachments.");
            return errors;
        }

        private static void ValidateContentFields(
            Dictionary<string, string> title,
            Dictionary<string, string> summary,
            Dictionary<string, string> bodyHtml,
            List<ContentBodyItemDto> bodyItems,
            List<ContentGalleryItemDto> galleryItems,
            List<string> tags,
            List<ContentAttachmentDto> attachments,
            string? heroImageUrl,
            string? heroImageResourceId,
            string? heroImageResourceSource,
            string? thumbnailUrl,
            string? thumbnailResourceId,
            string? thumbnailResourceSource,
            string? videoUrl,
            string? videoResourceId,
            string? videoResourceSource,
            string? externalUrl,
            List<string> errors)
        {
            if (title.Values.Any(v => v?.Length > MaxTitleLength)) errors.Add($"Title must be {MaxTitleLength} characters or fewer per language.");
            if (summary.Values.Any(v => v?.Length > MaxSummaryLength)) errors.Add($"Summary must be {MaxSummaryLength} characters or fewer per language.");
            if (bodyHtml.Values.Any(v => v?.Length > MaxBodyHtmlLength)) errors.Add("Body HTML is too large.");
            if (bodyItems.Count > MaxBodyItems) errors.Add($"A content item can have at most {MaxBodyItems} body items.");
            if (bodyItems.SelectMany(i => i.Content.Values).Any(v => v?.Length > MaxBodyItemLength)) errors.Add($"Each body item must be {MaxBodyItemLength} characters or fewer per language.");
            if (galleryItems.Count > MaxGalleryItems) errors.Add($"A gallery can have at most {MaxGalleryItems} items.");
            if (galleryItems.SelectMany(i => i.Caption.Values).Any(v => v?.Length > MaxSummaryLength)) errors.Add($"Gallery captions must be {MaxSummaryLength} characters or fewer per language.");
            if (tags.Count > MaxTags) errors.Add($"A content item can have at most {MaxTags} tags.");
            if (tags.Any(t => t.Length > MaxTagLength)) errors.Add($"Tags must be {MaxTagLength} characters or fewer.");
            if (attachments.Count > MaxAttachments) errors.Add($"A content item can have at most {MaxAttachments} attachments.");

            ValidateOptionalUrl(heroImageUrl, "Hero image URL", errors);
            ValidateResourceReference(heroImageResourceId, heroImageResourceSource, "Hero image", errors);
            ValidateOptionalUrl(thumbnailUrl, "Thumbnail URL", errors);
            ValidateResourceReference(thumbnailResourceId, thumbnailResourceSource, "Thumbnail", errors);
            ValidateOptionalUrl(externalUrl, "External URL", errors);
            ValidateResourceReference(videoResourceId, videoResourceSource, "Video", errors);
            if (!string.IsNullOrWhiteSpace(videoUrl) && !IsAllowedVideoUrl(videoUrl)) errors.Add("Video URL must be a YouTube URL.");

            foreach (var attachment in attachments)
            {
                ValidateOptionalUrl(attachment.Url, "Attachment URL", errors);
                ValidateResourceReference(attachment.ResourceId, attachment.ResourceSource, "Attachment", errors);
                if (!IsAllowedAttachment(attachment.FileName, attachment.ContentType))
                    errors.Add("Attachments must be PDF, Word, Excel, PowerPoint, or plain text files.");
            }

            foreach (var item in bodyItems)
            {
                var type = ContentAssetMetadataService.NormalizeBodyType(item.Type);
                if (type is "image" or "file") ValidateOptionalUrl(item.Url, "Body item URL", errors);
                if (type is "image" or "file" or "video") ValidateResourceReference(item.ResourceId, item.ResourceSource, "Body media item", errors);
                if (type == "video" && !string.IsNullOrWhiteSpace(item.Url) && !IsAllowedVideoUrl(item.Url)) errors.Add("Body video URL must be a YouTube URL.");
                if (type == "file" && !IsAllowedAttachment(item.FileName ?? string.Empty, item.ContentType ?? string.Empty))
                    errors.Add("Body file items must be PDF, Word, Excel, PowerPoint, or plain text files.");
            }

            foreach (var item in galleryItems)
            {
                var kind = ContentAssetMetadataService.NormalizeGalleryKind(item.Kind);
                ValidateOptionalUrl(item.Url, "Gallery item URL", errors);
                ValidateOptionalUrl(item.ThumbnailUrl, "Gallery item thumbnail URL", errors);
                ValidateResourceReference(item.ResourceId, item.ResourceSource, "Gallery item", errors);
                if (kind == "video" && !string.IsNullOrWhiteSpace(item.Url) && !IsAllowedVideoUrl(item.Url)) errors.Add("Gallery video URL must be a YouTube URL.");
            }
        }

        private static void ValidateResourceReference(string? resourceId, string? resourceSource, string label, List<string> errors)
        {
            var hasResourceId = !string.IsNullOrWhiteSpace(resourceId);
            var isManagedResource = string.Equals(resourceSource, ContentAssetMetadataService.ManagedResourceSource, StringComparison.OrdinalIgnoreCase);
            if (!hasResourceId && !isManagedResource)
                return;

            if (!hasResourceId || !ObjectId.TryParse(resourceId!.Trim(), out _))
                errors.Add($"{label} managed resource reference is missing or invalid.");
        }
    }
}
