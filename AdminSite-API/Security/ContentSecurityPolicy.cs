namespace FullProject.Security
{
    public static class ContentSecurityPolicy
    {
        public const int MaxTitleLength = 180;
        public const int MaxSummaryLength = 800;
        public const int MaxBodyItemLength = 25_000;
        public const int MaxBodyHtmlLength = 100_000;
        public const int MaxTags = 30;
        public const int MaxTagLength = 48;
        public const int MaxAttachments = 20;
        public const int MaxBodyItems = 10;

        public static void ValidateOptionalUrl(string? url, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!IsSafeUrl(url)) errors.Add($"{label} must be an http, https, or site-relative URL.");
        }

        public static bool IsAllowedVideoUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host.ToLowerInvariant();
            return host is "youtube.com" or "www.youtube.com" or "youtu.be";
        }

        public static bool IsAllowedAttachment(string? fileName, string? contentType)
        {
            var content = (contentType ?? string.Empty).Trim().ToLowerInvariant();
            if (content is "application/pdf" or "text/plain" ||
                content is "application/msword" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
                content is "application/vnd.ms-excel" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                content is "application/vnd.ms-powerpoint" or "application/vnd.openxmlformats-officedocument.presentationml.presentation")
                return true;

            var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            return ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt";
        }

        private static bool IsSafeUrl(string url)
        {
            var trimmed = url.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("/", StringComparison.Ordinal);
        }
    }
}
