namespace SharedComponents.Helpers
{
    public static class VideoUrlHelper
    {
        public static string? ToEmbedUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var cleaned = url.Trim();
            if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
                return cleaned;

            if (uri.Scheme is not ("http" or "https"))
                return null;

            var host = uri.Host.ToLowerInvariant();

            if (host is "youtu.be")
            {
                var id = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
                return BuildYouTubeEmbed(id);
            }

            if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
            {
                string? id = null;

                if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                    id = uri.AbsolutePath["/embed/".Length..].Split('/').FirstOrDefault();
                else if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase))
                    id = uri.AbsolutePath["/shorts/".Length..].Split('/').FirstOrDefault();
                else
                    id = ReadQueryValue(uri.Query, "v");

                return BuildYouTubeEmbed(id);
            }

            if (host is "vimeo.com" or "www.vimeo.com")
            {
                var id = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
                return string.IsNullOrWhiteSpace(id)
                    ? cleaned
                    : $"https://player.vimeo.com/video/{id}";
            }

            return cleaned;
        }

        private static string? BuildYouTubeEmbed(string? videoId) =>
            string.IsNullOrWhiteSpace(videoId)
                ? null
                : $"https://www.youtube.com/embed/{videoId}";

        private static string? ReadQueryValue(string query, string key)
        {
            foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    Uri.UnescapeDataString(pair[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return null;
        }
    }
}
