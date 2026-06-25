using FullProject.Models;
using MongoDB.Driver;
using SharedComponents.Helpers;

namespace FullProject.Services
{
    internal static class ManagedResourceReferenceHelper
    {
        public static FilterDefinition<ContentItem> ContentFilter(ManagedResource resource, string? url)
        {
            var filters = new List<FilterDefinition<ContentItem>>
            {
                Builders<ContentItem>.Filter.Eq(c => c.HeroImageResourceId, resource.Id),
                Builders<ContentItem>.Filter.Eq(c => c.ThumbnailResourceId, resource.Id),
                Builders<ContentItem>.Filter.Eq(c => c.VideoResourceId, resource.Id),
                Builders<ContentItem>.Filter.Eq("Attachments.ResourceId", resource.Id),
                Builders<ContentItem>.Filter.Eq("BodyItems.ResourceId", resource.Id),
                Builders<ContentItem>.Filter.Eq("GalleryItems.ResourceId", resource.Id)
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.HeroImageUrl, url));
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.ThumbnailUrl, url));
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.VideoUrl, url));
                filters.Add(Builders<ContentItem>.Filter.Eq("Attachments.Url", url));
                filters.Add(Builders<ContentItem>.Filter.Eq("BodyItems.Url", url));
                filters.Add(Builders<ContentItem>.Filter.Eq("GalleryItems.Url", url));
            }

            return Builders<ContentItem>.Filter.Or(filters);
        }

        public static FilterDefinition<ContentRevision> ContentRevisionFilter(ManagedResource resource, string? url)
        {
            var filters = new List<FilterDefinition<ContentRevision>>
            {
                Builders<ContentRevision>.Filter.Eq("Snapshot.HeroImageResourceId", resource.Id),
                Builders<ContentRevision>.Filter.Eq("Snapshot.ThumbnailResourceId", resource.Id),
                Builders<ContentRevision>.Filter.Eq("Snapshot.VideoResourceId", resource.Id),
                Builders<ContentRevision>.Filter.Eq("Snapshot.Attachments.ResourceId", resource.Id),
                Builders<ContentRevision>.Filter.Eq("Snapshot.BodyItems.ResourceId", resource.Id),
                Builders<ContentRevision>.Filter.Eq("Snapshot.GalleryItems.ResourceId", resource.Id)
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.HeroImageUrl", url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.ThumbnailUrl", url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.VideoUrl", url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.Attachments.Url", url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.BodyItems.Url", url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.GalleryItems.Url", url));
            }

            return Builders<ContentRevision>.Filter.Or(filters);
        }

        public static FilterDefinition<Section> SectionUrlFilter(string url) =>
            Builders<Section>.Filter.Or(
                Builders<Section>.Filter.Eq("Style.BackgroundImageUrl", url),
                Builders<Section>.Filter.Eq("Style.BackgroundVideoUrl", url),
                Builders<Section>.Filter.Eq("ImageUrl", url),
                Builders<Section>.Filter.Eq("Items.ImageUrl", url),
                Builders<Section>.Filter.Eq("ItemOverrides.CardImageUrl", url));

        public static FilterDefinition<Block> BlockUrlFilter(IEnumerable<string> urls)
        {
            var filters = urls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .SelectMany(url => new[]
                {
                    Builders<Block>.Filter.Eq("ImageUrl", url),
                    Builders<Block>.Filter.Eq("FileUrl", url),
                    Builders<Block>.Filter.Eq("EmbedUrl", url)
                })
                .ToList();

            return filters.Count == 0
                ? Builders<Block>.Filter.Where(_ => false)
                : Builders<Block>.Filter.Or(filters);
        }

        public static bool MatchesResource(string? resourceId, string? url, ManagedResource resource) =>
            string.Equals(resourceId, resource.Id, StringComparison.OrdinalIgnoreCase) ||
            SameUrl(url, resource.Url);

        public static bool MatchesManagedReference(string? resourceId, string? url, ManagedResource resource, string oldUrl) =>
            string.Equals(resourceId, resource.Id, StringComparison.OrdinalIgnoreCase) ||
            SameUrl(url, oldUrl);

        public static bool SameUrl(string? value, string? expected) =>
            !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(expected) &&
            string.Equals(value.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

        public static bool MatchesAnyUrl(string? value, IEnumerable<string> urls) =>
            urls.Any(url => SameUrl(value, url));

        public static List<string> ResourceUrlVariants(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new();

            var variants = new List<string> { value.Trim() };
            var embedUrl = VideoUrlHelper.ToEmbedUrl(value);
            if (!string.IsNullOrWhiteSpace(embedUrl) && !variants.Contains(embedUrl, StringComparer.OrdinalIgnoreCase))
                variants.Add(embedUrl);

            return variants;
        }
    }
}
