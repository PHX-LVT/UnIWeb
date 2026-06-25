using System.Text.RegularExpressions;

namespace SharedComponents.Helpers
{
    public static class VideoUrlHelper
    {
        private static readonly Regex YouTubeVideoIdRegex = new("^[A-Za-z0-9_-]{6,}$", RegexOptions.Compiled);

        public static string? ToEmbedUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var cleaned = url.Trim();
            if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
                return cleaned;

            if (uri.Scheme is not ("http" or "https"))
                return null;

            var host = uri.Host.ToLowerInvariant();

            if (TryGetYouTubeEmbedUrl(cleaned, out var youtubeEmbedUrl))
                return youtubeEmbedUrl;

            if (host is "vimeo.com" or "www.vimeo.com")
            {
                var id = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
                return string.IsNullOrWhiteSpace(id)
                    ? cleaned
                    : $"https://player.vimeo.com/video/{id}";
            }

            return cleaned;
        }

        public static bool IsYouTubeVideoUrl(string? url) =>
            TryGetYouTubeEmbedUrl(url, out _);

        public static bool TryGetYouTubeEmbedUrl(string? url, out string embedUrl)
        {
            embedUrl = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme is not ("http" or "https"))
                return false;

            var id = ExtractYouTubeVideoId(uri);
            if (!IsValidYouTubeVideoId(id)) return false;

            embedUrl = $"https://www.youtube.com/embed/{id}";
            return true;
        }

        private static string? ExtractYouTubeVideoId(Uri uri)
        {
            var host = uri.Host.ToLowerInvariant();
            var segments = uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (host is "youtu.be")
                return segments.FirstOrDefault();

            if (!IsYouTubeHost(host))
                return null;

            if (segments.Length == 0)
                return ReadQueryValue(uri.Query, "v");

            return segments[0].ToLowerInvariant() switch
            {
                "watch" => ReadQueryValue(uri.Query, "v"),
                "embed" => segments.ElementAtOrDefault(1),
                "shorts" => segments.ElementAtOrDefault(1),
                "live" => segments.ElementAtOrDefault(1),
                "v" => segments.ElementAtOrDefault(1),
                _ => null
            };
        }

        private static bool IsYouTubeHost(string host) =>
            host is "youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com" ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);

        private static bool IsValidYouTubeVideoId(string? videoId) =>
            !string.IsNullOrWhiteSpace(videoId) &&
            YouTubeVideoIdRegex.IsMatch(videoId);

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
