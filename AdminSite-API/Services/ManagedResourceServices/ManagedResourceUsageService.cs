using FullProject.Data;
using FullProject.DTOs;
using FullProject.Models;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SharedComponents.Helpers;

namespace FullProject.Services
{
    public class ManagedResourceUsageService
    {
        private readonly MongoDbContext _context;

        public ManagedResourceUsageService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, int>> GetUsageCountsAsync(IEnumerable<ManagedResource> resources)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var resource in resources.Where(r => !string.IsNullOrWhiteSpace(r.Id)))
            {
                var usage = await GetUsageAsync(resource);
                result[resource.Id] = usage.UsageCount;
            }

            return result;
        }

        public async Task<ManagedResourceUsageDto> GetUsageAsync(ManagedResource resource)
        {
            var references = new List<ManagedResourceUsageReferenceDto>();
            references.AddRange(await GetContentUsageAsync(_context.ContentDraft, resource, "Content Draft"));
            references.AddRange(await GetContentUsageAsync(_context.ContentPublished, resource, "Content Published"));
            references.AddRange(await GetContentRevisionUsageAsync(resource));
            references.AddRange(await GetPageUsageAsync(_context.PagesDraft, resource, "Page Draft"));
            references.AddRange(await GetPageUsageAsync(_context.PagesPublished, resource, "Page Published"));
            references.AddRange(await GetSectionUsageAsync(_context.SectionsDraft, resource, "Section Draft"));
            references.AddRange(await GetSectionUsageAsync(_context.SectionsPublished, resource, "Section Published"));
            references.AddRange(await GetBlockUsageAsync(_context.BlocksDraft, resource, "Block Draft"));
            references.AddRange(await GetBlockUsageAsync(_context.BlocksPublished, resource, "Block Published"));
            references.AddRange(await GetBrandingUsageAsync(resource));

            return new ManagedResourceUsageDto
            {
                ResourceId = resource.Id,
                UsageCount = references.Count,
                References = references
                    .OrderBy(r => r.Source)
                    .ThenBy(r => r.Title)
                    .ThenBy(r => r.Field)
                    .ToList()
            };
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetContentUsageAsync(
            IMongoCollection<ContentItem> collection,
            ManagedResource resource,
            string source)
        {
            var filter = ContentUsageFilter(resource);
            var items = await collection.Find(filter).ToListAsync();
            return items.SelectMany(item => BuildContentReferences(item, resource, source)).ToList();
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetContentRevisionUsageAsync(ManagedResource resource)
        {
            var revisions = await _context.ContentRevisions
                .Find(ContentRevisionUsageFilter(resource))
                .ToListAsync();

            var references = new List<ManagedResourceUsageReferenceDto>();
            foreach (var revision in revisions)
            {
                ContentItem? snapshot;
                try
                {
                    snapshot = BsonSerializer.Deserialize<ContentItem>(revision.Snapshot);
                }
                catch
                {
                    continue;
                }
                if (snapshot is null) continue;

                var snapshotRefs = BuildContentReferences(snapshot, resource, "Content Revision")
                    .Select(r =>
                    {
                        r.ItemId = revision.ContentId;
                        r.StableId = revision.ContentStableId;
                        if (!string.IsNullOrWhiteSpace(revision.Reason))
                        {
                            r.Detail = string.IsNullOrWhiteSpace(r.Detail)
                                ? revision.Reason
                                : $"{r.Detail} ({revision.Reason})";
                        }
                        r.UpdatedAt = revision.CreatedAt;
                        return r;
                    });
                references.AddRange(snapshotRefs);
            }

            return references;
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetPageUsageAsync(
            IMongoCollection<Page> collection,
            ManagedResource resource,
            string source)
        {
            if (string.IsNullOrWhiteSpace(resource.Url)) return new();
            var pages = await collection.Find(p => p.Card != null && p.Card.CardImageUrl == resource.Url).ToListAsync();
            return pages.Select(page => UsageRef(resource, source, page.Id, page.StableId, FirstText(page.Name), "Card image", string.Empty, page.UpdatedAt, page.Status.ToString())).ToList();
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetSectionUsageAsync(
            IMongoCollection<Section> collection,
            ManagedResource resource,
            string source)
        {
            if (string.IsNullOrWhiteSpace(resource.Url)) return new();
            var sections = await collection.Find(SectionUsageFilter(resource.Url)).ToListAsync();
            var references = new List<ManagedResourceUsageReferenceDto>();

            foreach (var section in sections)
            {
                if (MatchesUrl(section.Style.BackgroundImageUrl, resource.Url))
                    references.Add(UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Background image", string.Empty, section.UpdatedAt));
                if (MatchesUrl(section.Style.BackgroundVideoUrl, resource.Url))
                    references.Add(UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Background video", string.Empty, section.UpdatedAt));

                switch (section)
                {
                    case HeroSection hero when MatchesUrl(hero.ImageUrl, resource.Url):
                        references.Add(UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Hero image", string.Empty, section.UpdatedAt));
                        break;
                    case ListSection list:
                        references.AddRange(list.Items
                            .Where(item => MatchesUrl(item.ImageUrl, resource.Url))
                            .Select(item => UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "List item image", FirstText(item.Title), section.UpdatedAt)));
                        break;
                    case CarouselSection carousel:
                        references.AddRange(carousel.Items
                            .Where(item => MatchesUrl(item.ImageUrl, resource.Url))
                            .Select(item => UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Carousel item image", FirstText(item.Title), section.UpdatedAt)));
                        break;
                    case TestimonialSection testimonial:
                        references.AddRange(testimonial.Items
                            .Where(item => MatchesUrl(item.ImageUrl, resource.Url))
                            .Select(item => UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Testimonial image", FirstText(item.Title), section.UpdatedAt)));
                        break;
                    case ShowcaseSection showcase:
                        references.AddRange(showcase.ItemOverrides
                            .Where(item => MatchesUrl(item.CardImageUrl, resource.Url))
                            .Select(item => UsageRef(resource, source, section.Id, section.StableId, SectionTitle(section), "Showcase override image", FirstText(item.CardTitle), section.UpdatedAt)));
                        break;
                }
            }

            return references;
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetBlockUsageAsync(
            IMongoCollection<Block> collection,
            ManagedResource resource,
            string source)
        {
            if (string.IsNullOrWhiteSpace(resource.Url)) return new();
            var blocks = await collection.Find(BlockUsageFilter(resource)).ToListAsync();
            var references = new List<ManagedResourceUsageReferenceDto>();

            foreach (var block in blocks)
            {
                switch (block)
                {
                    case ImageBlock image when MatchesUrl(image.ImageUrl, resource.Url):
                        references.Add(UsageRef(resource, source, block.Id, block.StableId, FirstText(image.AltText), "Image block", string.Empty, block.UpdatedAt));
                        break;
                    case FileBlock file when MatchesUrl(file.FileUrl, resource.Url):
                        references.Add(UsageRef(resource, source, block.Id, block.StableId, file.Filename, "File block", file.FileType, block.UpdatedAt));
                        break;
                    case VideoBlock video when MatchesAnyUrl(video.EmbedUrl, ResourceUrlVariants(resource.Url)):
                        references.Add(UsageRef(resource, source, block.Id, block.StableId, FirstText(video.Title), "Video block", string.Empty, block.UpdatedAt));
                        break;
                    case CardBlock card when MatchesUrl(card.ImageUrl, resource.Url):
                        references.Add(UsageRef(resource, source, block.Id, block.StableId, FirstText(card.Title), "Card block image", string.Empty, block.UpdatedAt));
                        break;
                }
            }

            return references;
        }

        private async Task<List<ManagedResourceUsageReferenceDto>> GetBrandingUsageAsync(ManagedResource resource)
        {
            if (string.IsNullOrWhiteSpace(resource.Url)) return new();
            var brands = await _context.Branding.Find(b => b.LogoUrl == resource.Url).ToListAsync();
            return brands.Select(brand => UsageRef(resource, "Branding", brand.Id, brand.Id, brand.CompanyName, "Logo", string.Empty, null)).ToList();
        }

        private static IEnumerable<ManagedResourceUsageReferenceDto> BuildContentReferences(
            ContentItem item,
            ManagedResource resource,
            string source)
        {
            if (MatchesResource(item.HeroImageResourceId, item.HeroImageUrl, resource))
                yield return ContentRef(item, resource, source, "Hero image", string.Empty);
            if (MatchesResource(item.ThumbnailResourceId, item.ThumbnailUrl, resource))
                yield return ContentRef(item, resource, source, "Thumbnail", string.Empty);
            if (MatchesResource(item.VideoResourceId, item.VideoUrl, resource))
                yield return ContentRef(item, resource, source, "Video", string.Empty);

            foreach (var attachment in item.Attachments.Where(a => MatchesResource(a.ResourceId, a.Url, resource)))
                yield return ContentRef(item, resource, source, "Attachment", attachment.FileName);

            foreach (var bodyItem in item.BodyItems.Where(b => MatchesResource(b.ResourceId, b.Url, resource)))
                yield return ContentRef(item, resource, source, "Body media", bodyItem.FileName ?? FirstText(bodyItem.Caption));

            foreach (var galleryItem in item.GalleryItems.Where(g => MatchesResource(g.ResourceId, g.Url, resource)))
                yield return ContentRef(item, resource, source, "Gallery item", FirstText(galleryItem.Caption));
        }

        private static ManagedResourceUsageReferenceDto ContentRef(
            ContentItem item,
            ManagedResource resource,
            string source,
            string field,
            string detail) =>
            UsageRef(resource, source, item.Id, item.StableId, FirstText(item.Title), field, detail, item.UpdatedAt, item.Status.ToString());

        private static ManagedResourceUsageReferenceDto UsageRef(
            ManagedResource resource,
            string source,
            string itemId,
            string stableId,
            string title,
            string field,
            string detail,
            DateTime? updatedAt,
            string status = "") => new()
            {
                ResourceId = resource.Id,
                Source = source,
                ItemId = itemId,
                StableId = stableId,
                Title = title,
                Field = field,
                Detail = detail,
                Status = status,
                UpdatedAt = updatedAt
            };

        private static FilterDefinition<ContentItem> ContentUsageFilter(ManagedResource resource)
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

            if (!string.IsNullOrWhiteSpace(resource.Url))
            {
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.HeroImageUrl, resource.Url));
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.ThumbnailUrl, resource.Url));
                filters.Add(Builders<ContentItem>.Filter.Eq(c => c.VideoUrl, resource.Url));
                filters.Add(Builders<ContentItem>.Filter.Eq("Attachments.Url", resource.Url));
                filters.Add(Builders<ContentItem>.Filter.Eq("BodyItems.Url", resource.Url));
                filters.Add(Builders<ContentItem>.Filter.Eq("GalleryItems.Url", resource.Url));
            }

            return Builders<ContentItem>.Filter.Or(filters);
        }

        private static FilterDefinition<ContentRevision> ContentRevisionUsageFilter(ManagedResource resource)
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

            if (!string.IsNullOrWhiteSpace(resource.Url))
            {
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.HeroImageUrl", resource.Url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.ThumbnailUrl", resource.Url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.VideoUrl", resource.Url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.Attachments.Url", resource.Url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.BodyItems.Url", resource.Url));
                filters.Add(Builders<ContentRevision>.Filter.Eq("Snapshot.GalleryItems.Url", resource.Url));
            }

            return Builders<ContentRevision>.Filter.Or(filters);
        }

        private static FilterDefinition<Section> SectionUsageFilter(string url) =>
            Builders<Section>.Filter.Or(
                Builders<Section>.Filter.Eq("Style.BackgroundImageUrl", url),
                Builders<Section>.Filter.Eq("Style.BackgroundVideoUrl", url),
                Builders<Section>.Filter.Eq("ImageUrl", url),
                Builders<Section>.Filter.Eq("Items.ImageUrl", url),
                Builders<Section>.Filter.Eq("ItemOverrides.CardImageUrl", url));

        private static FilterDefinition<Block> BlockUsageFilter(ManagedResource resource)
        {
            var filters = ResourceUrlVariants(resource.Url)
                .SelectMany(url => new[]
                {
                    Builders<Block>.Filter.Eq("ImageUrl", url),
                    Builders<Block>.Filter.Eq("FileUrl", url),
                    Builders<Block>.Filter.Eq("EmbedUrl", url)
                })
                .ToList();

            return Builders<Block>.Filter.Or(filters);
        }

        private static bool MatchesResource(string? resourceId, string? url, ManagedResource resource) =>
            string.Equals(resourceId, resource.Id, StringComparison.OrdinalIgnoreCase) ||
            MatchesUrl(url, resource.Url);

        private static bool MatchesUrl(string? value, string? resourceUrl) =>
            !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(resourceUrl) &&
            string.Equals(value.Trim(), resourceUrl.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool MatchesAnyUrl(string? value, IEnumerable<string> urls) =>
            urls.Any(url => MatchesUrl(value, url));

        private static List<string> ResourceUrlVariants(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new();

            var variants = new List<string> { value.Trim() };
            var embedUrl = VideoUrlHelper.ToEmbedUrl(value);
            if (!string.IsNullOrWhiteSpace(embedUrl) && !variants.Contains(embedUrl, StringComparer.OrdinalIgnoreCase))
                variants.Add(embedUrl);

            return variants;
        }

        private static string FirstText(Dictionary<string, string>? values)
        {
            if (values is null || values.Count == 0) return string.Empty;
            if (values.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) return en;
            return values.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static string SectionTitle(Section section) => section switch
        {
            HeroSection hero => FirstText(hero.Heading),
            CtaSection cta => FirstText(cta.Heading),
            ListSection list => FirstText(list.SectionTitle),
            ShowcaseSection showcase => FirstText(showcase.SectionTitle),
            LibrarySection library => FirstText(library.SectionTitle),
            StatsSection stats => FirstText(stats.SectionTitle),
            CarouselSection carousel => FirstText(carousel.SectionTitle),
            NetworkMapSection map => FirstText(map.SectionTitle),
            TestimonialSection testimonial => FirstText(testimonial.SectionTitle),
            _ => section.GetType().Name
        };
    }
}
