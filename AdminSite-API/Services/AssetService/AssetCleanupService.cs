using FullProject.Models;

namespace GlobalManager.Services.AssetService
{
    public class AssetCleanupService
    {
        private readonly AssetReferenceService _references;
        private readonly R2StorageService _storage;

        public AssetCleanupService(AssetReferenceService references, R2StorageService storage)
        {
            _references = references;
            _storage = storage;
        }

        public async Task<bool> DeleteIfUnusedAsync(string? oldUrl, string? replacementUrl, string? excludingManagedResourceId = null)
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return false;
            if (string.Equals(oldUrl, replacementUrl, StringComparison.OrdinalIgnoreCase)) return false;
            if (await _references.IsReferencedAsync(oldUrl, excludingManagedResourceId)) return false;

            try
            {
                return await _storage.DeleteAsync(oldUrl);
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> DeleteUnusedAsync(IEnumerable<string?> urls, string? excludingManagedResourceId = null)
        {
            var deleted = 0;
            foreach (var url in urls
                         .Where(url => !string.IsNullOrWhiteSpace(url))
                         .Select(url => url!.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await DeleteIfUnusedAsync(url, null, excludingManagedResourceId))
                    deleted++;
            }

            return deleted;
        }

        public Task<int> DeleteUnusedPageGraphAssetsAsync(
            IEnumerable<Page?> pages,
            IEnumerable<Section?> sections,
            IEnumerable<Block?> blocks,
            string? excludingManagedResourceId = null) =>
            DeleteUnusedAsync(
                pages.SelectMany(PageAssetUrls)
                    .Concat(sections.SelectMany(SectionAssetUrls))
                    .Concat(blocks.SelectMany(BlockAssetUrls)),
                excludingManagedResourceId);

        public Task<int> DeleteUnusedContentAssetsAsync(IEnumerable<ContentItem?> items, string? excludingManagedResourceId = null) =>
            DeleteUnusedAsync(items.SelectMany(ContentAssetUrls), excludingManagedResourceId);

        public IEnumerable<string?> PageAssetUrls(Page? page)
        {
            if (page is null) yield break;
            yield return page.Card?.CardImageUrl;
        }

        public IEnumerable<string?> SectionAssetUrls(Section? section)
        {
            if (section is null) yield break;

            yield return section.Style?.BackgroundImageUrl;
            yield return section.Style?.BackgroundVideoUrl;

            switch (section)
            {
                case HeroSection hero:
                    yield return hero.ImageUrl;
                    break;
                case ListSection list:
                    foreach (var item in list.Items) yield return item.ImageUrl;
                    break;
                case ShowcaseSection showcase:
                    foreach (var item in showcase.ItemOverrides) yield return item.CardImageUrl;
                    break;
                case CarouselSection carousel:
                    foreach (var item in carousel.Items) yield return item.ImageUrl;
                    break;
                case TestimonialSection testimonial:
                    foreach (var item in testimonial.Items) yield return item.ImageUrl;
                    break;
            }
        }

        public IEnumerable<string?> BlockAssetUrls(Block? block)
        {
            if (block is null) yield break;

            switch (block)
            {
                case ImageBlock image:
                    yield return image.ImageUrl;
                    break;
                case VideoBlock video:
                    yield return video.EmbedUrl;
                    break;
                case FileBlock file:
                    yield return file.FileUrl;
                    break;
                case CardBlock card:
                    yield return card.ImageUrl;
                    break;
            }
        }

        public IEnumerable<string?> ContentAssetUrls(ContentItem? item)
        {
            if (item is null) yield break;

            yield return item.HeroImageUrl;
            yield return item.ThumbnailUrl;
            yield return item.VideoUrl;
            foreach (var attachment in item.Attachments) yield return attachment.Url;
            foreach (var body in item.BodyItems) yield return body.Url;
            foreach (var gallery in item.GalleryItems)
            {
                yield return gallery.Url;
                yield return gallery.ThumbnailUrl;
            }
        }

        public IEnumerable<string?> RemovedAssetUrls(IEnumerable<string?> oldUrls, IEnumerable<string?> currentUrls)
        {
            var currentSet = currentUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return oldUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(url => !currentSet.Contains(url));
        }
    }
}
