using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson;

namespace FullProject.Services
{
    public class ContentMappingService
    {
        public ContentTypeResponseDto MapType(ContentType type) => new()
        {
            Id = type.Id,
            Key = type.Key,
            Name = type.Name,
            Description = type.Description,
            Behavior = type.Behavior,
            RequiresBody = type.RequiresBody,
            RequiresHeroImage = type.RequiresHeroImage,
            RequiresFile = type.RequiresFile,
            RequiresVideoUrl = type.RequiresVideoUrl,
            AllowsAttachments = type.AllowsAttachments,
            ClickBehavior = type.ClickBehavior,
            Visible = type.Visible,
            Order = type.Order,
            CreatedAt = type.CreatedAt,
            UpdatedAt = type.UpdatedAt
        };

        public ContentResponseDto MapItem(ContentItem item) => new()
        {
            Id = item.Id,
            StableId = item.StableId,
            ContentTypeKey = item.ContentTypeKey,
            Slug = item.Slug,
            Title = item.Title,
            Summary = item.Summary,
            BodyHtml = item.BodyHtml,
            BodyItems = item.BodyItems.Select(ContentAssetMetadataService.ToDto).ToList(),
            GalleryItems = item.GalleryItems.Select(ContentAssetMetadataService.ToDto).ToList(),
            HeroImageUrl = item.HeroImageUrl,
            HeroImageResourceId = item.HeroImageResourceId,
            HeroImageResourceSource = item.HeroImageResourceSource,
            HeroImageStorageKey = item.HeroImageStorageKey,
            HeroImageAlt = item.HeroImageAlt,
            ThumbnailUrl = item.ThumbnailUrl,
            ThumbnailResourceId = item.ThumbnailResourceId,
            ThumbnailResourceSource = item.ThumbnailResourceSource,
            ThumbnailStorageKey = item.ThumbnailStorageKey,
            VideoUrl = item.VideoUrl,
            VideoResourceId = item.VideoResourceId,
            VideoResourceSource = item.VideoResourceSource,
            VideoStorageKey = item.VideoStorageKey,
            ExternalUrl = item.ExternalUrl,
            TemplateKey = item.TemplateKey,
            Tags = item.Tags,
            Attachments = item.Attachments.Select(ContentAssetMetadataService.ToDto).ToList(),
            Status = item.Status,
            Visible = item.Visible,
            AuthorId = item.AuthorId,
            UpdatedById = item.UpdatedById,
            PublishedById = item.PublishedById,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            SubmittedAt = item.SubmittedAt,
            PublishedAt = item.PublishedAt
        };

        public ContentAuditLogResponseDto MapLog(ContentAuditLog log) => new()
        {
            Id = log.Id,
            ContentStableId = log.ContentStableId,
            Action = log.Action,
            ActorId = log.ActorId,
            Message = log.Message,
            CreatedAt = log.CreatedAt
        };

        public RevisionResponseDto MapRevision(ContentRevision revision) => new()
        {
            Id = revision.Id,
            TargetId = revision.ContentId,
            StableId = revision.ContentStableId,
            SourceUpdatedAt = revision.SourceUpdatedAt,
            ActorId = revision.ActorId,
            Reason = revision.Reason,
            CreatedAt = revision.CreatedAt
        };

        public ContentItem CloneForPublished(ContentItem item, string actorId, DateTime publishedAt) => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            StableId = item.StableId,
            ContentTypeKey = item.ContentTypeKey,
            Slug = item.Slug,
            Title = new(item.Title),
            Summary = new(item.Summary),
            BodyHtml = new(item.BodyHtml),
            BodyItems = item.BodyItems.Select(CloneBodyItem).ToList(),
            GalleryItems = item.GalleryItems.Select(CloneGalleryItem).ToList(),
            HeroImageUrl = item.HeroImageUrl,
            HeroImageResourceId = item.HeroImageResourceId,
            HeroImageResourceSource = item.HeroImageResourceSource,
            HeroImageStorageKey = item.HeroImageStorageKey,
            HeroImageAlt = item.HeroImageAlt,
            ThumbnailUrl = item.ThumbnailUrl,
            ThumbnailResourceId = item.ThumbnailResourceId,
            ThumbnailResourceSource = item.ThumbnailResourceSource,
            ThumbnailStorageKey = item.ThumbnailStorageKey,
            VideoUrl = item.VideoUrl,
            VideoResourceId = item.VideoResourceId,
            VideoResourceSource = item.VideoResourceSource,
            VideoStorageKey = item.VideoStorageKey,
            ExternalUrl = item.ExternalUrl,
            TemplateKey = item.TemplateKey,
            Tags = item.Tags.ToList(),
            Attachments = item.Attachments.Select(a => new ContentAttachment
            {
                Id = a.Id,
                FileName = a.FileName,
                Url = a.Url,
                ResourceId = a.ResourceId,
                ResourceSource = a.ResourceSource,
                StorageKey = a.StorageKey,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes
            }).ToList(),
            Status = ContentStatus.Published,
            Visible = item.Visible,
            AuthorId = item.AuthorId,
            UpdatedById = actorId,
            PublishedById = actorId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = publishedAt,
            SubmittedAt = item.SubmittedAt,
            PublishedAt = publishedAt
        };

        private static ContentBodyItem CloneBodyItem(ContentBodyItem item) => new()
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

        private static ContentGalleryItem CloneGalleryItem(ContentGalleryItem item) => new()
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
    }
}
